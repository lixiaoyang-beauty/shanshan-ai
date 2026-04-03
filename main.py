import os
import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from zhipuai import ZhipuAI
from typing import Optional, List, Dict
from datetime import datetime

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

client = ZhipuAI(api_key=os.getenv("ZHIPU_API_KEY"))

# 对话历史存储
conversation_history = {}

# 学习轨迹存储
learning_sessions: Dict[str, "LearningTrajectory"] = {}

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

# ========== 新增：学习数据接口 ==========
class AngleRecord(BaseModel):
    angle: float
    duration: float  # 秒

class LearningDataRequest(BaseModel):
    session_id: str = "default"
    angle_history: List[AngleRecord] = []
    wrong_answers: Dict[str, int] = {}  # {"类型": 次数}
    exploration_stage: int
    current_angle: float
    idle_time: float  # 秒

class LearningDataResponse(BaseModel):
    status: str
    intervention: str  # "none" | "gentle_hint" | "socratic_question" | "positive"
    message: Optional[str] = None

# ========== 干预策略生成 ==========
def generate_intervention(session_id: str, data: LearningDataRequest) -> LearningDataResponse:
    """分析玩家学习状态，返回干预策略"""
    trajectory = learning_sessions.get(session_id)

    # 更新迷思概念记录
    for wrong_type, count in data.wrong_answers.items():
        if trajectory:
            if wrong_type not in trajectory.misconceptions:
                trajectory.misconceptions.append(wrong_type)
            trajectory.wrong_counts[wrong_type] = max(trajectory.wrong_counts.get(wrong_type, 0), count)

    # 1. 角度停滞检测：玩家在 <5° 范围停留超过20秒
    stagnant_angles = [r for r in data.angle_history if r.duration > 20]
    if stagnant_angles:
        return LearningDataResponse(
            status="received",
            intervention="socratic_question",
            message="你在某个角度停留很久了，是不是有什么疑问？光线的方向有什么变化吗？"
        )

    # 2. 快速滑动没停留 → 乱玩
    if len(data.angle_history) >= 3:
        avg_duration = sum(r.duration for r in data.angle_history[-5:]) / min(5, len(data.angle_history))
        if avg_duration < 1.5:
            return LearningDataResponse(
                status="received",
                intervention="gentle_hint",
                message="慢一点观察，光线在不同角度时方向有什么变化？"
            )

    # 3. 反复答错同一类 → 迷思概念
    for wrong_type, count in data.wrong_answers.items():
        if count >= 2:
            if wrong_type == "refraction_rule":
                msg = "你觉得光从水射向空气时，方向会怎么变？换一个角度再观察观察。"
            elif wrong_type == "critical_angle":
                msg = "还记得临界角吗？当折射角等于90度时的入射角就叫临界角哦。再想想看！"
            elif wrong_type == "total_reflection":
                msg = "折射光消失后，光去了哪里呢？注意看反射光有没有变化！"
            else:
                msg = "再仔细观察实验台，光线有什么变化？"
            return LearningDataResponse(
                status="received",
                intervention="socratic_question",
                message=msg
            )

    # 4. 玩家探索到关键角度时的鼓励
    if data.current_angle >= 45 and data.current_angle < 50 and data.exploration_stage >= 3:
        return LearningDataResponse(
            status="received",
            intervention="positive",
            message="你已经接近临界角了！再增大一点点，看看会发生什么！"
        )

    # 5. 全都对了 → 鼓励
    total_wrong = sum(data.wrong_answers.values())
    if total_wrong == 0 and len(data.angle_history) >= 5:
        return LearningDataResponse(
            status="received",
            intervention="positive",
            message="太棒了！你的观察力很强！继续探索吧！"
        )

    return LearningDataResponse(status="received", intervention="none", message=None)

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

# ========== 学习数据接口 ==========
@app.post("/learning-data", response_model=LearningDataResponse)
def receive_learning_data(req: LearningDataRequest):
    # 获取或创建学习轨迹
    if req.session_id not in learning_sessions:
        learning_sessions[req.session_id] = LearningTrajectory(req.session_id)

    trajectory = learning_sessions[req.session_id]
    trajectory.angle_exploration = req.angle_history

    # 分析并生成干预
    result = generate_intervention(req.session_id, req)

    # 记录干预
    if result.intervention != "none" and result.message:
        trajectory.add_intervention(result.intervention, result.message)

    return result

