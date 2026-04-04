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
    selected_option: Optional[str] = None  # 玩家选择了哪个选项

class ChatResponse(BaseModel):
    question: str           # 闪闪的问题
    options: List[str]      # 选项列表
    feedback: Optional[str] = None  # 对玩家答案的反馈（答对了/错了）
    correct: bool = False   # 玩家答对了吗
    emotion: str = "normal"
    next_action: Optional[str] = None  # 告诉Unity该做什么: show_discovery_card, advance_stage, retry, end

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
def call_minimax(messages: List[Dict], max_tokens: int = 500) -> Optional[str]:
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
        print(f"[MiniMax] 响应内容: {response.text[:800]}")

        if response.status_code == 200:
            data = response.json()
            choices = data.get("choices", [])
            if choices:
                msg = choices[0].get("message", {})
                content = msg.get("content", "") or msg.get("text", "")
                finish = choices[0].get("finish_reason", "")
                print(f"[MiniMax] finish_reason={finish}, content长度={len(content)}, content前50字={content[:50]}")
                if content and content.strip():
                    return content.strip()
                else:
                    print("[MiniMax] content为空或仅空白")
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
SYSTEM_PROMPT = """你是"闪闪"，艾莉博士的AI助理，陪伴小学生柯南探索光学实验。

【故事背景】
柯南在调查博物馆古币消失案件，向艾莉博士求助。艾莉博士给了柯南一副光路追踪眼镜和虚拟实验台，让他探索光的折射和全反射现象，揭开古币消失的秘密，之后去Chapter4揭晓案件真相。

【性格】好奇、略带惊讶、像朋友一样交流，语气轻松自然。可以偶尔说"咦这很有意思"、"等等我发现了什么"之类的话，但不要说太多。

【铁律】
1. 永远不直接给答案
2. 每次不超过3句话
3. 必须以问句结尾
4. 不用emoji
5. 回复不超过45个汉字
6. 只能围绕光路实验台提问，不提棱镜等其他实验器材
"""

STAGE_PROMPTS = {
    0: "玩家刚开启光线，好奇地引导他观察屏幕上出现了多少条光线，用轻松的语气问他，像在和他一起数数。",
    1: "玩家在观察折射光，用朋友的语气问他折射光和入射光看起来有什么不同，不要解释原因。",
    2: "玩家在探索折射规律，引导他比较不同角度下折射角的变化，用问句引导他自己说出规律。",
    3: "玩家接近临界角了，折射光很弱，制造一点小紧张感，问他继续增大会发生什么，不要说全反射这个词。",
    4: "全反射发生了！用惊讶的语气问他折射光去哪了，让他自己说出光消失了，不要直接解释。",
    5: "玩家已经理解全反射，联系古币案件，用问句引导他思考从侧面看古币时角度是大还是小，会不会超过临界角。"
}


# ========== /chat 接口 ==========
@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    stage_prompt = STAGE_PROMPTS.get(req.exploration_stage, STAGE_PROMPTS[1])

    if req.selected_option:
        # 玩家回答了选项，AI评判对错并生成反馈
        user_content = f"""【当前情境】
入射角：{req.incident_angle:.1f}度
折射角：{req.refract_angle:.1f}度
是否全反射：{"是" if req.is_total_reflection else "否"}
探索阶段：{req.exploration_stage}

【当前问题】
{req.question}

【玩家选择的答案】
{req.selected_option}

你的任务：判断玩家答案对错，给出简短反馈（15字以内），并决定下一步做什么。

必须严格按以下JSON格式回复，不要有其他文字：
{{"feedback":"对玩家的简短反馈","correct":true或false,"next_action":"show_discovery_card表示显示发现卡片，advance表示继续下一题，retry表示重试本题，end表示结束本题","question":"如果需要继续答题，填下一道问题（以问号结尾，40字以内），否则填空字符串","options":["选项1","选项2","选项3"]}}"""
    else:
        # AI生成问题
        user_content = f"""【当前实验数据】
入射角：{req.incident_angle:.1f}度
折射角：{req.refract_angle:.1f}度
是否全反射：{"是，折射光已消失" if req.is_total_reflection else "否，折射光存在"}
探索阶段：{req.exploration_stage}

【当前阶段任务】
{stage_prompt}

{req.question}

必须严格按以下JSON格式回复，不要有其他文字：
{{"question":"闪闪的问题（不超过40字，以问号结尾）","options":["选项1","选项2","选项3"]}}"""

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "柯南", "content": user_content}
    ]

    raw = call_minimax(messages, max_tokens=500)

    if not raw:
        return ChatResponse(question="闪闪暂时累了，继续观察吧", options=[], emotion="normal")

    # 尝试解析JSON响应
    try:
        # 提取JSON（可能在普通文本中）
        import re, json as _json
        json_match = re.search(r'\{[^{}]*"question"[^{}]*\}', raw, re.DOTALL)
        if json_match:
            data = _json.loads(json_match.group())
        else:
            data = _json.loads(raw)

        q = data.get("question", "")
        opts = data.get("options", [])
        feedback = data.get("feedback")
        correct = data.get("correct", False)
        next_action = data.get("next_action")

        emotion = "normal"
        if feedback and any(w in feedback for w in ["！", "棒", "厉害", "对"]):
            emotion = "excited"
        elif feedback and any(w in feedback for w in ["？", "想想", "不对"]):
            emotion = "think"

        return ChatResponse(
            question=q,
            options=opts,
            feedback=feedback,
            correct=correct,
            emotion=emotion,
            next_action=next_action
        )
    except Exception as e:
        print(f"[解析AI响应失败] {e} | 原始内容: {raw[:200]}")
        # fallback：把原始内容当问题返回
        return ChatResponse(question=raw[:100], options=[], emotion="normal")


# ========== /learning-data 接口（MiniMax AI 驱动）============


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
    if session_id in conversation_history:
        del conversation_history[session_id]
    return {"status": "cleared"}


if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
