# 《消失的硬币》项目设计文档
> 给 Cursor/Claude 的上下文文档，开发时请先阅读此文件

---

## 项目基本信息
- **项目名称**：消失的硬币（Disappearing Coin）
- **引擎版本**：Unity 2022.3.57f1c2
- **开发截止**：2025年3月22日
- **类型**：教育解谜游戏，光学物理知识（折射/全反射）
- **脚本编辑器**：Cursor（双击脚本自动打开）

---

## 游戏结构

### 章节列表
| 章节 | 场景名 | 主题 | 状态 |
|------|--------|------|------|
| 第一章 | Chapter1_Museum | 引入谜题 | 已完成 |
| 第二章 | Chapter2_Lab | 基础光学 | 已完成 |
| 第三章 | Chapter3_Lab | 光学折射实验 | 完成但UI需重做 |
| 第四章 | Chapter4_Museum | 博物馆侦探破案 | 开发中 ← 当前 |
| 结束 | SampleScene | 通关 | 待做 |

### Build Settings 场景顺序
- Index 0: SampleScene
- Index 1: Chapter3_Lab
- Index 2: Chapter4_Museum（当前）

---

## 第四章详细设计

### 场景文件
`Assets/Scenes/Chapter4_Museum.unity`

### Hierarchy结构
```
Chapter4_Museum
├── Directional Light
├── Background
├── Camera（主摄像机，Tag: MainCamera）
├── Museum_01（展厅主体）
├── GameManager（挂载所有脚本）
│   ├── DialogueManager.cs
│   ├── SceneTransitionManager.cs
│   └── Chapter4Manager.cs
├── Curator（馆长NPC，Transform引用）
├── Security（保安NPC，Transform引用）
├── EventSystem（自动生成）
└── Canvas
    └── DialoguePanel（手动摆好的UI）
        ├── BgImage（Dialogue.jpg背景）
        ├── AvatarImage（头像，动态切换）
        ├── SpeakerName（TextMeshPro，金色名牌位置）
        ├── ContentText（TextMeshPro，羊皮纸区域）
        └── ContinueBtn（Button+TextMeshPro）
```

### 剧情流程
```
进场
→ [自动] 馆长+保安对话4句（玩家不能移动）
→ [开放移动] 提示"走近馆长"
→ [E键交互] 玩家介入对话5句
→ [选择题1] 硬币消失原因（正确：全反射）
→ [答对] 2句对话
→ [选择题2] 换角度能看见原因（正确：折射）
→ [答对] 4句对话
→ [结案报告弹窗]
→ [跳转] SampleScene
```

### 角色列表
| 角色 | 头像文件 | Inspector变量名 |
|------|----------|----------------|
| 馆长 | Assets/UI/Curator.jpg | curatorAvatar |
| 保安 | Assets/UI/Security.jpg | securityAvatar |
| 柯南 | Assets/UI/Conan.jpg | conanAvatar |

### 对话格式
脚本内对话格式：`"角色名|对话内容"`
例：`"馆长|你就是那个小侦探？"`

---

## UI系统设计规范

### 第四章 UI 图片（均在 Assets/UI/）
| 用途 | 文件名 | 尺寸 |
|------|--------|------|
| 对话框背景 | Dialogue.jpg | 1400×400 |
| 选择题面板 | Choice Panel.jpg | 1200×700 |
| 结案报告 | Case Closed Report.jpg | 1000×750 |
| 进场提示条 | Notification Bar.jpg | 800×90 |
| 交互提示 | Interaction Prompt Badge.jpg | 320×80 |
| 馆长头像 | Curator.jpg | 512×512 |
| 保安头像 | Security.jpg | 512×512 |
| 柯南头像 | Conan.jpg | 512×512 |
| 艾莉头像 | Ally.jpg | 512×512 |

### UI颜色规范
```
深蓝背景：#0A1535
金色边框：#D4AF37
羊皮纸文字：#261A07（深棕）
金牌文字：#1A0F02
```

### 字体
- 所有TMP文字使用：`MSYH SDF`（微软雅黑，Assets/Fonts/）
- DialogueManager.chineseFont 字段存储字体引用

---

## 核心脚本说明

### Chapter4Manager.cs
**路径**：`Assets/Scripts/Chapter4Manager.cs`
**挂载**：GameManager对象

**公开字段（Inspector连线）**：
```csharp
public DialogueManager dialogueManager;  // → GameManager
public Camera mainCamera;                // → Camera
public Transform curatorNPC;             // → Curator
public Transform securityNPC;            // → Security
public Sprite curatorAvatar;             // → Assets/UI/Curator.jpg
public Sprite conanAvatar;               // → Assets/UI/Conan.jpg
public Sprite securityAvatar;            // → Assets/UI/Security.jpg
public Sprite dialogueBgSprite;          // → Assets/UI/Dialogue.jpg
```

**UI引用（从Canvas子对象获取，用SerializeField）**：
```csharp
// 需要新增的字段——对话框手动摆好后引用
public TextMeshProUGUI speakerNameText;   // → SpeakerName
public TextMeshProUGUI contentText;       // → ContentText
public Image avatarImage;                 // → AvatarImage
public Button continueButton;             // → ContinueBtn
```

### DialogueManager.cs
**路径**：`Assets/Scripts/DialogueManager.cs`
**公开字段**：
```csharp
public TMP_FontAsset chineseFont;   // → MSYH SDF
public Sprite defaultAvatar;        // → Ally Doctor
```

### SceneTransitionManager.cs
**路径**：`Assets/Scripts/SceneTransitionManager.cs`
**用法**：`SceneTransitionManager.LoadScene("SampleScene")`

---

## 第四章 TODO 清单

### 立即要做
- [ ] Chapter4Manager.cs 改为引用手动摆好的UI对象（SerializeField）
- [ ] 在Inspector里把 SpeakerName/ContentText/AvatarImage/ContinueBtn 连线
- [ ] 测试完整对话流程
- [ ] 制作选择题面板UI（Choice Panel.jpg）
- [ ] 制作结案报告弹窗（Case Closed Report.jpg）
- [ ] 制作进场提示条（Notification Bar.jpg）

### 第四章完成后
- [ ] 第三章UI重做（Lab Dialogue.jpg / Lab Choice.jpg / Experiment Report.jpg）
- [ ] 主菜单界面
- [ ] 通关结算界面
- [ ] 毕业论文撰写

---

## Cursor 使用指南

### 日常工作流
1. 在Unity双击脚本 → 自动在Cursor打开
2. `Ctrl+L` 打开AI Chat
3. 把报错直接粘贴进Chat，或说"帮我实现XXX功能"
4. Cursor直接修改当前文件，不用复制粘贴

### 给Cursor的标准提问模板
```
我在开发Unity 2022教育游戏《消失的硬币》第四章博物馆场景。
[描述具体问题或需求]
相关脚本是Chapter4Manager.cs，字体用MSYH SDF，UI图片在Assets/UI/。
```

### 报错处理
- 报错先截图发给 Claude Web（宏观分析）
- 具体代码修改在 Cursor 里做

---

## 毕业论文要点（备忘）
- 主题：基于Unity的光学物理教育游戏设计与实现
- 核心技术点：光线折射/全反射模拟、对话系统、场景管理
- 教学目标：通过游戏化方式理解光学全反射现象
- 创新点：AI辅助开发流程（Claude+Cursor协作）
