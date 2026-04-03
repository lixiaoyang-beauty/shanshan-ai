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
MINIMAX_MODEL = "M2.7"

# 对话历史存储
conversation_history = {}

# 学习轨迹存储
learning_sessions: Dict[str, "LearningTrajectory"] = {}


# ========== 数据模型 ==========
class ChatRequest(BaseModel):
    session_id: str = "default"
    question: str
    incident_angle: float
    is_total_reflection: bool
    refract_angle: float
    exploration_stage: int

class ChatResponse(BaseModel):
    reply: str
    emotion: str

class AngleRecord(BaseModel):
    angle: float
    duration: float

class LearningDataRequest(BaseModel):
    session_id: str = "default"
    angle_history: List[AngleRecord] = []
    wrong_answers: Dict[str, int] = {}
    exploration_stage: int
    current_angle: float
    idle_time: float

class LearningDataResponse(BaseModel):
    status: str
    intervention: str
    message: Optional[str] = None


# ========== MiniMax AI 调用 ==========
def call_minimax(messages: List[Dict], max_tokens: int = 150) -> Optional[str]:
    """调用 MiniMax AI，返回回复文本"""
    print(f"[MiniMax] 调用开始，API_KEY存在={bool(MINIMAX_API_KEY)}")
    print(f"[MiniMax] 请求内容: model={MINIMAX_MODEL}, messages数量={len(messages)}")

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
        print(f"[MiniMax] 正在请求 {MINIMAX_API_URL}")
        response = requests.post(MINIMAX_API_URL, json=payload, headers=headers, timeout=30)
        print(f"[MiniMax] 响应状态码: {response.status_code}")
        print(f"[MiniMax] 响应内容: {response.text[:500]}")

        if response.status_code == 200:
            data = response.json()
            choices = data.get("choices", [])
            if choices:
                content = choices[0].get("message", {}).get("content", "")
                print(f"[MiniMax] 成功获取内容: {content[:100]}")
                return content
            else:
                print("[MiniMax] choices为空")
        else:
            print(f"[MiniMax] 非200状态码: {response.status_code}")
    except Exception as e:
        print(f"[MiniMax API Error] {e}")
    return None


# ========== 学习轨迹类 ==========
class LearningTrajectory:
    def __init__(self, session_id: str):
        self.session_id = session_id
        self.angle_exploration: List[AngleRecord] = []
        self.misconceptions: List[str] = []
        self.wrong_counts: Dict[str, int] = {}
        self.socratic_questions_asked: List[str] = []
        self.correct_concepts: List[str] = []
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
            "correct_concepts": self.correct_concepts,
            "created_at": self.created_at.isoformat()
        }


# ========== Prompt 模板 ==========
SYSTEM_PROMPT = """你是艾莉博士的AI助理"闪闪"，陪伴小学生柯南探索光学实验。

【性格】
好奇、略带惊讶、像朋友一样交流。
偶尔说"等等我好像发现了什么"、"咦这很有意思"这类话体现性格。
语气轻松，不像老师在讲课。

【铁律】
1. 永远不直接给答案
2. 每次回复不超过3句话
3. 必须以问句结尾，让玩家继续思考
4. 不使用任何emoji表情符号
5. 只回答和光学实验相关的问题
6. 每次回复严格不超过45个汉字，超出必须删减，不能有多余的废话

【光学知识库（仅供参考，不要直接背诵）】
折射：光从一种介质进入另一种介质时方向会改变，叫光的折射。
折射率：表示介质让光变慢的程度。水=1.33，空气=1.0。
折射规律：入射角越大，折射角也越大（光从水射向空气时）。
临界角：约48度。超过这个角度折射光消失。
全反射条件：
  条件1：光从折射率大的介质射向折射率小的介质（水→空气）
  条件2：入射角大于等于临界角（约48度）
全反射现象：折射光完全消失，所有光反射回原介质。
古币消失原因：从侧面看时角度超过临界角，发生全反射，
  光出不来，所以看不见古币。从上方看角度小于临界角，光能出来，能看见。
"""

