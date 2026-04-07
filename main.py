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
    next_action: str = ""  # "retry" / "advance" / "discovery_card" / "lecture"
    question: Optional[str] = None
    options: Optional[List[str]] = None


class LearningDataRequest(BaseModel):
    session_id: str = "default"
    question_id: str = ""
    selected_option: str = ""       # 玩家选了什么
    correct_answer: str = ""        # 正确答案是什么
    wrong_count: int = 0            # 本题错了几次
    exploration_stage: int = 0
    current_angle: float = 0.0
    idle_time: float = 0.0


class LearningDataResponse(BaseModel):
    status: str
    socratic_question: Optional[str] = None  # 苏格拉底追问（AI生成）


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
    "q_line_count": "想想看，光线到达水面时，会同时产生反射和折射吗？",
    "q_refraction_rule": "观察一下，当入射角变大时，折射光线会往哪个方向偏？",
    "q_critical_angle": "当折射角变成90度时，这时的入射角有个特殊的名字...想想看？",
    "q_prediction": "注意看折射光的亮度，它在慢慢变...还是...？",
    "q_total_reflection": "反射光和折射光，哪个变亮了？光好像被'留住'了？",
    "q_verify": "全反射需要两个条件同时满足，回忆一下发现卡片里说的两个条件？",
    "q_coin": "从侧面看古币时，光线要经过哪个介质出去？角度大还是小？",
}

HINTS_LEVEL2 = {
    "q_line_count": "光线遇到水面时，一部分会反射回空气，一部分会折射进入水中，再加上原来的入射光，一共是3条哦！",
    "q_refraction_rule": "想象一下，筷子插进水里的弯曲角度，当入射角变大，折射角也跟着变大，对吗？",
    "q_critical_angle": "当折射角达到90度时，即光线贴着水面传播，这时的入射角就叫做临界角。",
    "q_prediction": "随着入射角增大，折射光越来越暗，最后几乎看不见了——说明折射光在变弱。",
    "q_total_reflection": "当光从水中射向空气且入射角大于临界角时，光不再折射，而是全部反射回水中，这就是全反射！",
    "q_verify": "全反射条件：①光从水射向空气（光密→光疏）；②入射角大于等于临界角！",
    "q_coin": "从侧面看时角度倾斜很大，入射角一定大于临界角——那光还出得来吗？",
}

# ========== 第三次错误讲解 ==========
LECTURES = {
    "q_line_count": "光线遇到水面时，会同时发生反射和折射。入射光、反射光、折射光，一共3条光线。这是光线分解的基本现象。",
    "q_refraction_rule": "光的折射规律：入射角越大，折射角越大。就像筷子插进水里的样子，角度会变大而不是变小。继续增大角度看看！",
    "q_critical_angle": "当折射角达到90度时，这时的入射角叫做临界角。临界角是全反射现象发生的关键条件。继续增大角度超过临界角看看！",
    "q_prediction": "随着入射角增大，折射光越来越暗，说明折射光线的能量在逐渐减弱。继续增大角度观察！",
    "q_total_reflection": "当光从光密介质射向光疏介质，且入射角大于临界角时，光不再发生折射，而是全部反射回介质内部，这就是全反射现象！",
    "q_verify": "全反射的两个条件：①光从水射向空气（光密→光疏）；②入射角大于等于临界角。两个缺一不可！",
    "q_coin": "从侧面看古币时，入射角很大，超过了临界角，光全反射出不来——这就是古币消失的秘密！",
}


# ========== 预设问题和选项 ==========
# 每个问题对应的问题文本和选项（方案A：后端直接返回，不调MiniMax）
PRESET_QUESTIONS = {
    "q_line_count": {
        "question": "哇，你看到光线了！你能观察到几条光线呀？",
        "options": ["1条", "2条", "3条"]
    },
    "q_prediction": {
        "question": "好有意思！如果继续增大人射角，你觉得折射光会怎么样？",
        "options": ["变得更强", "逐渐消失", "方向不变"]
    },
    "q_refraction_rule": {
        "question": "你发现规律了吗？人射角变大时，折射角怎么变？",
        "options": ["折射角会变大", "折射角会变小", "折射角不变"]
    },
    "q_critical_angle": {
        "question": "折射光越来越弱了！当折射角刚好等于90度时，那个人射角有特别的名字，叫什么呢？",
        "options": ["临界角", "折射角", "人射角", "反射角"]
    },
    "q_total_reflection": {
        "question": "哇！折射光消失了！光到底去了哪里呢？",
        "options": ["光消失了", "光全部反射回水中", "光被水吸收了"]
    },
    "q_verify": {
        "question": "你知道吗？发生全反射需要满足两个条件，是哪两个呢？",
        "options": [
            "光从水射向空气，人射角>=临界角",
            "光从空气射向水，角度越大越好",
            "只要角度够大就会全反射"
        ]
    },
    "q_coin": {
        "question": "最后来想想：从侧面观察古币时，人射角是大还是小？会不会发生全反射呢？",
        "options": ["大于临界角", "小于临界角"]
    },
}

