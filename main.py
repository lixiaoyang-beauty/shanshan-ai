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
    # ShanShanAsk 会发送以下字段
    question: str = ""             # 当前上下文/情境描述
    incident_angle: float = 0
    is_total_reflection: bool = False
    refract_angle: float = 0
    exploration_stage: int = 0
    # 答题历史（个性化用）
    answer_sequence: str = ""      # 本题历次选项，逗号分隔，如 "1条,2条"
    global_wrong_topics: int = 0   # 这局共在几道题上出过错


class ChatResponse(BaseModel):
    correct: bool = False
    feedback: str = ""
    next_action: str = ""  # "retry" / "advance" / "discovery_card" / "lecture" / "socratic_retry"
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

# 各问题的苏格拉底追问策略（供 MiniMax 生成个性化追问用）
QUESTION_GUIDANCE = {
    "q_line_count": {
        "misconception_map": {
            "1条": (
                "这个孩子脑子里只有「一束光从A直走到B」的直觉，"
                "完全没有意识到光遇到两种介质的界面时会同时分裂成两条——一条反射回去、一条折射进去。"
                "他盯着入射光看，却没有往水面两侧追踪光的走向。"
                "引导方向：让他注意光源射出后、水面上同时有几个地方在发光。"
            ),
            "2条": (
                "这个孩子注意到光分裂了，找到了两条，但漏掉了第三条。"
                "最常见情形：找到了反射光和折射光，却把入射光当成了其中一条算进去，导致重复计数；"
                "或者找到了入射光和反射光，漏掉了方向偏折不那么明显的折射光。"
                "他知道「不止一条」，但对三条光的名字和来源还没有清晰区分。"
                "引导方向：帮他数清楚——光从哪来（入射），打到水面后往哪去了（反射往回、折射往另一侧）。"
            ),
        },
    },
    "q_refraction_rule": {
        "misconception_map": {
            "折射角会变小": (
                "这个孩子把「折射光靠近法线」和「折射角变小」搞混了。"
                "他可能以为光折射后方向压得越来越「陡」，就等于折射角越来越小——"
                "但实际上角度是从法线量的，入射角增大时折射角也在增大，只是比入射角小。"
                "引导方向：让他对比角度很小时和角度很大时的折射光线，看看它离法线是更近了还是更远了。"
            ),
            "折射角不变": (
                "这个孩子以为折射角是一个固定值，与入射角无关。"
                "他可能还没有建立「角度越大，折射越厉害」的物理直觉。"
                "引导方向：让他慢慢拖动滑块，亲眼观察折射光是不是随角度在动。"
            ),
        },
    },
    "q_critical_angle": {
        "misconception_map": {
            "折射角": (
                "这个孩子选了「折射角」，说明他搞混了主语——"
                "题目问的是「此刻的入射角叫什么名字」，他却回答了「折射角」。"
                "这是语言层面的混淆：折射角已经等于90度了，"
                "而我们要给起名字的是此时的那个入射角，不是折射角本身。"
                "引导方向：区分「折射角=90度」（这是条件）和「此时的入射角叫什么」（这是问题）。"
            ),
            "反射角": (
                "这个孩子把全反射（折射消失）和反射角联系在一起，"
                "以为折射光消失时，特殊的是反射角。"
                "但临界角是折射角刚好变成90度时的那个入射角，和反射角无关。"
                "引导方向：让他注意此刻折射光在哪——是不是正好贴着水面走，对应90度？"
            ),
            "入射角": (
                "这个孩子知道答案和入射角有关，但不知道有个专门名字，"
                "于是直接选了「入射角」。他的理解方向是对的，只差「临界角」这个专有名词。"
                "引导方向：轻推一步——这个特殊状态的入射角有个专门的名字，叫「临界」角。"
            ),
        },
    },
    "q_total_reflection": {
        "misconception_map": {
            "光消失了": (
                "这个孩子只盯着折射光那一侧看，看到折射光不见了就认为光消失了，"
                "完全没有注意到反射光那侧其实变亮了——光不是没了，是全跑回水里去了。"
                "引导方向：让他把目光转向反射光一侧，问他那边是不是突然变亮了。"
            ),
            "光被水吸收了": (
                "这个孩子没有观察到现象，在猜——「光不见了，那肯定被什么吸收了」。"
                "他缺少「能量守恒」的直觉：光的能量不会凭空消失，"
                "要么折射出去，要么反射回来，只有这两条路。"
                "引导方向：让他问自己——反射光变亮了还是变暗了？那些能量去了哪里？"
            ),
        },
    },
    "q_verify": {
        "misconception_map": {
            "光从空气射向水，角度越大越好": (
                "这个孩子把全反射的方向搞反了。"
                "全反射只发生在光从「光密」介质射向「光疏」介质时（水→空气），"
                "从空气射向水时，光只会折射进去，不会全反射。"
                "引导方向：回忆一下，刚才实验里光是从哪里射向哪里的，"
                "是空气射向水，还是水射向空气？"
            ),
            "只要角度够大就会全反射": (
                "这个孩子记住了「角度要够大」，但忘了还有一个前提："
                "必须是光从光密介质（水）射向光疏介质（空气），缺一不可。"
                "他以为随便哪个方向只要角度大就能全反射。"
                "引导方向：问他，如果从空气射向水，角度再大会全反射吗？让他对比实验里的条件。"
            ),
        },
    },
    "q_coin": {
        "misconception_map": {
            "小于临界角": (
                "这个孩子没有建立「侧面=大角度」的空间直觉。"
                "他可能以为「侧面看」和「斜看」是一回事，或者凭感觉觉得侧面的角度并不算大。"
                "实际上从极度侧面看时，光线几乎平着打向水面，入射角接近90度，远超临界角。"
                "引导方向：让他想象自己趴在桌边从侧面看一枚硬币——光线几乎是「横着」射向水面的，"
                "角度大不大？"
            ),
        },
    },
}

