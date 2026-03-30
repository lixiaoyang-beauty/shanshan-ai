import os
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from zhipuai import ZhipuAI
from typing import Optional

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

client = ZhipuAI(api_key="5829d2dcde0e400c8adb6bcf27799948.jjx85nTfpPyiocWt")

# 对话历史存储
conversation_history = {}

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