@app.get("/learning-trajectory/{session_id}")
def get_trajectory(session_id: str):
    """获取某个session的学习轨迹（用于后续分析）"""
    if session_id not in learning_sessions:
        return {"error": "session not found"}
    return learning_sessions[session_id].to_dict()

# ========== 原有聊天接口 ==========
KNOWLEDGE = """
【光学知识库-小学四年级版】
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
本实验：光从水底射向水面，滑块控制入射角0-85度。
"""

STAGE_PROMPTS = {
    0: """
玩家刚进入实验，第一次看到光路实验台。
你的任务：热情打招呼，引导玩家观察现在能看到几条光线（答案是3条：入射光、反射光、折射光）。
不要解释什么是折射，让玩家自己数。
""",
    1: """
玩家正在观察折射现象（入射角约20-37度）。
你的任务：引导玩家注意折射光的存在，问他折射光和入射光有什么不同。
不要解释原因，只引导观察现象。
""",
    2: """
玩家发现了折射现象，正在探索规律（入射角约38-46度）。
你的任务：引导玩家比较不同角度下折射角的变化，
可以自然提示按Tab键看数据面板，引导玩家自己说出"入射角越大折射角越大"。
""",
    3: """
玩家接近临界角（入射角约47-49度），折射光变得很弱。
你的任务：制造紧张感，引导玩家预测再增大角度会发生什么。
可以说"我感觉要发生变化了"这类话，但不说出全反射。
""",
    4: """
全反射刚刚发生！折射光消失了！
你的任务：用惊讶的语气反问玩家折射光去哪了。
绝对不能解释全反射，让玩家先困惑，先表达，再确认。
这是整个实验的高潮，要让玩家自己说出"消失了"。
""",
    5: """
玩家已经发现全反射，正在理解阶段。
你的任务：引导玩家联系古币案。
问玩家：从侧面看古币时，入射角大还是小？会不会超过临界角？
让玩家自己推导出古币消失的原因。
"""
}

@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    stage_prompt = STAGE_PROMPTS.get(req.exploration_stage, STAGE_PROMPTS[1])

    system_prompt = f"""你是艾莉博士的AI助理"闪闪"，陪伴小学生柯南探索光学实验。

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

【当前实验数据】
入射角：{req.incident_angle:.1f}度
折射角：{req.refract_angle:.1f}度
是否全反射：{"是，折射光已消失" if req.is_total_reflection else "否，折射光存在"}
探索阶段：{req.exploration_stage}

【当前阶段任务】
{stage_prompt}

【光学知识库（仅供参考，不要直接背诵）】
{KNOWLEDGE}

【回答理解策略】
如果玩家说"不知道"或"随便"→温和提示："没关系，再看看实验台，光线有什么变化？"
如果玩家回答方向正确→正向追问，深化理解
如果玩家回答偏了→不批评，用问题引导回来："你觉得光线的角度有没有变化？"
"""

    # 获取或初始化对话历史
    if req.session_id not in conversation_history:
        conversation_history[req.session_id] = []

    history = conversation_history[req.session_id]

    # 构建消息列表
    messages = [{"role": "system", "content": system_prompt}]
    messages.extend(history)
    messages.append({"role": "user", "content": req.question})

    try:
        response = client.chat.completions.create(
            model="glm-4-flash",
            messages=messages,
            max_tokens=150,
            temperature=0.8
        )
        reply = response.choices[0].message.content

        # 保存对话历史（最多10轮=20条）
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

    except Exception as e:
        return ChatResponse(
            reply="闪闪暂时有点累了，你先继续观察实验台，有什么发现吗？",
            emotion="normal"
        )

@app.delete("/session/{session_id}")
def clear_session(session_id: str):
    if session_id in conversation_history:
        del conversation_history[session_id]
    return {"status": "cleared"}

if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