# ========== 预设问题和答案 ==========
PRESET_ANSWERS = {
    # stage 1: 能看到几条光线
    "q_line_count": {
        "correct": "3条",
        "feedback_correct": "答对！3条：入射光、反射光、折射光。折射就是光从水进入空气时方向改变！试试拖大角度观察！",
        "feedback_wrong": "再仔细看！应该有3条：入射光、反射光、折射光。试试拖动滑块！"
    },
    # stage 2: 预测挑战 - 折射光变弱还是变强
    "q_prediction": {
        "correct": "逐渐消失",
        "feedback_correct": "你预测对了！折射光越来越弱，继续增大角度验证！",
        "feedback_wrong": "再仔细观察折射光的强度变化，注意看亮度！"
    },
    # stage 3: 折射规律
    "q_refraction_rule": {
        "correct": "折射角会变大",
        "feedback_correct": "答对！折射规律：入射角越大，折射角也越大，这就是折射定律！继续增大角度，快到临界角了！",
        "feedback_wrong": "入射角越大，折射角也越大——想想筷子插进水里的样子！"
    },
    # stage 4: 临界角
    "q_critical_angle": {
        "correct": "临界角",
        "feedback_correct": "正确！折射角=90度时的入射角叫临界角！继续增大角度超过48度看看！",
        "feedback_wrong": "当折射角刚好等于90度时，这时的入射角叫做临界角！"
    },
    # stage 5: 全反射
    "q_total_reflection": {
        "correct": "光全部反射回水中",
        "feedback_correct": "完全正确！这就是全反射现象！折射光完全消失，所有光反射回水中！",
        "feedback_wrong": "注意看反射光——它变亮了！说明光反射回去了！"
    },
    # 验证题: 全反射条件
    "q_verify": {
        "correct": "光从水射向空气，入射角>=临界角",
        "feedback_correct": "完全正确！全反射的两个条件缺一不可！",
        "feedback_wrong": "全反射条件：光从水射向空气，且入射角大于等于临界角！"
    },
    # 古币题
    "q_coin": {
        "correct": "大于临界角",
        "feedback_correct": "对了！从侧面看角度大，超过临界角，光全反射出不来——古币消失！",
        "feedback_wrong": "从侧面看时角度大，超过临界角，光全反射出不来！"
    },
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
# 方案A：预设问题 + 预设评判，不调MiniMax生成问题
@app.post("/chat", response_model=ChatResponse)
def chat(req: ChatRequest):
    qid = req.question_id
    selected = req.selected_option
    wrong_count = req.wrong_count

    # 情况1：有 question_id，要获取预设问题和选项（没有 selected_option）
    if qid and qid in PRESET_QUESTIONS and not selected:
        preset_q = PRESET_QUESTIONS[qid]
        return ChatResponse(
            correct=False,
            feedback="",
            next_action="show_options",
            question=preset_q["question"],
            options=preset_q["options"]
        )

    # 情况2：有 question_id + selected_option，进行预设评判
    if qid and qid in PRESET_ANSWERS:
        preset = PRESET_ANSWERS[qid]
        correct = (selected == preset["correct"])

        if correct:
            feedback = preset["feedback_correct"]
            if qid == "q_total_reflection":
                return ChatResponse(correct=True, feedback=feedback, next_action="discovery_card")
            return ChatResponse(correct=True, feedback=feedback, next_action="advance")

        # 答错了
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

    # 记录错题
    if req.question_id and req.selected_option:
        misconception = ""
        if req.question_id in MISCONCEPTIONS and req.selected_option in MISCONCEPTIONS[req.question_id]:
            misconception = MISCONCEPTIONS[req.question_id][req.selected_option]
        trajectory.add_wrong(req.question_id, req.selected_option, misconception)

    # 构建苏格拉底追问（AI分析玩家迷思概念）
    # 针对每道题给出具体的追问策略，让MiniMax生成有针对性的追问
    qid = req.question_id or ""
    wrong_opt = req.selected_option or ""

    # 各问题的追问策略指引
    QUESTION_GUIDANCE = {
        "q_line_count": {
            "misconception_map": {
                "1条": "误以为只有入射光，忽略了反射和折射",
                "2条": "混淆了入射光和反射光，或漏掉了折射"
            },
            "focus_hint": "光碰到水面时会同时产生反射和折射",
            "socratic_angle": "从光源到水面，光是一起走还是分开走了？"
        },
        "q_refraction_rule": {
            "misconception_map": {
                "折射角会变小": "误以为入射角越大折射角越小",
                "折射角不变": "不理解折射角随入射角变化的规律"
            },
            "focus_hint": "入射角变大，折射角跟着变",
            "socratic_angle": "对比一下角度小时和角度大时，折射光偏向哪边？"
        },
        "q_critical_angle": {
            "misconception_map": {
                "折射角": "混淆了入射角和折射角",
                "反射角": "混淆了反射和折射的定义"
            },
            "focus_hint": "折射角=90度时，这个入射角有特殊名字",
            "socratic_angle": "折射角变成90度（即贴着水面走）时的入射角，叫什么呢？"
        },
        "q_total_reflection": {
            "misconception_map": {
                "光消失了": "误以为光消失了",
                "光被水吸收了": "误以为光被介质吸收"
            },
            "focus_hint": "反射光变亮了，说明光反射回去了",
            "socratic_angle": "注意看反射光——它变亮了！光去哪了？"
        },
        "q_verify": {
            "misconception_map": {
                "只要角度够大": "忽略了光从哪个介质到哪个介质",
                "空气到水": "搞反了折射方向"
            },
            "focus_hint": "全反射需要①水到空气②角度>=临界角",
            "socratic_angle": "回忆一下：光从水里射向空气容易全反射，还是从空气射向水？"
        },
        "q_coin": {
            "misconception_map": {
                "小于临界角": "误以为从侧面看角度小"
            },
            "focus_hint": "从侧面看时光线倾斜厉害，入射角很大",
            "socratic_angle": "从侧面看时，光线要倾斜很大角度出去，入射角是大还是小？"
        },
    }

    guidance = QUESTION_GUIDANCE.get(qid, {})
    misconception_map = guidance.get("misconception_map", {})
    socratic_angle = guidance.get("socratic_angle", "仔细观察实验台，答案就在现象里！")

    misconception_text = misconception_map.get(wrong_opt, "对折射/反射规律有误解")

    # 追问语气策略：第1次错用引导性追问，第2-3次错用更直接的追问
    if req.wrong_count == 1:
        tone_hint = "语气温和，像朋友聊天，用问句引导，不要给答案"
    else:
        tone_hint = "语气稍微直接一点，但仍然用问句，可以稍微给点方向提示"

    analysis_content = f"""【玩家答题情况】
探索阶段：{req.exploration_stage}
当前入射角：{req.current_angle:.1f}度
本题错了几次：{req.wrong_count}次
玩家选择了：{wrong_opt}
正确答案：{req.correct_answer}
玩家的迷思概念：{misconception_text}
追问角度提示：{socratic_angle}

{tone_hint}，不超过20字，生成一句苏格拉底式追问。"""

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "闪闪", "content": analysis_content}
    ]

    ai_message = call_minimax(messages, max_tokens=100)

    socratic = ""
    if ai_message and ai_message.strip():
        trajectory.add_socratic(ai_message)
        socratic = ai_message.strip()[:50]

    return LearningDataResponse(
        status="received",
        socratic_question=socratic if socratic else None
    )