PRESET_QUESTIONS = {
    "q_line_count": {
        "question": "哇！你看到光线了吗？好有意思～咦，光好像不只是一条路线呢！你仔细看看，能观察到几条光线呀？",
        "options": ["1条", "2条", "3条"]
    },
    "q_prediction": {
        "question": "哇，折射光是不是越来越暗了～好有意思！如果继续增大角度，你觉得折射光会怎么样呢？",
        "options": ["变得更强", "逐渐消失", "方向不变"]
    },
    "q_refraction_rule": {
        "question": "咦，你发现了吗？角度小的时候和角度大的时候，折射光偏折的程度好像不太一样......人射角变大时，折射角会怎么变呢？",
        "options": ["折射角会变大", "折射角会变小", "折射角不变"]
    },
    "q_critical_angle": {
        "question": "哇，折射光越来越暗了，都快要消失了一样！好神奇～当折射角刚好等于90度（贴着水面走）时，这时的入射角有特别的名字，叫什么呢？",
        "options": ["临界角", "折射角", "人射角", "反射角"]
    },
    "q_total_reflection": {
        "question": "哇！折射光完全消失了！等等......光真的不见了吗？你仔细看看反射光那边——咦，是不是反而变亮了？光到底去了哪里呀？",
        "options": ["光消失了", "光全部反射回水中", "光被水吸收了"]
    },
    "q_verify": {
        "question": "太厉害了！发现了全反射现象！咦，不过要发生全反射可没那么简单哦～还记得发现卡片里说的两个条件吗？一起说说是哪两个？",
        "options": [
            "光从水射向空气，人射角>=临界角",
            "光从空气射向水，角度越大越好",
            "只要角度够大就会全反射"
        ]
    },
    "q_coin": {
        "question": "叮叮！恭喜你发现了古币消失的秘密！还记得发现卡片里讲的吗？从侧面看古币时，光线要倾斜着射出水面——这个时候的入射角是大还是小呢？会不会发生全反射呀？",
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
def call_minimax(messages: List[Dict], max_tokens: int = 600) -> Optional[str]:
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
            try:
                data = response.json()
            except Exception:
                # MiniMax 有时返回 extra data（多段 JSON），取第一段
                import json as _json
                decoder = _json.JSONDecoder()
                data, _ = decoder.raw_decode(response.text.strip())
            print(f"[MiniMax] 原始响应: {data}")  # 调试：看实际返回结构
            choices = data.get("choices", [])
            if choices:
                first_choice = choices[0]
                # finish_reason=length 表示被截断，content为空，返回None让C#走兜底
                if first_choice.get("finish_reason") == "length":
                    print("[MiniMax] truncated, returning None")
                    return None
                msg = first_choice.get("message", {})
                content = msg.get("content") or msg.get("text") or ""
                print(f"[MiniMax] content: '{str(content)[:80]}'")
                if content and str(content).strip():
                    return str(content).strip()
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
【铁律】1. 永远不直接给答案 2. 每次不超过3句话 3. 必须以问句结尾 4. 不用emoji 5. 回复不超过60个汉字"""


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

        # 答错了 → MiniMax 个性化苏格拉底追问
        feedback = preset["feedback_wrong"]

        # 第3次错 → 强制讲解，不再追问
        if wrong_count >= 3 and qid in LECTURES:
            feedback = LECTURES[qid]
            return ChatResponse(correct=False, feedback=feedback, next_action="lecture")

        # 解析答题序列
        seq_list = [s.strip() for s in req.answer_sequence.split(",") if s.strip()] if req.answer_sequence else []
        seq_display = " → ".join(seq_list) if len(seq_list) > 1 else selected

        # 读取针对这道题、这个具体错误选项的认知分析
        guidance = QUESTION_GUIDANCE.get(qid, {})
        misconception_map = guidance.get("misconception_map", {})
        # 找到最匹配的迷思描述（模糊匹配：selected 包含在 key 中即命中）
        misconception_text = next(
            (v for k, v in misconception_map.items() if k in selected or selected in k),
            f"对「{selected}」这个选项有误解，具体原因不明"
        )

        # 分层策略：错次不同，引导的深度和方式不同
        if wrong_count == 1:
            depth_guide = (
                "第1次答错，语气温和像朋友。"
                "不否定他，而是帮他发现自己「忽略了什么」。"
                "用「你可能觉得…但你注意到…了吗？」的结构，"
                "引导他去观察一个具体的、他此刻能看到的现象。"
            )
        elif wrong_count == 2:
            depth_guide = (
                "第2次答错了，语气稍微直接。"
                "承认他看到的部分是对的，再明确指出哪里有矛盾。"
                "给他一个具体的观察动作，问句收尾。"
            )
        else:
            depth_guide = (
                "第3次答错，可以结合生活类比来解释为什么，"
                "给出非常明确的观察方向，问句收尾。"
                "但绝对不能直接说出正确答案。"
            )

        # 全局学习状态描述
        global_note = ""
        if req.global_wrong_topics >= 3:
            global_note = f"（注意：这个孩子这局已经在{req.global_wrong_topics}道题上答错过，说明他对整个折射/反射体系的理解都还比较模糊，需要更基础的引导。）"
        elif req.global_wrong_topics >= 2:
            global_note = f"（这个孩子这局在{req.global_wrong_topics}道题上答错，有一定的基础误解。）"

        # 答题序列描述
        seq_note = ""
        if len(seq_list) >= 2:
            seq_note = f"他这道题的答题轨迹是：{seq_display}。上一次选的是「{seq_list[-2]}」，这次换成了「{selected}」——说明他在尝试，但方向还没对。"
        else:
            seq_note = f"这是他第一次答错这道题，选了「{selected}」。"

        analysis_content = f"""你是「闪闪」，正在陪一个小学生「柯南」做光学实验。现在他答错了一道题，你需要帮他发现自己哪里想错了。

【他现在的状态】
{seq_note}
{global_note}
当前实验：入射角 {req.incident_angle:.1f}度，折射角 {req.refract_angle:.1f}度，{'此时已发生全反射' if req.is_total_reflection else '尚未发生全反射'}。

【他为什么会选「{selected}」——认知分析】
{misconception_text}

【你的任务】
根据上面的认知分析，生成1句【引导反馈】，帮他发现自己的思维漏洞。
要求：
- {depth_guide}
- 句子要自然流畅，像朋友说话，不要像试题解析
- 结尾必须是问句
- 不超过50字（重要：输出前请数清楚，确保不超过50个汉字）
- 严禁出现「正确答案是」「应该选」「答案是」等直接泄底的话

回复格式（严格JSON，不含其他文字）：
{{"guided":"引导反馈内容"}}"""

        messages = [
            {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
            {"role": "user", "name": "闪闪", "content": analysis_content}
        ]

        ai_message = call_minimax(messages, max_tokens=600)

        guided_feedback = ""
        if ai_message and ai_message.strip():
            raw = ai_message.strip()
            # 去掉 Markdown 代码块围栏（MiniMax 有时会自动加上）
            raw = raw.strip()
            if raw.startswith("```"):
                lines = raw.split("\n")
                # 去掉首行 ```json 和末行 ```
                if len(lines) >= 2 and lines[0].strip().startswith("```"):
                    lines = lines[1:]
                if len(lines) >= 1 and lines[-1].strip().startswith("```"):
                    lines = lines[:-1]
                raw = "\n".join(lines).strip()
            try:
                import json
                decoder = json.JSONDecoder()
                data, _ = decoder.raw_decode(raw)
                guided_feedback = data.get("guided", "")[:50]
                print(f"[/chat] MiniMax guided: '{guided_feedback}'")
            except:
                # JSON 解析仍失败：原文直接使用
                guided_feedback = raw[:50] if raw else ""
                print(f"[/chat] JSON解析失败，使用原文: '{guided_feedback}'")

        # AI 失败时，本地兜底（C# 的 GetWrongHint 也会处理，这里是双保险）
        if not guided_feedback:
            guided_feedback = f"你选了「{selected}」，再仔细观察一下——现象是不是和你想的不太一样？"
            print(f"[/chat] AI失败，使用本地兜底: '{guided_feedback}'")

        print(f"[/chat] 返回 ChatResponse(feedback='{guided_feedback[:30]}...', next_action='socratic_retry')")
        return ChatResponse(
            correct=False,
            feedback=guided_feedback,
            next_action="socratic_retry",
            question=preset.get("question", ""),
            options=PRESET_QUESTIONS[qid].get("options") if qid in PRESET_QUESTIONS else preset.get("options", [])
        )

    # 情况3：玩家自由提问（AskBtn）→ MiniMax 生成回答
    if req.question and not qid:
        free_question_prompt = f"""【玩家自由提问】
        探索阶段：{req.exploration_stage}
        当前入射角：{req.incident_angle:.1f}度
        折射角：{req.refract_angle:.1f}度
        是否全反射：{'是' if req.is_total_reflection else '否'}
        玩家的问题是：{req.question}

        你是"闪闪"，艾莉博士的AI助理，用轻松友好的语气回答玩家的问题。
        要求：1.不直接给答案 2.不超过3句话 3.必须以问句结尾 4.不用emoji 5.回复不超过45个汉字
        如果玩家问的问题和当前实验现象有关，要引导他结合实验观察来思考。"""

        messages = [
            {"role": "system", "name": "闪闪", "content": SYSTEM_PROMPT},
            {"role": "user", "name": "柯南", "content": free_question_prompt}
        ]

        raw = call_minimax(messages, max_tokens=300)

        reply = ""
        if raw and raw.strip():
            try:
                import json
                data = json.loads(raw)
                reply = data.get("content", "") or data.get("text", "") or raw.strip()
            except:
                reply = raw.strip()
        else:
            reply = "让我想想......嗯，我也有点不确定，我们一起继续观察吧！"

        return ChatResponse(
            correct=False,
            feedback="",
            next_action="free_answer",
            question=reply[:50] if len(reply) > 50 else reply,
            options=[]
        )

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

    ai_message = call_minimax(messages, max_tokens=300)

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

    raw = call_minimax(messages, max_tokens=200)

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
