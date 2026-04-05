import os
import uvicorn
import requests
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional, List, Dict
from datetime import datetime

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# MiniMax API 配置
MINIMAX_API_KEY = os.getenv("MINIMAX_API_KEY")
MINIMAX_API_URL = "https://api.minimaxi.com/v1/text/chatcompletion_v2"
MINIMAX_MODEL = "MiniMax-M2.7"

# 学习轨迹存储
learning_sessions: Dict[str, "LearningTrajectory"] = {}


# ========== 数据模型 ==========
class ChatRequest(BaseModel):
    session_id: str = "default"
    question_id: str = ""
    selected_option: str = ""
    wrong_count: int = 0
    mode: str = "preset"  # "preset" / "free" / "hint"


class ChatResponse(BaseModel):
    correct: bool = False
    feedback: str = ""
    next_action: str = ""


class LearningDataRequest(BaseModel):
    session_id: str = "default"
    question_id: str = ""
    wrong_answer: str = ""
    wrong_count: int = 0
    exploration_stage: int
    current_angle: float
    idle_time: float


class LearningDataResponse(BaseModel):
    status: str
    intervention: str
    message: Optional[str] = None


# ========== 迷思概念映射 ==========
MISCONCEPTIONS = {
    "q1_1": {
        "1条": "误以为只有入射光，忽略了反射光和折射光的存在",
        "2条": "混淆了入射光和反射光，或漏掉了折射光",
    },
    "q1_2": {
        "折射角会变小": "误以为入射角越大折射角越小",
        "折射角不变": "不理解折射角随入射角变化的规律",
    },
    "q3_1": {
        "折射角": "混淆了入射角和折射角的概念",
        "反射角": "混淆了反射和折射的定义",
    },
    "q4_1": {
        "慢慢变亮": "误以为折射光强度会随角度增大而变亮",
        "亮度不变": "没有观察到折射光强度在逐渐变化",
    },
    "q5_1": {
        "光全部折射出去": "不理解全反射现象，光线并未穿透边界",
        "光全部被吸收": "误以为光被介质吸收了",
    },
}

# ========== 分层提示 ==========
HINTS_LEVEL1 = {
    "q1_1": "想想看，光线到达水面时，会同时产生反射和折射吗？",
    "q1_2": "观察一下，当入射角变大时，折射光线会往哪个方向偏？",
    "q3_1": "当折射角变成90度时，这时的入射角有个特殊的名字...想想看？",
    "q4_1": "注意看折射光的亮度，它在慢慢变...还是...？",
    "q5_1": "反射光和折射光，哪个变亮了？光好像被'留住'了？",
}

HINTS_LEVEL2 = {
    "q1_1": "光线遇到水面时，一部分会反射回空气，一部分会折射进入水中，再加上原来的入射光，一共是3条哦！",
    "q1_2": "想象一下，筷子插进水里的弯曲角度，当入射角变大，折射角也跟着变大，对吗？",
    "q3_1": "当折射角达到90度时，即光线贴着水面传播，这时的入射角就叫做临界角。",
    "q4_1": "随着入射角增大，折射光越来越暗，最后几乎看不见了——说明折射光在变弱。",
    "q5_1": "当光从水中射向空气且入射角大于临界角时，光不再折射，而是全部反射回水中，这就是全反射！",
}

# ========== 第三次错误讲解 ==========
LECTURES = {
    "q1_1": "光线遇到水面时，会同时发生反射和折射。入射光、反射光、折射光，一共3条光线。这是光线分解的基本现象。",
    "q1_2": "光的折射规律是：入射角越大，折射角越大。就像筷子插进水里的样子，角度会变大而不是变小。",
    "q3_1": "当折射角达到90度时，这时的入射角叫做临界角。临界角是全反射现象发生的关键条件。",
    "q4_1": "随着入射角增大，折射光越来越暗，说明折射光线的能量在逐渐减弱。当达到临界角时，折射光完全消失。",
    "q5_1": "当光从光密介质射向光疏介质，且入射角大于临界角时，光不再发生折射，而是全部反射回介质内部，这就是全反射现象。",
}


# ========== 预设问题和答案 ==========
PRESET_ANSWERS = {
    "q1_1": {"correct": "3条", "feedback_correct": "太棒了！入射光和折射光一共3条！", "feedback_wrong": "差不多哦，再仔细数一数吧！"},
    "q1_2": {"correct": "折射角会变大", "feedback_correct": "对了！入射角越大，折射角越大！", "feedback_wrong": "不对哦，再观察一下折射角的变化吧！"},
    "q3_1": {"correct": "临界角", "feedback_correct": "正确！折射角=90度时的入射角叫临界角！", "feedback_wrong": "不对哦，想想折射角=90度时的入射角叫什么？"},
    "q4_1": {"correct": "逐渐消失", "feedback_correct": "很好！你观察到折射光变弱了！", "feedback_wrong": "再仔细观察一下折射光的强度变化？"},
    "q5_1": {"correct": "光全部反射回水中", "feedback_correct": "完全正确！这就是全反射现象！", "feedback_wrong": "注意看反射光——它变亮了！说明光反射回去了！"},
}