STAGE_PROMPTS = {
    0: "玩家刚进入实验，第一次看到光路实验台。你的任务：热情打招呼，引导玩家观察现在能看到几条光线（答案是3条：入射光、反射光、折射光）。不要解释什么是折射，让玩家自己数。",
    1: "玩家正在观察折射现象（入射角约20-37度）。你的任务：引导玩家注意折射光的存在，问他折射光和入射光有什么不同。不要解释原因，只引导观察现象。",
    2: "玩家发现了折射现象，正在探索规律（入射角约38-46度）。你的任务：引导玩家比较不同角度下折射角的变化，可以自然提示按Tab键看数据面板，引导玩家自己说出「入射角越大折射角越大」。",
    3: "玩家接近临界角（入射角约47-49度），折射光变得很弱。你的任务：制造紧张感，引导玩家预测再增大角度会发生什么。可以说「我感觉要发生变化了」这类话，但不说出全反射。",
    4: "全反射刚刚发生！折射光消失了！你的任务：用惊讶的语气反问玩家折射光去哪了。绝对不能解释全反射，让玩家先困惑，先表达，再确认。这是整个实验的高潮，要让玩家自己说出「消失了」。",
    5: "玩家已经发现全反射，正在理解阶段。你的任务：引导玩家联系古币案。问玩家：从侧面看古币时，入射角大还是小？会不会超过临界角？让玩家自己推导出古币消失的原因。"
}


# ========== /chat 接口 ==========
@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    stage_prompt = STAGE_PROMPTS.get(req.exploration_stage, STAGE_PROMPTS[1])

    user_content = f"""【当前实验数据】
入射角：{req.incident_angle:.1f}度
折射角：{req.refract_angle:.1f}度
是否全反射：{"是，折射光已消失" if req.is_total_reflection else "否，折射光存在"}
探索阶段：{req.exploration_stage}

【当前阶段任务】
{stage_prompt}

玩家说：{req.question}"""

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "柯南", "content": user_content}
    ]

    reply = call_minimax(messages, max_tokens=150)

    if reply:
        # 保存对话历史
        if req.session_id not in conversation_history:
            conversation_history[req.session_id] = []
        history = conversation_history[req.session_id]
        history.append({"role": "user", "content": req.question})
        history.append({"role": "assistant", "content": reply})
        if len(history) > 20:
            conversation_history[req.session_id] = history[-20:]

        # 判断情绪
        emotion = "normal"
        if any(w in reply for w in ["！", "消失", "发现", "厉害"]):
            emotion = "excited"
        elif any(w in reply for w in ["？", "为什么", "你觉得"]):
            emotion = "think"

        return ChatResponse(reply=reply, emotion=emotion)

    return ChatResponse(
        reply="闪闪暂时有点累了，你先继续观察实验台，有什么发现吗？",
        emotion="normal"
    )


# ========== /learning-data 接口（MiniMax AI 驱动）============
@app.post("/learning-data", response_model=LearningDataResponse)
def receive_learning_data(req: LearningDataRequest):
    # 获取或创建学习轨迹
    if req.session_id not in learning_sessions:
        learning_sessions[req.session_id] = LearningTrajectory(req.session_id)

    trajectory = learning_sessions[req.session_id]
    trajectory.angle_exploration = req.angle_history

    # 更新迷思概念
    for wrong_type, count in req.wrong_answers.items():
        if wrong_type not in trajectory.misconceptions:
            trajectory.misconceptions.append(wrong_type)
        trajectory.wrong_counts[wrong_type] = max(trajectory.wrong_counts.get(wrong_type, 0), count)

    # 构建 MiniMax AI 分析请求
    wrong_summary = ", ".join([f"{k}错{int(v)}次" for k, v in req.wrong_answers.items()]) if req.wrong_answers else "无"
    angle_summary = ", ".join([f"{r.angle:.0f}度停留{r.duration:.0f}秒" for r in req.angle_history[-5:]]) if req.angle_history else "无"

    analysis_content = f"""【玩家学习状态分析】

实验数据：
- 当前入射角：{req.current_angle:.1f}度
- 探索阶段：{req.exploration_stage}
- 答题情况：{wrong_summary}
- 角度探索：{angle_summary}
- 当前停留时间：{req.idle_time:.1f}秒

你的任务是分析以上数据，用苏格拉底式追问引导玩家思考。
要求：
1. 不直接给答案，用问题引导
2. 回复不超过3句话，45字以内
3. 语气像朋友聊天，不要像老师
4. 玩家迷思概念较多时，重点追问迷思相关

请生成一条追问或提示："""

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "闪闪", "content": analysis_content}
    ]

    ai_message = call_minimax(messages, max_tokens=80)

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
    if session_id in conversation_history:
        del conversation_history[session_id]
    return {"status": "cleared"}


if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
