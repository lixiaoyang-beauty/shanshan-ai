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
    question_id: str = ""          # 预设问题ID
    selected_option: str = ""      # 玩家选择的选项
    exploration_stage: int = 0


class ChatResponse(BaseModel):
    correct: bool = False
    feedback: str = ""
    next_action: str = ""  # "retry" / "advance" / "discovery_card"


# ========== 预设问题和答案 ==========
PRESET_ANSWERS = {
    # stage 1: 能看到几条光线
    "q1_1": {"correct": "3条",   "feedback_correct": "太棒了！入射光和折射光一共3条！", "feedback_wrong": "差不多哦，再仔细数一数吧！"},
    "q1_2": {"correct": "折射角会变大", "feedback_correct": "对了！入射角越大，折射角越大！", "feedback_wrong": "不对哦，再观察一下折射角的变化吧！"},
    # stage 3: 临界角
    "q3_1": {"correct": "临界角", "feedback_correct": "正确！折射角=90度时的入射角叫临界角！", "feedback_wrong": "不对哦，想想折射角=90度时的入射角叫什么？"},
    # stage 4: 折射光变弱
    "q4_1": {"correct": "逐渐消失", "feedback_correct": "很好！你观察到折射光变弱了！", "feedback_wrong": "再仔细观察一下折射光的强度变化？"},
    # stage 5: 全反射
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
        self.misconceptions: List[str] = []
        self.wrong_counts: Dict[str, int] = {}
        self.socratic_questions_asked: List[str] = []
        self.created_at = datetime.now()

    def add_intervention(self, intervention_type: str, message: str):
        if intervention_type == "socratic_question":
            self.socratic_questions_asked.append(message)

    def to_dict(self):
        return {
            "session_id": self.session_id,
            "misconceptions": self.misconceptions,
            "wrong_counts": self.wrong_counts,
            "socratic_questions": self.socratic_questions_asked,
            "created_at": self.created_at.isoformat()
        }


SYSTEM_PROMPT = """你是"闪闪"，艾莉博士的AI助理，陪伴小学生柯南探索光学实验。

【故事背景】
柯南在调查博物馆古币消失案件，向艾莉博士求助。艾莉博士给了柯南一副光路追踪眼镜和虚拟实验台，让他探索光的折射和全反射现象，揭开古币消失的秘密。

【性格】好奇、略带惊讶、像朋友一样交流，语气轻松自然。
【铁律】1. 永远不直接给答案 2. 每次不超过3句话 3. 必须以问句结尾 4. 不用emoji 5. 回复不超过45个汉字"""


# ========== /chat 接口（预设评判）============
@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    qid = req.question_id
    selected = req.selected_option

    # 预设评判：查表直接返回
    if qid in PRESET_ANSWERS:
        preset = PRESET_ANSWERS[qid]
        correct = (selected == preset["correct"])
        feedback = preset["feedback_correct"] if correct else preset["feedback_wrong"]

        if qid in ("q5_1",) and correct:
            return ChatResponse(correct=True, feedback=feedback, next_action="discovery_card")
        elif correct:
            return ChatResponse(correct=True, feedback=feedback, next_action="advance")
        else:
            return ChatResponse(correct=False, feedback=feedback, next_action="retry")

    # 未知问题ID，默认重试
    return ChatResponse(correct=False, feedback="再想想看？", next_action="retry")


# ========== /learning-data 接口（AI个性化追问）============
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


@app.post("/learning-data", response_model=LearningDataResponse)
def receive_learning_data(req: LearningDataRequest):
    if req.session_id not in learning_sessions:
        learning_sessions[req.session_id] = LearningTrajectory(req.session_id)

    trajectory = learning_sessions[req.session_id]

    # 记录迷思概念
    if req.wrong_answer:
        trajectory.wrong_counts[req.wrong_answer] = max(
            trajectory.wrong_counts.get(req.wrong_answer, 0), req.wrong_count
        )

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
        trajectory.add_intervention("socratic_question", ai_message)
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