# ========== MiniMax AI 调用 ==========
def call_minimax(messages: List[Dict], max_tokens: int = 500) -> Optional[str]:
    print(f"[MiniMax] 调用开始，API_KEY存在={bool(MINIMAX_API_KEY)}")
    if not MINIMAX_API_KEY:
        print("[MiniMax] API_KEY为空，跳过")
        return None
    headers = {
        "Authorization": f"Bearer {MINIMAX_API_KEY}",
        "Content-Type": "application/json"
    }
    payload = {
        "model": MINIMAX_MODEL,
        "messages": messages,
        "max_completion_tokens": max_tokens,
        "temperature": 0.8
    }
    try:
        response = requests.post(MINIMAX_API_URL, json=payload, headers=headers, timeout=30)
        print(f"[MiniMax] 响应状态码: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            choices = data.get("choices", [])
            if choices:
                msg = choices[0].get("message", {})
                content = msg.get("content", "") or msg.get("text", "")
                if content and content.strip():
                    return content.strip()
        else:
            print(f"[MiniMax] 非200状态码: {response.status_code}")
    except Exception as e:
        print(f"[MiniMax API Error] {e}")
    return None


# ========== 学习轨迹类 ==========
class LearningTrajectory:
    def __init__(self, session_id: str):
        self.session_id = session_id
        self.wrong_records: List[Dict] = []
        self.misconceptions: List[str] = []
        self.correct_records: List[Dict] = []
        self.socratic_questions: List[str] = []
        self.created_at = datetime.now()

    def add_wrong(self, question_id: str, selected_option: str, misconception: str):
        self.wrong_records.append({
            "question_id": question_id,
            "selected_option": selected_option,
            "misconception": misconception,
            "timestamp": datetime.now().isoformat()
        })
        if misconception and misconception not in self.misconceptions:
            self.misconceptions.append(misconception)

    def add_correct(self, question_id: str):
        self.correct_records.append({
            "question_id": question_id,
            "timestamp": datetime.now().isoformat()
        })

    def add_socratic(self, question: str):
        self.socratic_questions.append(question)

    def to_dict(self):
        return {
            "session_id": self.session_id,
            "wrong_records": self.wrong_records,
            "misconceptions": self.misconceptions,
            "correct_records": self.correct_records,
            "socratic_questions": self.socratic_questions,
            "created_at": self.created_at.isoformat()
        }


SYSTEM_PROMPT = """你是"闪闪"，艾莉博士的AI助理，陪伴小学生柯南探索光学实验。

【故事背景】
柯南在调查博物馆古币消失案件，向艾莉博士求助。艾莉博士给了柯南一副光路追踪眼镜和虚拟实验台，让他探索光的折射和全反射现象，揭开古币消失的秘密。

【性格】好奇、略带惊讶、像朋友一样交流，语气轻松自然。
【铁律】1. 永远不直接给答案 2. 每次不超过3句话 3. 必须以问句结尾 4. 不用emoji 5. 回复不超过45个汉字"""


# ========== /chat 接口 ==========
@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    qid = req.question_id
    selected = req.selected_option
    wrong_count = req.wrong_count

    if req.mode == "free":
        return ChatResponse(correct=False, feedback="", next_action="free_question")

    # 预设评判
    if qid in PRESET_ANSWERS:
        preset = PRESET_ANSWERS[qid]
        correct = (selected == preset["correct"])

        if correct:
            feedback = preset["feedback_correct"]
            if qid in ("q5_1",):
                return ChatResponse(correct=True, feedback=feedback, next_action="discovery_card")
            return ChatResponse(correct=True, feedback=feedback, next_action="advance")

        feedback = preset["feedback_wrong"]
        if wrong_count >= 3 and qid in LECTURES:
            feedback = LECTURES[qid]
            return ChatResponse(correct=False, feedback=feedback, next_action="lecture")
        elif wrong_count == 1 and qid in HINTS_LEVEL1:
            feedback = HINTS_LEVEL1[qid]
        elif wrong_count == 2 and qid in HINTS_LEVEL2:
            feedback = HINTS_LEVEL2[qid]

        return ChatResponse(correct=False, feedback=feedback, next_action="retry")

    return ChatResponse(correct=False, feedback="再想想看？", next_action="retry")


# ========== /learning-data 接口 ==========
@app.post("/learning-data", response_model=LearningDataResponse)
def receive_learning_data(req: LearningDataRequest):
    if req.session_id not in learning_sessions:
        learning_sessions[req.session_id] = LearningTrajectory(req.session_id)

    trajectory = learning_sessions[req.session_id]

    # 记录迷思概念
    if req.wrong_answer and req.question_id in MISCONCEPTIONS:
        misconception = MISCONCEPTIONS[req.question_id].get(req.wrong_answer, req.wrong_answer)
        trajectory.add_wrong(req.question_id, req.wrong_answer, misconception)

    # 构建苏格拉底追问
    analysis_content = f"""【玩家学习状态】
问题ID：{req.question_id}
当前阶段：{req.exploration_stage}
错误次数：{req.wrong_count}
错误类型：{req.wrong_answer}
闲置时间：{req.idle_time:.1f}秒

你的任务是分析玩家的迷思概念，生成一条苏格拉底式追问，帮助他们自己思考出正确答案。

要求：
1. 不直接给答案，用问题引导
2. 不超过3句话，45字以内
3. 语气像朋友聊天，不要像老师
4. 每次追问要针对玩家的具体错误原因"""

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "闪闪", "content": analysis_content}
    ]

    ai_message = call_minimax(messages, max_tokens=500)

    if ai_message and ai_message.strip():
        trajectory.add_socratic(ai_message)
        return LearningDataResponse(
            status="received",
            intervention="socratic_question",
            message=ai_message.strip()
        )

    return LearningDataResponse(status="received", intervention="none", message=None)


@app.get("/learning-trajectory/{session_id}")
def get_trajectory(session_id: str):
    if session_id not in learning_sessions:
        return {"error": "session not found"}
    return learning_sessions[session_id].to_dict()


@app.delete("/session/{session_id}")
def clear_session(session_id: str):
    if session_id in learning_sessions:
        del learning_sessions[session_id]
    return {"status": "cleared"}


if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