@app.get("/learning-trajectory/{session_id}")
def get_trajectory(session_id: str):
    if session_id not in learning_sessions:
        return {"error": "session not found"}
    return learning_sessions[session_id].to_dict()


@app.get("/trajectory/summary/{session_id}")
def get_summary(session_id: str):
    """返回迷思概念摘要，用于章节结束时展示"""
    if session_id not in learning_sessions:
        return {"misconceptions": [], "total_wrong": 0, "socratic_questions": []}
    t = learning_sessions[session_id]
    return {
        "misconceptions": t.misconceptions,
        "total_wrong": len(t.wrong_records),
        "socratic_questions": t.socratic_questions[-3:],  # 最近3条追问
    }


@app.delete("/session/{session_id}")
def clear_session(session_id: str):
    if session_id in learning_sessions:
        del learning_sessions[session_id]
    return {"status": "cleared"}


# ========== /hint 接口（玩家卡住时，由 MiniMax 生成简短提示语）============
class HintRequest(BaseModel):
    hint_context: str = ""       # Unity 描述玩家当前状态
    exploration_stage: int = 0  # 当前阶段


class HintResponse(BaseModel):
    hint_text: str  # 闪闪的一句简短提示


@app.post("/hint", response_model=HintResponse)
def hint(req: HintRequest):
    """
    Unity 的 GiveHint() 调用这个接口。
    MiniMax 分析玩家当前状态，生成一句简短提示语（不超过20字）。
    """
    if not req.hint_context:
        return HintResponse(hint_text="试试调节角度，观察光线有什么变化！")

    messages = [
        {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
        {"role": "user", "name": "柯南", "content": req.hint_context}
    ]

    raw = call_minimax(messages, max_tokens=80)

    if raw and raw.strip():
        text = raw.strip()[:30]
        return HintResponse(hint_text=text)

    # MiniMax 不可用时的预设提示
    fallback_hints = [
        "试试把角度调大一点！",
        "注意观察折射光的变化！",
        "把角度调到40度以上看看！",
        "仔细看反射光有没有变化！",
    ]
    import random
    return HintResponse(hint_text=random.choice(fallback_hints))


if __name__ == "__main__":
    port = int(os.getenv("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
