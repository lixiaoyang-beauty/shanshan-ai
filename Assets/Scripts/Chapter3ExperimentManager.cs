using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

public class Chapter3ExperimentManager : MonoBehaviour
{
    [Header("── 对话框 ──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 闪闪面板 ──")]
    public GameObject shanShanPanel;
    public TextMeshProUGUI shanShanText;
    public Button shanShanAskBtn;
    public GameObject shanShanInputPanel;
    public TMP_InputField shanShanInput;
    public Button shanShanSendBtn;
    public Button shanShanCloseBtn;

    [Header("── Slider ──")]
    public Slider angleSlider;

    [Header("── 闪闪服务 ──")]
    public string shanShanServerUrl = "https://shanshan-ai-production.up.railway.app";
    public string sessionId = "player1";

    [Header("── 头像 ──")]
    public Sprite allyAvatar;
    public Sprite conanAvatar;

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    [Header("── 折射率 ──")]
    public float n1 = 1.33f;
    public float n2 = 1.00f;

    [Header("── 学习追踪 ──")]
    public LearningTracker learningTracker;

    // 实验区域
    private RectTransform experimentArea;
    private Image incidentLine, reflectedLine, refractedLine;
    private Image[] normalDashes;
    private TextMeshProUGUI incidentAngleLabel, reflectAngleLabel, refractAngleLabel;
    private bool laserOn = false;

    // 物理
    private float currentIncidentAngle;
    private float currentRefractAngle;
    private bool  isTotalReflection;
    private float criticalAngle;

    private bool analysisMode = false;

    // 状态机
    private int  stage = 0;
    private bool laserActivated   = false;
    private bool stage2Triggered  = false;
    private bool stage3Triggered  = false;
    private bool stage4Triggered  = false;
    private bool totalReflFound   = false;
    private bool verifyDone       = false;
    private bool predictionMade        = false;
    private bool refractionRuleAnswered = false;  // 折射规律答完才触发临界角题

    // 预设问题ID（用于发送给/chat评判）
    private string currentQuestionId = "";

    // 每道题的错题次数
    private Dictionary<string, int> questionWrongCount = new Dictionary<string, int>();

    // 星星
    private Image[] starImages = new Image[3];
    private int starsEarned = 0;

    // 提示等级
    private float idleTimer = 0f;
    private int   hintLevel = -1;
    private float lastAngle = 15f;

    // 闪光
    private Image flashOverlay;

    // 选项气泡
    private GameObject bubbleContainer;
    private bool waitingForChoice = false;  // 等待玩家选择时暂停提示

    // 独立按钮（继续探索/继续按钮/go按钮等）
    private List<GameObject> extraButtons = new List<GameObject>();

    // Slider锁定
    private bool sliderLocked = false;

    // 跳转按钮引用
    private GameObject goBtn;

    // 对话
    private bool isInDialogue = false;
    private string[] currentLines;
    private int lineIndex;
    private System.Action lineCallback;
    private Coroutine typingCoroutine;
    private bool typingDone = false;
    private string fullText = "";

    // 闪闪
    private bool shanShanBusy = false;

    // 颜色
    static readonly Color NAVY   = new Color(0.04f,0.08f,0.21f,1f);
    static readonly Color NAVY2  = new Color(0.07f,0.13f,0.28f,1f);
    static readonly Color CYAN   = new Color(0f,0.82f,1f,1f);
    static readonly Color CREAM  = new Color(0.96f,0.93f,0.82f,1f);
    static readonly Color DARK   = new Color(0.08f,0.05f,0.01f,1f);
    static readonly Color GOLD   = new Color(0.72f,0.53f,0.04f,1f);
    static readonly Color GOLDB  = new Color(0.72f,0.53f,0.04f,0.4f);
    static readonly Color GREEN  = new Color(0.15f,0.75f,0.35f,1f);
    static readonly Color LASER  = new Color(1f,0.15f,0.15f,1f);
    static readonly Color LASERW = new Color(1f,0.4f,0.4f,0.3f);
    static readonly Color WATERBG= new Color(0.05f,0.18f,0.42f,0.55f);
    static readonly Color WLINE  = new Color(0.2f,0.7f,1f,0.9f);
    static readonly Color NORMALC= new Color(0.8f,0.8f,0.8f,0.5f);

    // ══════════════════════════════════════════
    void Start()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        Hide(dialoguePanel);
        Hide(shanShanPanel);
        Hide(shanShanInputPanel);

        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }
        if (shanShanSendBtn != null)
        { shanShanSendBtn.onClick.RemoveAllListeners(); shanShanSendBtn.onClick.AddListener(OnShanShanSend); }
        if (shanShanInput != null)
            shanShanInput.onSubmit.AddListener((_) => OnShanShanSend());
        if (shanShanAskBtn != null)
        { shanShanAskBtn.onClick.RemoveAllListeners(); shanShanAskBtn.onClick.AddListener(OnAskBtnClicked); }
        if (shanShanCloseBtn != null)
        { shanShanCloseBtn.onClick.RemoveAllListeners(); shanShanCloseBtn.onClick.AddListener(OnCloseBtnClicked); }

        AudioManager.AddClickSound(continueButton);
        AudioManager.AddClickSound(shanShanSendBtn);
        AudioManager.AddClickSound(shanShanAskBtn);
        AudioManager.AddClickSound(shanShanCloseBtn);

        if (angleSlider != null)
        {
            angleSlider.interactable = false;
            angleSlider.minValue = 5f;
            angleSlider.maxValue = 85f;
            angleSlider.value    = 15f;
            angleSlider.onValueChanged.RemoveAllListeners();
            angleSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        criticalAngle = Mathf.Asin(n2/n1) * Mathf.Rad2Deg;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        BuildExperimentArea();
        BuildFlashOverlay();
        BuildStarDisplay();
        UpdateRayLines(15f);
        HideAllRays();

        // 自动查找 LearningTracker（未在 Inspector 连线时）
        if (learningTracker == null)
            learningTracker = FindObjectOfType<LearningTracker>();

        Debug.Log("=== Chapter3ExperimentManager Start === learningTracker=" + (learningTracker != null));

        StartCoroutine(StartChapter());
    }

    // ══════════════════════════════════════════
    void Update()
    {
        if (isInDialogue && Input.GetKeyDown(KeyCode.Return)) OnContinue();
        if (Input.GetKeyDown(KeyCode.Tab) && stage >= 2) ToggleAnalysisMode();

        if (stage >= 2 && stage <= 6 && laserOn)
        {
            float cur = angleSlider != null ? angleSlider.value : 15f;
            if (Mathf.Abs(cur - lastAngle) > 0.5f)
            {
                idleTimer = 0f; hintLevel = -1; lastAngle = cur;
                CheckStageProgression(cur);  // 每当slider值变化就检查阶段推进
            }
            // 等待玩家选择时不触发提示
            else if (!isInDialogue && !shanShanBusy && !waitingForChoice)
            {
                idleTimer += Time.deltaTime;
                if      (idleTimer > 90f && hintLevel < 2) { hintLevel = 2; GiveHint(2); }
                else if (idleTimer > 60f && hintLevel < 1) { hintLevel = 1; GiveHint(1); }
                else if (idleTimer > 30f && hintLevel < 0) { hintLevel = 0; GiveHint(0); }
            }
        }
    }

    // 检查角度阈值，推进阶段
    void CheckStageProgression(float value)
    {
        Debug.Log($"[CheckStageProgression] stage={stage} val={value} s2T={stage2Triggered} s3T={stage3Triggered} isTR={isTotalReflection}");

        // 预测挑战
        if (stage == 2 && !stage2Triggered && value > 20f && !isTotalReflection)
        {
            Debug.Log("[CheckStageProgression] 触发预测挑战！");
            stage2Triggered = true;
            LockSlider();
            StartCoroutine(DelayDo(1f, ShowPredictionChallenge));
            return;
        }

        // 折射规律
        if (stage == 3 && !stage3Triggered && value > 38f && !isTotalReflection)
        {
            Debug.Log("[CheckStageProgression] 触发折射规律题！");
            stage3Triggered = true;
            stage = 4;
            idleTimer = 0f; hintLevel = -1;
            LockSlider();
            StartCoroutine(DelayDo(0.8f, () => {
                currentQuestionId = "q_refraction_rule";
                StartCoroutine(ShanShanAsk("玩家正在探索折射规律，问他入射角变大时折射角怎么变", () => {
                    ShowChoiceBubble(
                        new[]{ "折射角会变大", "折射角会变小", "折射角不变" },
                        new System.Action[]{
                            () => OnRefractionRuleAnswer(true),
                            () => OnRefractionRuleAnswer(false),
                            () => OnRefractionRuleAnswer(false)
                        });
                }));
            }));
            return;
        }

        // 临界角
        if (stage == 4 && !stage4Triggered && value > 45f && !isTotalReflection && refractionRuleAnswered)
        {
            Debug.Log("[CheckStageProgression] 触发临界角题！");
            stage4Triggered = true;
            LockSlider();
            StartCoroutine(DelayDo(0.8f, () => {
                currentQuestionId = "q_critical_angle";
                StartCoroutine(ShanShanAsk("玩家接近临界角，折射光很弱，问他折射角=90度时的入射角叫什么", () => {
                    ShowChoiceBubble(
                        new[]{ "临界角", "折射角", "入射角", "反射角" },
                        new System.Action[]{
                            () => OnCriticalAngleAnswer(true),
                            () => OnCriticalAngleAnswer(false),
                            () => OnCriticalAngleAnswer(false),
                            () => OnCriticalAngleAnswer(false)
                        });
                }));
            }));
            return;
        }

        // 全反射
        if (stage >= 3 && isTotalReflection && !totalReflFound)
        {
            Debug.Log("[CheckStageProgression] 触发全反射！");
            OnTotalReflectionFirst();
        }
    }

    // ══════════════════════════════════════════
    // 提示等级（调用 /hint 接口，MiniMax 生成简短提示语）
    // ══════════════════════════════════════════
    void GiveHint(int level)
    {
        string context = $"玩家在阶段{stage}，hint等级{level}，当前角度{currentIncidentAngle:.1f}度，是否全反射={isTotalReflection}，生成一句简短提示（15字以内），不要选项，像朋友聊天一样。";
        StartCoroutine(CallShanShanHint(context));
    }
    // ══════════════════════════════════════════
    // 学习轨迹记录（Feature 3：调用 /learning-data 追踪玩家学习状态）
    // 答错时调用：发送错题详情 → MiniMax分析 → 返回苏格拉底追问
    // ══════════════════════════════════════════
    void RecordWrongAnswer(string qid, string selectedOpt, string correctOpt)
    {
        // 记录迷思概念计数（用于学习报告）
        if (!questionWrongCount.ContainsKey(qid)) questionWrongCount[qid] = 0;
        questionWrongCount[qid]++;
        StartCoroutine(DoRecordWrongAnswer(qid, selectedOpt, correctOpt));
    }

    IEnumerator DoRecordWrongAnswer(string qid, string selectedOpt, string correctOpt)
    {
        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question_id\":\"" + qid + "\"," +
            "\"selected_option\":\"" + EscapeJson(selectedOpt) + "\"," +
            "\"correct_answer\":\"" + EscapeJson(correctOpt) + "\"," +
            "\"wrong_count\":" + wrongAttempts + "," +
            "\"exploration_stage\":" + stage + "," +
            "\"current_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"idle_time\":" + idleTimer.ToString("F1") + "}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/learning-data", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        string socraticQuestion = null;

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            int sqIdx = json.IndexOf("\"socratic_question\":\"");
            if (sqIdx >= 0)
            {
                int s = sqIdx + 20;
                int e = json.IndexOf("\"", s);
                if (e > s)
                {
                    string raw = json.Substring(s, e - s);
                    if (!string.IsNullOrEmpty(raw) && raw != "null")
                        socraticQuestion = raw;
                }
            }
        }

        // 等主预设反馈显示完（2.5秒）后，叠加苏格拉底追问
        yield return new WaitForSeconds(2.5f);
        string display = !string.IsNullOrEmpty(socraticQuestion)
            ? "💡 " + socraticQuestion
            : "💡 " + GetSocraticFallback(qid);
        ShanShanSayLocal(display, true);
    }

    // 苏格拉底追问后备库（MiniMax不可用时使用）
    string GetSocraticFallback(string qid)
    {
        switch (qid)
        {
            case "q_line_count":       return "想想看，光线碰到水面时会怎样？";
            case "q_refraction_rule":  return "入射角变大时，折射角往哪边偏？";
            case "q_critical_angle":  return "折射角=90度时，这个入射角叫什么？";
            case "q_prediction":      return "折射光是不是越来越暗了？";
            case "q_total_reflection": return "反射光变亮了！光是不是跑回去了？";
            case "q_verify":           return "回忆一下发现卡片里的两个条件？";
            case "q_coin":             return "从侧面看时，光线要经过哪个介质出去？";
            default:                   return "再仔细观察一下实验台！";
        }
    }

    // 章节结束时获取迷思概念摘要并展示面板
    void ShowLearningSummary()
    {
        DoShowLearningSummary();
    }

    void DoShowLearningSummary()
    {
        // 基于本地 questionWrongCount 统计（不依赖后端）
        int totalWrongLocal = 0;
        foreach (var v in questionWrongCount.Values) totalWrongLocal += v;

        string body;
        if (totalWrongLocal == 0)
        {
            body = "太棒了！本次学习没有遇到迷思概念，所有概念都掌握得很好！";
        }
        else
        {
            body = $"本次学习共答错 {totalWrongLocal} 道题，以下是你可能需要加强的概念：\n";
            if (questionWrongCount.ContainsKey("q_line_count") && questionWrongCount["q_line_count"] > 0)
                body += $"  光线分解：误以为只有1条或2条光线（错{questionWrongCount["q_line_count"]}次）\n";
            if (questionWrongCount.ContainsKey("q_refraction_rule") && questionWrongCount["q_refraction_rule"] > 0)
                body += $"  折射规律：误以为入射角越大折射角越小（错{questionWrongCount["q_refraction_rule"]}次）\n";
            if (questionWrongCount.ContainsKey("q_critical_angle") && questionWrongCount["q_critical_angle"] > 0)
                body += $"  临界角：对临界角概念不清晰（错{questionWrongCount["q_critical_angle"]}次）\n";
            if (questionWrongCount.ContainsKey("q_total_reflection") && questionWrongCount["q_total_reflection"] > 0)
                body += $"  全反射：误以为光消失了或被吸收（错{questionWrongCount["q_total_reflection"]}次）\n";
            if (questionWrongCount.ContainsKey("q_verify") && questionWrongCount["q_verify"] > 0)
                body += $"  全反射条件：对两个条件缺一不可（错{questionWrongCount["q_verify"]}次）\n";
            if (questionWrongCount.ContainsKey("q_coin") && questionWrongCount["q_coin"] > 0)
                body += $"  古币消失：对侧面角度与全反射关系不清晰（错{questionWrongCount["q_coin"]}次）\n";
        }

        ShowSummaryPanel(totalWrongLocal, body);
    }

    void ShowSummaryPanel(int totalWrong, string body)
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("SummaryOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        var panel = new GameObject("SummaryPanel");
        panel.transform.SetParent(overlay.transform, false);
        var pRt = panel.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.15f, 0.15f);
        pRt.anchorMax = new Vector2(0.85f, 0.85f);
        pRt.offsetMin = pRt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = NAVY2;
        MakeBorder(panel, CYAN, 2f);

        // 标题
        MakeTMP("Title", panel.transform,
            V2(0f, 0.82f), V2(1f, 0.95f), V2(20, 5), V2(-20, -5),
            "学习报告", 28, CYAN, TextAlignmentOptions.Center, true);

        MakeImg("Div", panel.transform,
            V2(0.02f, 0.80f), V2(0.98f, 0.82f), V2(0, 0), V2(0, 0),
            new Color(0f, 0.82f, 1f, 0.4f));

        // 错题数
        string statText = totalWrong == 0
            ? "本题全对！"
            : $"本次共答错 {totalWrong} 次，以下是你可能需要加强的概念：";
        MakeTMP("Stat", panel.transform,
            V2(0f, 0.72f), V2(1f, 0.80f), V2(20, 5), V2(-20, -5),
            statText, 18, new Color(1f, 0.85f, 0.3f, 1f), TextAlignmentOptions.Center, false);

        // 迷思概念正文
        MakeTMP("Body", panel.transform,
            V2(0f, 0.10f), V2(1f, 0.72f), V2(20, 8), V2(-20, -8),
            body, 20, CREAM, TextAlignmentOptions.Top, false);

        // 关闭按钮
        MakeActionButton("继续回博物馆破案！", GOLD,
            () => { Destroy(overlay); StartAllyEnding(); },
            V2(0.2f, 0.02f), V2(0.8f, 0.09f), panel.transform);

        StartCoroutine(PopIn(pRt));
    }

    float lastAngleSampleTime = 0f;
    float lastSampledAngle = 15f;

    IEnumerator CallShanShanHint(string hintContext)
    {
        if (shanShanBusy) yield break;
        shanShanBusy = true;

        string body =
            "{\"hint_context\":\"" + EscapeJson(hintContext) + "\"," +
            "\"exploration_stage\":" + stage + "}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/hint", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        shanShanBusy = false;

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            int idx = json.IndexOf("\"hint_text\":\"");
            if (idx >= 0)
            {
                int s = idx + 13;
                int e = json.IndexOf("\"", s);
                if (e > s)
                {
                    string hintText = json.Substring(s, e - s);
                    ShanShanSayLocal(hintText, true);
                    yield break;
                }
            }
        }
        ShanShanSayLocal("试试调节角度，观察光线有什么变化！", true);
    }

    // ══════════════════════════════════════════
    // Stage 0：艾莉开场
    // ══════════════════════════════════════════
    IEnumerator StartChapter()
    {
        yield return new WaitForSeconds(0.6f);
        ShowDialogue(new[]
        {
            "艾莉|光路追踪眼镜已激活！你现在能看见光线的路径了！",
            "艾莉|实验台显示了两种介质：上面是空气（n=1.0），下面是水（n=1.33）。",
            "艾莉|n就是折射率，折射率越大光走得越慢！",
            "艾莉|我给你配了AI助理闪闪，它会引导你探索！",
            "艾莉|开始探索吧！"
        }, OnIntroFinished);
    }

    void OnIntroFinished()
    {
        stage = 1;
        Show(shanShanPanel);
        LearningTracker.learningTrackerEnabled = true;  // 开启学习追踪
        StartCoroutine(ClearSession());
        // 直接显示欢迎语，不需要调API
        ShanShanSayLocal("嗨柯南！我是闪闪！实验台已经就位啦，点一下那个按钮开启光线，我们一起看看会发生什么！", true);
    }

    // ══════════════════════════════════════════
    // 开启激光按钮（实验区域内）
    // ══════════════════════════════════════════
    public void OnLaserButtonClicked()
    {
        AudioManager.PlayClick();
        if (laserActivated) return;
        laserOn = true;
        laserActivated = true;
        ShowAllRays();
        UnlockSlider();
        stage = 2;
        idleTimer = 0f; hintLevel = -1;

        StartCoroutine(DelayDo(0.8f, () => {
            currentQuestionId = "q_line_count";
            wrongAttempts = 0;
            StartCoroutine(ShanShanAsk("哇！光线出来了！你能看到几条光线？", () => {
                ShowChoiceBubble(
                    new[]{ "1条", "2条", "3条" },
                    new System.Action[]{
                        () => OnLineCountAnswer(1),
                        () => OnLineCountAnswer(2),
                        () => OnLineCountAnswer(3)
                    });
            }));
        }));
    }

    // ══════════════════════════════════════════
    // Slider
    // ══════════════════════════════════════════
    void OnSliderChanged(float value)
    {
        UpdateRayLines(value);
        RecordAngleSample();
    }

    void RecordAngleSample()
    {
        float now = Time.time;
        if (now - lastAngleSampleTime > 1f && laserOn)
        {
            lastAngleSampleTime = now;
            lastSampledAngle = currentIncidentAngle;
        }
    }

    // ══════════════════════════════════════════
    // 答题回调
    // ══════════════════════════════════════════
    void OnLineCountAnswer(int count)
    {
        ClearBubbles();
        if (count == 3)
        {
            wrongAttempts = 0;
            EarnStar(0);
            AudioManager.PlayCorrect();
            ShanShanSayLocal(GetCorrectFeedback("q_line_count"), true);
        }
        else
        {
            AudioManager.PlayWrong();
            wrongAttempts++;
            learningTracker?.OnAnswerRecorded("line_count");
            string selectedOpt = count == 1 ? "1条" : (count == 2 ? "2条" : "3条");
            RecordWrongAnswer("q_line_count", selectedOpt, "3条");
            if (wrongAttempts >= 3)
            {
                ShanShanSayLocal(GetLectureText("q_line_count"), true);
                wrongAttempts = 0;
                StartCoroutine(DelayDo(2f, ShowLectureContinueButton));
            }
            else
            {
                ShanShanSayLocal(GetWrongHint("q_line_count", wrongAttempts), true);
                StartCoroutine(DelayDo(2.2f, () => ShowChoiceBubble(
                    new[]{ "1条", "2条", "3条" },
                    new System.Action[]{
                        () => OnLineCountAnswer(1),
                        () => OnLineCountAnswer(2),
                        () => OnLineCountAnswer(3)
                    })));
            }
        }
    }

    void OnRefractionRuleAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            wrongAttempts = 0;
            refractionRuleAnswered = true;
            UnlockSlider();
            AudioManager.PlayCorrect();
            ShanShanSayLocal(GetCorrectFeedback("q_refraction_rule"), true);
        }
        else
        {
            AudioManager.PlayWrong();
            wrongAttempts++;
            learningTracker?.OnAnswerRecorded("refraction_rule");
            string wrongOpt = "折射角会变小"; // 迷思概念映射的key
            RecordWrongAnswer("q_refraction_rule", wrongOpt, "折射角会变大");
            if (wrongAttempts >= 3)
            {
                ShanShanSayLocal(GetLectureText("q_refraction_rule"), true);
                wrongAttempts = 0;
                StartCoroutine(DelayDo(2f, ShowLectureContinueButton));
            }
            else
            {
                ShanShanSayLocal(GetWrongHint("q_refraction_rule", wrongAttempts), true);
                StartCoroutine(DelayDo(2.2f, () => {
                    currentQuestionId = "q_refraction_rule";
                    StartCoroutine(ShanShanAsk("再次引导玩家观察：入射角变大时，折射角怎么变？", () => {
                        ShowChoiceBubble(
                            new[]{ "折射角会变大", "折射角会变小", "折射角不变" },
                            new System.Action[]{
                                () => OnRefractionRuleAnswer(true),
                                () => OnRefractionRuleAnswer(false),
                                () => OnRefractionRuleAnswer(false)
                            });
                    }));
                }));
            }
        }
    }

    void OnCriticalAngleAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            wrongAttempts = 0;
            UnlockSlider();
            AudioManager.PlayCorrect();
            ShanShanSayLocal(GetCorrectFeedback("q_critical_angle"), true);
        }
        else
        {
            AudioManager.PlayWrong();
            wrongAttempts++;
            learningTracker?.OnAnswerRecorded("critical_angle");
            RecordWrongAnswer("q_critical_angle", "折射角", "临界角");
            if (wrongAttempts >= 3)
            {
                ShanShanSayLocal(GetLectureText("q_critical_angle"), true);
                wrongAttempts = 0;
                StartCoroutine(DelayDo(2f, ShowLectureContinueButton));
            }
            else
            {
                ShanShanSayLocal(GetWrongHint("q_critical_angle", wrongAttempts), true);
                StartCoroutine(DelayDo(2.2f, () => {
                    currentQuestionId = "q_critical_angle";
                    StartCoroutine(ShanShanAsk("再次引导玩家：折射角等于90度时的那个入射角叫什么？", () => {
                        ShowChoiceBubble(
                            new[]{ "临界角", "折射角", "入射角", "反射角" },
                            new System.Action[]{
                                () => OnCriticalAngleAnswer(true),
                                () => OnCriticalAngleAnswer(false),
                                () => OnCriticalAngleAnswer(false),
                                () => OnCriticalAngleAnswer(false)
                            });
                    }));
                }));
            }
        }
    }

    // ══════════════════════════════════════════
    // 猜测挑战
    // ══════════════════════════════════════════
    void ShowPredictionChallenge()
    {
        if (predictionMade) return;
        predictionMade = true;
        wrongAttempts = 0;
        currentQuestionId = "q_prediction";
        StartCoroutine(ShanShanAsk("折射光越来越弱了...", () => {
            ShowChoiceBubble(
                new[]{ "变得更强", "逐渐消失", "方向不变" },
                new System.Action[]{
                    () => OnPredictionSelected(0),
                    () => OnPredictionSelected(1),
                    () => OnPredictionSelected(2)
                });
        }));
    }

    void OnPredictionSelected(int idx)
    {
        string[] predictionOptions = new[]{ "变得更强", "逐渐消失", "方向不变" };
        string selected = idx >= 0 && idx < predictionOptions.Length ? predictionOptions[idx] : "";
        bool correct = (idx == 1);
        OnPrediction(correct, selected);
    }

    void OnPrediction(bool correct, string reply)
    {
        ClearBubbles();
        stage = 3;
        idleTimer = 0f; hintLevel = -1;
        // 无论对错，都继续，给统一引导语+继续按钮
        ShanShanSayLocal("好有趣！我们一起来看看后面会发生什么！", true);
        StartCoroutine(DelayDo(1f, () => ShowPredictionContinueButton()));
    }

    void ShowPredictionContinueButton()
    {
        // 继续探索按钮：推进阶段到3，解锁slider
        var btn = MakeActionButton("继续探索", CYAN,
            () => { stage = 3; idleTimer = 0f; hintLevel = -1; UnlockSlider(); ClearBubbles(); },
            V2(0.3f, 0.02f), V2(0.7f, 0.12f));
        if (btn != null) extraButtons.Add(btn);
    }

    // ══════════════════════════════════════════
    // 全反射
    // ══════════════════════════════════════════
    void OnTotalReflectionFirst()
    {
        if (totalReflFound) return;
        totalReflFound = true;
        stage = 5;
        idleTimer = 0f; hintLevel = -1;
        ClearBubbles();
        LockSlider();  // 全反射触发时锁定
        AudioManager.PlayLaser();
        StartCoroutine(FlashScreen(new Color(1f,0.3f,0.1f,0.4f)));
        StartCoroutine(TotalReflectionSequence());
    }

    IEnumerator TotalReflectionSequence()
    {
        yield return new WaitForSeconds(0.5f);
        currentQuestionId = "q_total_reflection";
        wrongAttempts = 0;
        StartCoroutine(ShanShanAsk("折射光突然消失了！用非常惊讶的语气感叹这个现象，然后问玩家光去哪了。", () => {
            ShowChoiceBubble(
                new[]{ "光消失了", "光全部反射回水中", "光被水吸收了" },
                new System.Action[]{
                    () => OnTotalReflAnswer(false),
                    () => OnTotalReflAnswer(true),
                    () => OnTotalReflAnswer(false)
                });
        }));
    }

    void OnTotalReflAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            wrongAttempts = 0;
            AudioManager.PlayCorrect();
            ShanShanSayLocal(GetCorrectFeedback("q_total_reflection"), true);
            UnlockSlider();
            StartCoroutine(DelayDo(1.8f, ShowDiscoveryCard));
        }
        else
        {
            AudioManager.PlayWrong();
            wrongAttempts++;
            learningTracker?.OnAnswerRecorded("total_reflection");
            RecordWrongAnswer("q_total_reflection", "光被水吸收了", "光全部反射回水中");
            if (wrongAttempts >= 3)
            {
                ShanShanSayLocal(GetLectureText("q_total_reflection"), true);
                wrongAttempts = 0;
                StartCoroutine(DelayDo(2.5f, ShowDiscoveryCard));
            }
            else
            {
                ShanShanSayLocal(GetWrongHint("q_total_reflection", wrongAttempts), true);
                StartCoroutine(DelayDo(2.2f, ShowTotalReflRetryBubble));
            }
        }
    }

    void ShowTotalReflRetryBubble()
    {
        ShowChoiceBubble(
            new[]{ "光消失了", "光全部反射回水中", "光被水吸收了" },
            new System.Action[]{
                () => OnTotalReflAnswer(false),
                () => OnTotalReflAnswer(true),
                () => OnTotalReflAnswer(false)
            });
    }

    // ══════════════════════════════════════════
    // 发现卡片
    // ══════════════════════════════════════════
    void ShowDiscoveryCard()
    {
        EarnStar(1);
        StartCoroutine(FlashScreen(new Color(0f,0.8f,1f,0.3f)));

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("DiscoveryOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0,0,0,0.65f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRt = card.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.10f,0.10f);
        cRt.anchorMax = new Vector2(0.90f,0.92f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = NAVY;
        MakeBorder(card, CYAN, 3f);

        MakeTMP("Title", card.transform,
            V2(0f,0.87f),V2(1f,1f),V2(20,5),V2(-20,-5),
            "重大发现！全反射！",28,CYAN,TextAlignmentOptions.Center,true);
        MakeImg("Div",card.transform,
            V2(0.02f,0.865f),V2(0.98f,0.868f),V2(0,0),V2(0,0),
            new Color(0f,0.82f,1f,0.4f));
        MakeTMP("Body",card.transform,
            V2(0f,0.35f),V2(1f,0.865f),V2(20,8),V2(-20,-8),
            "全反射现象：\n折射光完全消失，所有光反射回水中！\n\n发生全反射的两个必要条件：\n\n条件1：光从光密介质射向光疏介质\n（折射率大到折射率小，如水到空气）\n\n条件2：入射角大于或等于临界角\n（水的临界角约为48度）",
            22,CREAM,TextAlignmentOptions.Center,false);
        MakeTMP("Sub",card.transform,
            V2(0f,0.20f),V2(1f,0.35f),V2(20,0),V2(-20,0),
            "这就是古币消失的秘密所在！",
            18,new Color(1f,0.85f,0.3f,1f),TextAlignmentOptions.Center,false);

        MakeActionButton("我明白了！",CYAN,
            () => { Destroy(overlay); stage = 6; StartCoroutine(DelayDo(0.5f, ShowVerifyPanel)); },
            V2(0.2f,0.04f),V2(0.8f,0.14f),card.transform);

        StartCoroutine(PopIn(cRt));
    }

    // ══════════════════════════════════════════
    // 验证题
    // ══════════════════════════════════════════
    void ShowVerifyPanel()
    {
        currentQuestionId = "q_verify";
        StartCoroutine(ShanShanAsk("进入全反射条件选择题，让玩家选出正确的全反射条件", () => {
            ShowChoiceBubble(
                new[]{
                    "光从水射向空气，入射角>=临界角",
                    "光从空气射向水，角度越大越好",
                    "只要角度够大就会全反射"
                },
                new System.Action[]{
                    () => OnVerify(true),
                    () => OnVerify(false),
                    () => OnVerify(false)
                });
        }));
    }

    void OnVerify(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            verifyDone = true;
            EarnStar(2);
            AudioManager.PlayCorrect();
            StartCoroutine(FlashScreen(new Color(0.2f,1f,0.3f,0.3f)));
            StartCoroutine(ShanShanAsk("玩家答对了全反射条件，联系古币案件，问他从侧面看时入射角大还是小", () => {
                ShowChoiceBubble(
                    new[]{ "大于临界角", "小于临界角" },
                    new System.Action[]{
                        () => OnCoinAnswer(true),
                        () => OnCoinAnswer(false)
                    });
            }));
        }
        else
        {
            AudioManager.PlayWrong();
            RecordWrongAnswer("q_verify", "只要角度够大就会全反射", "光从水射向空气，入射角>=临界角");
            StartCoroutine(ShanShanAsk("玩家答错了全反射条件，引导他记住两个条件：光从水到空气且入射角>=临界角"));
            learningTracker?.OnAnswerRecorded("verify_condition");
            StartCoroutine(DelayDo(2f, ShowVerifyPanel));
        }
    }

    void OnCoinAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            StartCoroutine(ShanShanAsk("玩家答对了古币问题，解释为什么从侧面看不到古币，引导回博物馆"));
            StartCoroutine(DelayDo(1.5f, ShowGoButton));
        }
        else
        {
            RecordWrongAnswer("q_coin", "小于临界角", "大于临界角");
            learningTracker?.OnAnswerRecorded("coin_angle");
            // 答错了，给第二次选择机会
            StartCoroutine(DelayDo(1.5f, ShowCoinRetryBubble));
        }
    }

    void ShowCoinRetryBubble()
    {
        currentQuestionId = "q_coin";
        StartCoroutine(ShanShanAsk("问玩家从侧面观察古币时，入射角大于还是小于临界角", () => {
            ShowChoiceBubble(
                new[]{ "大于临界角", "小于临界角" },
                new System.Action[]{
                    () => OnCoinAnswerSecond(true),
                    () => OnCoinAnswerSecond(false)
                });
        }));
    }

    void OnCoinAnswerSecond(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            RecordWrongAnswer("q_coin", "小于临界角", "大于临界角");
            StartCoroutine(ShanShanAsk("玩家答对了，解释古币消失原因并引导回博物馆"));
            StartCoroutine(DelayDo(1.5f, ShowGoButton));
        }
        else
        {
            RecordWrongAnswer("q_coin", "大于临界角", "大于临界角");
            StartCoroutine(ShanShanAsk("玩家答错了，解释从侧面看角度大导致全反射，引导回博物馆"));
            StartCoroutine(DelayDo(1.5f, ShowGoButton));
        }
    }

    void ShowGoButton()
    {
        goBtn = MakeActionButton("已明白原理，回博物馆破案！",GOLD,
            () => {
                if (goBtn != null) { Destroy(goBtn); goBtn = null; }
                ShowLearningSummary();  // 先展示学习报告
            },
            V2(0.2f,0.02f),V2(0.8f,0.11f));
    }

    // ══════════════════════════════════════════
    // 艾莉结尾
    // ══════════════════════════════════════════
    void StartAllyEnding()
    {
        stage = 7;

        // 清理所有UI
        Hide(shanShanPanel);
        Hide(shanShanInputPanel);
        ClearBubbles();
        if (angleSlider != null) angleSlider.interactable = false;

        // 清理实验区域
        var expArea = GameObject.Find("ExperimentArea");
        if (expArea != null) expArea.SetActive(false);

        // 清理星星栏
        var starBar = GameObject.Find("StarBar");
        if (starBar != null) starBar.SetActive(false);

        // 清理Slider
        if (angleSlider != null) angleSlider.gameObject.SetActive(false);

        // 清理发现卡片/验证面板（如果还在）
        var discovery = GameObject.Find("DiscoveryOverlay");
        if (discovery != null) Destroy(discovery);

        ShowDialogue(new[]
        {
            "艾莉|太棒了！你已经完全掌握了全反射原理！",
            "艾莉|从侧面看时角度超过临界角，光全反射出不来——古币消失！",
            "我|从上方俯视时角度小，光折射出来，古币重现！",
            "艾莉|完全正确！快回博物馆把真相告诉馆长！",
            "我|走！"
        }, () => SceneTransitionManager.LoadScene("Chapter4_Museum"));
    }

    // ══════════════════════════════════════════
    // 选项气泡（横向居中，底部）
    // ══════════════════════════════════════════
    void ShowChoiceBubble(string[] options, System.Action[] callbacks)
    {
        ClearBubbles();
        waitingForChoice = true;  // 暂停idle提示
        idleTimer = 0f;           // 重置计时
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        bubbleContainer = new GameObject("Bubbles");
        bubbleContainer.transform.SetParent(canvas.transform, false);
        var cRt = bubbleContainer.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.02f,0.02f);
        cRt.anchorMax = new Vector2(0.98f,0.14f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        var bgImg = bubbleContainer.AddComponent<Image>();
        bgImg.color = new Color(0,0,0,0);
        bgImg.raycastTarget = false;  // 透明区域不阻挡点击

        float gap  = 0.01f;
        float btnW = (1f - gap * (options.Length + 1)) / options.Length;

        for (int i = 0; i < options.Length; i++)
        {
            int   idx = i;
            float x   = gap + i * (btnW + gap);

            var go = new GameObject("Opt" + i);
            go.transform.SetParent(bubbleContainer.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(x, 0.05f);
            rt.anchorMax = new Vector2(x + btnW, 0.95f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.06f,0.14f,0.32f,0.95f);
            MakeBorder(go, CYAN, 1.5f);

            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var bc  = btn.colors;
            bc.highlightedColor = new Color(0.1f,0.3f,0.55f,1f);
            bc.pressedColor     = new Color(0f,0.5f,0.8f,1f);
            btn.colors = bc;

            string capturedOption = options[idx];
            System.Action cb = callbacks[idx];
            btn.onClick.AddListener(() => {
                lastSelectedOption = capturedOption;
                ClearBubbles();
                cb?.Invoke();
                AudioManager.PlayClick();
            });

            MakeTMP("T",go.transform,V2(0,0),V2(1,1),V2(4,4),V2(-4,-4),
                options[idx],15,CREAM,TextAlignmentOptions.Center,false);

            StartCoroutine(PopIn(rt));
        }
    }

    void ClearBubbles()
    {
        if (bubbleContainer != null) Destroy(bubbleContainer);
        bubbleContainer = null;
        // 删除所有独立按钮（继续探索/继续/go等）
        foreach (var btn in extraButtons)
        {
            if (btn != null) Destroy(btn);
        }
        extraButtons.Clear();
        waitingForChoice = false;  // 恢复idle提示
        idleTimer = 0f;
        // 强制刷新Canvas，确保Raycast状态立即更新
        Canvas.ForceUpdateCanvases();
    }

    // ══════════════════════════════════════════
    // Tab切换
    // ══════════════════════════════════════════
    void ToggleAnalysisMode()
    {
        analysisMode = !analysisMode;
        ShanShanSayLocal(analysisMode
            ? "临界角约" + criticalAngle.ToString("F0") + "度！现在入射角是" + currentIncidentAngle.ToString("F0") + "度。"
            : "已切换回普通模式。按Tab随时查看角度提示！");
    }

    // ══════════════════════════════════════════
    // 闪闪输入框
    // ══════════════════════════════════════════
    void OnAskBtnClicked()
    {
        AudioManager.PlayClick();
        Show(shanShanInputPanel);
        if (shanShanAskBtn != null) shanShanAskBtn.gameObject.SetActive(false);
        if (shanShanInput != null) shanShanInput.Select();
    }

    void OnCloseBtnClicked()
    {
        Hide(shanShanInputPanel);
        if (shanShanAskBtn != null) shanShanAskBtn.gameObject.SetActive(true);
    }

    void OnShanShanSend()
    {
        if (shanShanBusy || shanShanInput == null) return;
        string q = shanShanInput.text.Trim();
        if (string.IsNullOrEmpty(q)) return;
        shanShanInput.text = "";
        StartCoroutine(AskShanShanFree(q));
    }

    IEnumerator AskShanShanFree(string q)
    {
        ShanShanSayLocal("让我想想……", true);
        yield return CallShanShanApi(q, stage);
        // 如果API失败，CallShanShanApi 会显示"暂时连不上"
        // 不再做任何额外处理，提示已由 CallShanShanApi 显示
    }

    void ShanShanSayLocal(string msg, bool forceShow = false)
    {
        // waitingForChoice=true 时，只有 forceShow=true 才更新文字（API回复等重要时刻）
        if (waitingForChoice && !forceShow) return;
        if (shanShanText != null) { shanShanText.text = msg; ApplyFont(shanShanText); }
        Show(shanShanPanel);
    }

    // ============================================================
    // AI问题+选项系统（完整闭环）
    // ============================================================
    // 当前AI生成的问题和选项（用于发给AI评判）
    private string aiCurrentQuestion = "";
    private string[] aiCurrentOptions = new string[0];
    private int wrongAttempts = 0;  // 本题错了几次

    // 预设问题文本后备库（API不可用时使用）
    string GetPresetQuestionFallback(string qid)
    {
        switch (qid)
        {
            case "q_line_count":       return "哇！你能看到几条光线？";
            case "q_refraction_rule":  return "入射角变大时，折射角怎么变？";
            case "q_critical_angle":   return "折射角=90度时的入射角叫什么？";
            case "q_prediction":      return "继续增大角度，折射光会怎样？";
            case "q_total_reflection": return "哇！折射光消失了！光去哪了？";
            case "q_verify":           return "发生全反射需要哪两个条件？";
            case "q_coin":            return "从侧面观察古币时，入射角是大还是小？";
            default:                   return "仔细观察实验台，选择正确答案！";
        }
    }

    // AI生成问题+选项，并显示
    // 调用后端 /chat 接口，获取预设问题和选项
    IEnumerator ShanShanAsk(string context, System.Action onReplyDone = null)
    {
        string qid = currentQuestionId;
        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question_id\":\"" + qid + "\"," +
            "\"wrong_count\":" + wrongAttempts + "," +
            "\"question\":\"" + context.Replace("\"", "\\\"") + "\"," +
            "\"incident_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"is_total_reflection\":" + (isTotalReflection ? "true" : "false") + "," +
            "\"refract_angle\":" + currentRefractAngle.ToString("F1") + "," +
            "\"exploration_stage\":" + stage + "," +
            "\"selected_option\":null}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/chat", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            Debug.Log("[ShanShanAsk] 收到响应: " + json);

            // 解析 question 字段
            string question = "";
            int qIdx = json.IndexOf("\"question\":\"");
            if (qIdx >= 0) {
                int qStart = qIdx + 12;
                int qEnd = json.IndexOf("\"", qStart);
                if (qEnd > qStart) question = json.Substring(qStart, qEnd - qStart);
            }

            // 解析 options 数组
            string[] options = new string[0];
            int optIdx = json.IndexOf("\"options\":[");
            if (optIdx >= 0) {
                int arrStart = json.IndexOf("[", optIdx);
                int arrEnd = json.IndexOf("]", arrStart);
                if (arrEnd > arrStart) {
                    string arrStr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                    string[] parts = arrStr.Split(new char[] { '"' });
                    var optList = new System.Collections.Generic.List<string>();
                    foreach (var p in parts) {
                        string trimmed = p.Trim().Trim(',', ' ');
                        if (!string.IsNullOrEmpty(trimmed) && trimmed != ",")
                            optList.Add(trimmed);
                    }
                    options = optList.ToArray();
                }
            }

            ShanShanSayLocal(question, true);

            if (options.Length > 0)
            {
                yield return new WaitForSeconds(1f);
                onReplyDone?.Invoke();
            }
            else
            {
                onReplyDone?.Invoke();
            }
            yield break;
        }

        // API 失败时：显示预设后备问题，调用回调（确保选项气泡出现）
        string displayText = GetPresetQuestionFallback(qid);
        ShanShanSayLocal(displayText, true);
        yield return new WaitForSeconds(1f);
        onReplyDone?.Invoke();
    }

    IEnumerator SendAnswerToAI()
    {
        // 等一下让玩家看清选中的效果
        yield return new WaitForSeconds(0.3f);

        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question_id\":\"" + currentQuestionId + "\"," +
            "\"selected_option\":\"" + EscapeJson(lastSelectedOption) + "\"," +
            "\"wrong_count\":" + (wrongAttempts + 1) + "," +
            "\"exploration_stage\":" + stage + "," +
            "\"incident_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"is_total_reflection\":" + (isTotalReflection ? "true" : "false") + "," +
            "\"refract_angle\":" + currentRefractAngle.ToString("F1") + "," +
            "\"question\":\"\"}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/chat", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            ProcessAIAnswerResponse(json);
        }
    }

    string lastSelectedOption = "";

    void ProcessAIAnswerResponse(string json)
    {
        string feedback = "", question = "", nextAction = "";
        bool correct = false;
        string[] options = new string[0];

        try
        {
            int fbIdx = json.IndexOf("\"feedback\":\"");
            if (fbIdx >= 0) {
                int fbStart = fbIdx + 12;
                int fbEnd = json.IndexOf("\"", fbStart);
                feedback = fbEnd > fbStart ? json.Substring(fbStart, fbEnd - fbStart) : "";
            }

            int corrIdx = json.IndexOf("\"correct\":");
            if (corrIdx >= 0) {
                int vStart = json.IndexOfAny(new char[] { 't', 'f' }, corrIdx + 10);
                if (vStart > corrIdx)
                    correct = json.Substring(vStart, 4) == "true";
            }

            int naIdx = json.IndexOf("\"next_action\":\"");
            if (naIdx >= 0) {
                int naStart = naIdx + 15;
                int naEnd = json.IndexOf("\"", naStart);
                nextAction = naEnd > naStart ? json.Substring(naStart, naEnd - naStart) : "";
            }

            int qIdx = json.IndexOf("\"question\":\"");
            if (qIdx >= 0) {
                int qStart = qIdx + 12;
                int qEnd = json.IndexOf("\"", qStart);
                question = qEnd > qStart ? json.Substring(qStart, qEnd - qStart) : "";
            }

            int optIdx = json.IndexOf("\"options\":[");
            if (optIdx >= 0) {
                int arrStart = json.IndexOf("[", optIdx);
                int arrEnd = json.IndexOf("]", arrStart);
                if (arrEnd > arrStart) {
                    string arrStr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                    string[] parts = arrStr.Split(new char[] { '"' });
                    var optList = new System.Collections.Generic.List<string>();
                    foreach (var p in parts) {
                        string trimmed = p.Trim().Trim(',', ' ');
                        if (!string.IsNullOrEmpty(trimmed) && trimmed != ",")
                            optList.Add(trimmed);
                    }
                    options = optList.ToArray();
                }
            }
        }
        catch { }

        // 预测挑战：无论对错都显示衔接语+继续按钮，不评判对错
        if (currentQuestionId == "q_prediction")
        {
            ShanShanSayLocal("好有趣！不管你猜得对不对，我们一起继续看看会发生什么！", true);
            StartCoroutine(DelayDo(1.2f, ShowPredictionContinueButton));
            return;
        }

        lastSelectedOption = "";

        // 答对 → show_discovery_card
        if (nextAction == "show_discovery_card")
        {
            AudioManager.PlayCorrect();
            ShanShanSayLocal(GetCorrectFeedback(currentQuestionId), true);
            UnlockSlider();
            StartCoroutine(DelayDo(1.8f, ShowDiscoveryCard));
            return;
        }

        // 答对 → advance（进入下一阶段）
        if (nextAction == "advance" || (correct && string.IsNullOrEmpty(nextAction)))
        {
            AudioManager.PlayCorrect();
            if (currentQuestionId == "q_refraction_rule")
                refractionRuleAnswered = true;
            ShanShanSayLocal(GetCorrectFeedback(currentQuestionId), true);
            UnlockSlider();
            return;
        }

        // 第三次答错 → lecture（讲解+继续按钮）
        if (nextAction == "lecture")
        {
            AudioManager.PlayWrong();
            wrongAttempts++;
            learningTracker?.OnAnswerRecorded("ai_wrong");
            ShanShanSayLocal(GetLectureText(currentQuestionId), true);
            StartCoroutine(DelayDo(2f, ShowLectureContinueButton));
            return;
        }

        // 答错（第1次或第2次）→ 分层提示 + 重新显示相同选项
        AudioManager.PlayWrong();
        wrongAttempts++;
        learningTracker?.OnAnswerRecorded("ai_wrong");
        ShanShanSayLocal(GetWrongHint(currentQuestionId, wrongAttempts), true);
        if (aiCurrentOptions.Length > 0)
            StartCoroutine(DelayDo(2.2f, () => ShowAIOptionBubble(aiCurrentOptions)));
    }

    string GetCorrectFeedback(string questionId)
    {
        switch (questionId)
        {
            case "q_line_count":     return "太棒了！入射光、反射光、折射光，三条全找到了！继续拖大角度，看看折射光会有什么变化～";
            case "q_refraction_rule":return "就是这样！入射角越大，折射角也越大。继续增大角度，快要发生一件很神奇的事了……";
            case "q_critical_angle": return "答对了！这个特殊角度就叫临界角！把角度调过临界角，看看会发生什么！";
            case "q_total_reflection":return "没错！光全部反射回水里了——这就是全反射！太神奇了！";
            case "q_verify":         return "完全正确！两个条件缺一不可，记住了吗！";
            case "q_coin":           return "对！从侧面看古币，入射角超过临界角，光反射不出来，所以看不见！谜底揭开了！";
            default:                 return "答对了！继续探索吧！";
        }
    }

    string GetWrongHint(string questionId, int attempts)
    {
        switch (questionId)
        {
            case "q_line_count":
                return attempts == 1
                    ? "没关系，再数数看～光射到水面时，会同时向两个方向走哦！"
                    : "提示：入射光 + 反射光 + 折射光，数一数一共几条？";
            case "q_refraction_rule":
                return attempts == 1
                    ? "对比一下角度小时和角度大时，折射光偏向哪边，有没有变化？"
                    : "再观察一下～入射角变大的时候，折射光是离界面更近还是更远？";
            case "q_critical_angle":
                return attempts == 1
                    ? "折射角恰好等于90度时，这个特殊的入射角有个专门的名字哦～"
                    : "临界就是'临界点'——超过这个角，折射光就彻底消失了！";
            case "q_total_reflection":
                return attempts == 1
                    ? "折射光那边没有光了，它去哪了呢？看看反射光那边！"
                    : "反射光是不是变亮了？说明光都跑回水里去了——那光去哪了？";
            case "q_verify":
                return attempts == 1
                    ? "全反射需要两个条件同时满足，想想发现卡片里说的是什么～"
                    : "条件1：光从光密到光疏（水→空气）；条件2：角度要超过临界角！";
            case "q_coin":
                return attempts == 1
                    ? "从侧面看古币时，入射角是比正上方看更大还是更小？"
                    : "侧面就意味着角度倾斜很厉害，入射角一定比正上方大很多～";
            default:
                return attempts == 1 ? "没关系，再想想看！" : "再仔细观察一下实验台～";
        }
    }

    string GetLectureText(string questionId)
    {
        switch (questionId)
        {
            case "q_line_count":      return "光遇到水面会同时反射和折射。入射光 + 反射光 + 折射光，一共3条！记住了吗？继续探索！";
            case "q_refraction_rule": return "光的折射规律：入射角越大，折射角也越大。就像筷子插进水里的样子～继续增大角度看看！";
            case "q_critical_angle":  return "折射角恰好等于90度时，这个入射角叫做临界角。超过临界角，折射光就消失啦！快去看看！";
            case "q_total_reflection":return "折射光消失，所有光都反射回水中——这就是全反射！要发生全反射，入射角必须超过临界角！";
            case "q_verify":          return "全反射条件：①光从水射向空气（光密→光疏）；②入射角大于等于临界角。两个缺一不可！";
            case "q_coin":            return "从侧面看古币，入射角很大，超过临界角，发生全反射，光出不来，所以古币消失了！";
            default:                  return "没关系，继续探索！";
        }
    }

    void ShowAIOptionBubble(string[] opts)
    {
        if (opts.Length == 0) return;
        System.Action[] cbs = new System.Action[opts.Length];
        for (int i = 0; i < cbs.Length; i++) cbs[i] = () => StartCoroutine(SendAnswerToAI());
        ShowChoiceBubble(opts, cbs);
    }

    // AI 消息显示（带回调，用于 LearningTracker 非阻塞队列）
    public void ShowAiMessage(string msg, System.Action onDone)
    {
        if (shanShanText != null) { shanShanText.text = msg; ApplyFont(shanShanText); }
        Show(shanShanPanel);
        StartCoroutine(AiMessageDoneDelay(msg.Length * 0.04f + 0.5f, onDone));
    }

    // 外部查询：当前是否在等待玩家选择选项
    public bool IsWaitingForChoice()
    {
        return waitingForChoice;
    }

    // Slider锁定/解锁
    void LockSlider()
    {
        Debug.Log("[LockSlider] 锁定slider");
        if (angleSlider != null) angleSlider.interactable = false;
        sliderLocked = true;
    }

    void UnlockSlider()
    {
        Debug.Log("[UnlockSlider] 解锁slider");
        if (angleSlider != null) angleSlider.interactable = true;
        sliderLocked = false;
    }

    // 第三次答错后显示继续按钮，点击解锁滑块
    void ShowLectureContinueButton()
    {
        var btn = MakeActionButton("继续探索", CYAN,
            () => { UnlockSlider(); ClearBubbles(); },
            V2(0.3f, 0.02f), V2(0.7f, 0.12f));
        if (btn != null) extraButtons.Add(btn);
    }

    IEnumerator AiMessageDoneDelay(float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);
        onDone?.Invoke();
    }

    // 外部访问接口（LearningTracker 调用）
    public float GetCurrentAngle()
    {
        return currentIncidentAngle;
    }

    public int GetCurrentStage()
    {
        return stage;
    }

    IEnumerator CallShanShanApi(string question, int stageOverride, System.Action onReplyDone = null)
    {
        if (shanShanBusy) yield break;
        shanShanBusy = true;

        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question\":\"" + EscapeJson(question) + "\"," +
            "\"incident_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"is_total_reflection\":" + (isTotalReflection ? "true" : "false") + "," +
            "\"refract_angle\":" + currentRefractAngle.ToString("F1") + "," +
            "\"exploration_stage\":" + stageOverride + "}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/chat", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        shanShanBusy = false;

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            // 新格式：从 question 字段获取回复
            int idx = json.IndexOf("\"question\":\"");
            if (idx >= 0)
            {
                int s = idx + 12;
                int e = json.IndexOf("\"", s);
                if (e > s)
                {
                    string replyText = json.Substring(s, e - s);
                    ShanShanSayLocal(replyText, true);
                    float delay = Mathf.Max(1f, replyText.Length * 0.04f + 0.5f);
                    yield return new WaitForSeconds(delay);
                    onReplyDone?.Invoke();
                    yield break;
                }
            }
        }
        else
            ShanShanSayLocal("暂时连不上，继续观察实验台！");

        onReplyDone?.Invoke();
    }

    IEnumerator ClearSession()
    {
        using var req = new UnityWebRequest(shanShanServerUrl + "/session/" + sessionId, "DELETE");
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();
    }

    // ══════════════════════════════════════════
    // 构建实验区域
    // ══════════════════════════════════════════
    void BuildExperimentArea()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var areaGo = new GameObject("ExperimentArea");
        areaGo.transform.SetParent(canvas.transform, false);
        areaGo.transform.SetSiblingIndex(0);

        experimentArea = areaGo.AddComponent<RectTransform>();
        experimentArea.anchorMin = new Vector2(0.02f,0.42f);
        experimentArea.anchorMax = new Vector2(0.54f,0.92f);
        experimentArea.offsetMin = experimentArea.offsetMax = Vector2.zero;

        var bg = areaGo.AddComponent<Image>();
        bg.color = new Color(0.03f,0.06f,0.15f,0.92f);
        bg.raycastTarget = false;
        MakeBorder(areaGo, new Color(0f,0.6f,0.9f,0.4f), 1.5f);

        // 空气区域
        MakeAreaTMP("空气",    new Vector2(0.14f,0.82f), CYAN, 18f, true);
        MakeAreaTMP("n = 1.0", new Vector2(0.14f,0.72f), new Color(0.6f,0.9f,1f,0.8f), 14f, false);

        // 水体背景
        var wGo = new GameObject("Water");
        wGo.transform.SetParent(experimentArea, false);
        var wRt = wGo.AddComponent<RectTransform>();
        wRt.anchorMin = Vector2.zero; wRt.anchorMax = new Vector2(1f,0.5f);
        wRt.offsetMin = wRt.offsetMax = Vector2.zero;
        var wImg = wGo.AddComponent<Image>(); wImg.color = WATERBG;
        wImg.raycastTarget = false;

        MakeAreaTMP("水",       new Vector2(0.14f,0.30f), new Color(0.5f,0.85f,1f,1f), 18f, true);
        MakeAreaTMP("n = 1.33", new Vector2(0.14f,0.20f), new Color(0.5f,0.85f,1f,0.8f), 14f, false);

        // 水面线
        var sfGo = new GameObject("Surface");
        sfGo.transform.SetParent(experimentArea, false);
        var sfRt = sfGo.AddComponent<RectTransform>();
        sfRt.anchorMin = new Vector2(0f,0.5f); sfRt.anchorMax = new Vector2(1f,0.5f);
        sfRt.offsetMin = new Vector2(0,-1.5f); sfRt.offsetMax = new Vector2(0,1.5f);
        sfGo.AddComponent<Image>().color = WLINE;

        // 法线虚线
        normalDashes = new Image[12];
        for (int i = 0; i < 12; i++)
        {
            var dGo = new GameObject("D"+i);
            dGo.transform.SetParent(experimentArea, false);
            var dRt = dGo.AddComponent<RectTransform>();
            float yBot = 0.04f + i * 0.08f;
            dRt.anchorMin = new Vector2(0.497f,yBot);
            dRt.anchorMax = new Vector2(0.503f,yBot+0.05f);
            dRt.offsetMin = dRt.offsetMax = Vector2.zero;
            normalDashes[i] = dGo.AddComponent<Image>();
            normalDashes[i].color = NORMALC;
            normalDashes[i].raycastTarget = false;
        }

        // 开启入射光线按钮（水中左下，可点击）
        var laserGo = new GameObject("LaserBtn");
        laserGo.transform.SetParent(experimentArea, false);
        var lRt = laserGo.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.03f,0.01f);
        lRt.anchorMax = new Vector2(0.40f,0.13f);
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;
        var lImg = laserGo.AddComponent<Image>();
        lImg.color = new Color(0f,0.55f,0.75f,0.9f);
        MakeBorder(laserGo, CYAN, 1.5f);
        var lBtn = laserGo.AddComponent<Button>(); lBtn.targetGraphic = lImg;
        var lbc  = lBtn.colors;
        lbc.highlightedColor = new Color(0.1f,0.8f,1f,1f);
        lBtn.colors = lbc;
        lBtn.onClick.AddListener(OnLaserButtonClicked);
        MakeTMP("T",laserGo.transform,V2(0,0),V2(1,1),V2(2,2),V2(-2,-2),
            "开启入射光线",16,Color.white,TextAlignmentOptions.Center,true);

        // 光线
        incidentLine  = MakeRayLine("Incident",  LASER);
        reflectedLine = MakeRayLine("Reflected", LASERW);
        refractedLine = MakeRayLine("Refracted", LASER);

        // 角度标注
        incidentAngleLabel = MakeAngleLabel("IncLbl");
        reflectAngleLabel  = MakeAngleLabel("ReflLbl");
        refractAngleLabel  = MakeAngleLabel("RefrLbl");
    }

    void MakeAreaTMP(string text, Vector2 center, Color color, float size, bool bold)
    {
        var go = new GameObject("AT_"+text);
        go.transform.SetParent(experimentArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = center - new Vector2(0.12f,0.05f);
        rt.anchorMax = center + new Vector2(0.12f,0.05f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp); tmp.text = text; tmp.fontSize = size;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    TextMeshProUGUI MakeAngleLabel(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(experimentArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.sizeDelta = new Vector2(70f,20f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp); tmp.fontSize = 14;
        tmp.color = new Color(1f,0.95f,0.5f,0.9f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    Image MakeRayLine(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(experimentArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.pivot = new Vector2(0f,0.5f);
        rt.sizeDelta = new Vector2(120f,3f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return img;
    }

    void HideAllRays()
    {
        if (incidentLine  != null) incidentLine.gameObject.SetActive(false);
        if (reflectedLine != null) reflectedLine.gameObject.SetActive(false);
        if (refractedLine != null) refractedLine.gameObject.SetActive(false);
        if (incidentAngleLabel != null) incidentAngleLabel.gameObject.SetActive(false);
        if (reflectAngleLabel  != null) reflectAngleLabel.gameObject.SetActive(false);
        if (refractAngleLabel  != null) refractAngleLabel.gameObject.SetActive(false);
    }

    void ShowAllRays()
    {
        if (incidentLine       != null) incidentLine.gameObject.SetActive(true);
        if (reflectedLine      != null) reflectedLine.gameObject.SetActive(true);
        if (refractedLine      != null) refractedLine.gameObject.SetActive(true);
        if (incidentAngleLabel != null) incidentAngleLabel.gameObject.SetActive(true);
        if (reflectAngleLabel  != null) reflectAngleLabel.gameObject.SetActive(true);
        if (refractAngleLabel  != null) refractAngleLabel.gameObject.SetActive(true);
        // 强制刷新光线
        UpdateRayLines(angleSlider != null ? angleSlider.value : 15f);
    }

    // ══════════════════════════════════════════
    // 光路绘制
    // ══════════════════════════════════════════
    void UpdateRayLines(float angleDeg)
    {
        if (experimentArea == null) return;
        float w   = experimentArea.rect.width;
        float h   = experimentArea.rect.height;
        float len = Mathf.Min(w,h) * 0.42f;
        if (len < 50f) len = 100f;

        currentIncidentAngle = angleDeg;
        float ratio  = n1 / n2;
        float rad    = angleDeg * Mathf.Deg2Rad;
        float sinRef = ratio * Mathf.Sin(rad);
        float t      = Mathf.Clamp01(angleDeg / criticalAngle);

        // 入射光：固定红色
        SetRay(incidentLine, 270f - angleDeg, len);
        if (incidentLine != null) incidentLine.color = LASER;

        // 反射光：接近全反射越亮
        if (reflectedLine != null)
            reflectedLine.color = new Color(1f,0.15f,0.15f, Mathf.Lerp(0.2f,1f,t));
        SetRay(reflectedLine, angleDeg - 90f, len);

        if (sinRef >= 1f)
        {
            isTotalReflection   = true;
            currentRefractAngle = 0f;
            if (reflectedLine != null) reflectedLine.color = LASER;
            if (refractedLine != null) refractedLine.gameObject.SetActive(false);
            UpdateAngleLabels(angleDeg, 0f, true);
        }
        else
        {
            isTotalReflection   = false;
            currentRefractAngle = Mathf.Asin(sinRef) * Mathf.Rad2Deg;
            // 折射光：接近全反射越浅
            if (refractedLine != null)
            {
                refractedLine.color = new Color(1f,0.15f,0.15f, Mathf.Lerp(1f,0.08f,t));
                if (laserOn) refractedLine.gameObject.SetActive(true);
            }
            SetRay(refractedLine, 90f - currentRefractAngle, len);
            UpdateAngleLabels(angleDeg, currentRefractAngle, false);
        }
    }

    void UpdateAngleLabels(float inc, float refr, bool isTR)
    {
        float d = 30f;
        if (incidentAngleLabel != null)
        {
            incidentAngleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-d,-d);
            incidentAngleLabel.text = "θ1=" + inc.ToString("F0") + "°";
        }
        if (reflectAngleLabel != null)
        {
            reflectAngleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(d+15f,-d);
            reflectAngleLabel.text  = "θr=" + inc.ToString("F0") + "°";
            reflectAngleLabel.color = new Color(1f,0.6f,0.6f,0.8f);
        }
        if (refractAngleLabel != null)
        {
            if (isTR)
            {
                refractAngleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f,35f);
                refractAngleLabel.text  = "全反射！";
                refractAngleLabel.color = new Color(1f,0.3f,0.3f,1f);
                refractAngleLabel.gameObject.SetActive(true);
            }
            else
            {
                refractAngleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(d+15f,d);
                refractAngleLabel.text  = "θ2=" + refr.ToString("F0") + "°";
                refractAngleLabel.color = new Color(1f,0.95f,0.5f,0.9f);
                refractAngleLabel.gameObject.SetActive(laserOn);
            }
        }
    }

    void SetRay(Image line, float angleDeg, float len)
    {
        if (line == null) return;
        var rt = line.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(len,3f);
        rt.localRotation = Quaternion.Euler(0,0,angleDeg);
    }

    // ══════════════════════════════════════════
    // 进度星星
    // ══════════════════════════════════════════
    void BuildStarDisplay()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var container = new GameObject("StarBar");
        container.transform.SetParent(canvas.transform, false);
        var rt = container.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.15f,0.93f);
        rt.anchorMax = new Vector2(0.85f,1.00f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var barBg = container.AddComponent<Image>();
        barBg.color = new Color(0.04f,0.08f,0.18f,0.85f);
        barBg.raycastTarget = false;
        MakeBorder(container, new Color(0.72f,0.53f,0.04f,0.5f),1f);

        string[] labels = { "认识光线", "发现全反射", "理解原理" };
        float[] xMins   = { 0.02f, 0.35f, 0.68f };

        for (int i = 0; i < 3; i++)
        {
            var sGo = new GameObject("Star"+i);
            sGo.transform.SetParent(container.transform, false);
            var sRt = sGo.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(xMins[i],0.05f);
            sRt.anchorMax = new Vector2(xMins[i]+0.28f,0.95f);
            sRt.offsetMin = sRt.offsetMax = Vector2.zero;
            starImages[i] = sGo.AddComponent<Image>();
            starImages[i].color = new Color(0.15f,0.15f,0.18f,0.8f);
            MakeBorder(sGo, new Color(0.4f,0.4f,0.4f,0.5f),1f);

            var lGo = new GameObject("Lbl");
            lGo.transform.SetParent(sGo.transform, false);
            FillRect(lGo);
            var lt = lGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(lt); lt.text = "☆  " + labels[i];
            lt.fontSize = 14; lt.color = new Color(0.5f,0.5f,0.5f,1f);
            lt.alignment = TextAlignmentOptions.Center;
            lt.raycastTarget = false;
        }
    }

    void EarnStar(int index)
    {
        if (index >= 3 || starImages[index] == null) return;
        starsEarned++;
        AudioManager.PlayStar();
        starImages[index].color = new Color(0.25f,0.18f,0.02f,0.95f);
        MakeBorder(starImages[index].gameObject, GOLD, 1.5f);

        var lbl = starImages[index].transform.Find("Lbl");
        if (lbl != null)
        {
            var t = lbl.GetComponent<TextMeshProUGUI>();
            if (t != null)
            {
                t.color = GOLD; t.fontSize = 13;
                t.fontStyle = FontStyles.Bold;
                if (t.text.StartsWith("☆"))
                    t.text = "★" + t.text.Substring(1);
            }
        }
        StartCoroutine(StarPopAnimation(starImages[index].GetComponent<RectTransform>()));
    }

    IEnumerator StarPopAnimation(RectTransform rt)
    {
        if (rt == null) yield break;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 6f;
            float s = t < 0.6f ? Mathf.Lerp(1f,1.4f,t/0.6f) : Mathf.Lerp(1.4f,1f,(t-0.6f)/0.4f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ══════════════════════════════════════════
    // 屏幕闪光
    // ══════════════════════════════════════════
    void BuildFlashOverlay()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("Flash");
        go.transform.SetParent(canvas.transform, false);
        FillRect(go);
        flashOverlay = go.AddComponent<Image>();
        flashOverlay.color = new Color(0,0,0,0);
        flashOverlay.raycastTarget = false;
    }

    IEnumerator FlashScreen(Color flashColor)
    {
        if (flashOverlay == null) yield break;
        flashOverlay.color = flashColor;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            flashOverlay.color = Color.Lerp(flashColor, new Color(0,0,0,0), t);
            yield return null;
        }
        flashOverlay.color = new Color(0,0,0,0);
    }

    // ══════════════════════════════════════════
    // 对话系统
    // ══════════════════════════════════════════
    void ShowDialogue(string[] lines, System.Action onFinish)
    {
        isInDialogue = true;
        currentLines = lines; lineIndex = 0; lineCallback = onFinish;
        Show(dialoguePanel); DisplayLine();
    }

    void DisplayLine()
    {
        var parts  = currentLines[lineIndex].Split('|');
        string spk = parts.Length > 1 ? parts[0].Trim() : "";
        string txt = parts.Length > 1 ? parts[1].Trim() : currentLines[lineIndex];

        if (speakerNameText != null) { speakerNameText.text = spk; ApplyFont(speakerNameText); }
        if (avatarImage != null)
        {
            Sprite sp = spk == "艾莉" ? allyAvatar : spk == "我" ? conanAvatar : null;
            avatarImage.sprite = sp;
            avatarImage.color  = sp != null ? Color.white : new Color(0.1f,0.15f,0.35f,0.8f);
        }
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        fullText = txt; typingDone = false;
        if (contentText != null)
        { ApplyFont(contentText); typingCoroutine = StartCoroutine(TypeText(contentText, txt)); }
    }

    IEnumerator TypeText(TextMeshProUGUI target, string text)
    {
        target.text = "";
        foreach (char c in text)
        { target.text += c; yield return new WaitForSeconds(0.04f); }
        typingDone = true;
    }

    public void OnContinue()
    {
        if (!typingDone)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (contentText != null) contentText.text = fullText;
            typingDone = true; return;
        }
        lineIndex++;
        if (lineIndex >= currentLines.Length)
        { Hide(dialoguePanel); isInDialogue = false; lineCallback?.Invoke(); }
        else DisplayLine();
    }

    // ══════════════════════════════════════════
    // UI工具
    // ══════════════════════════════════════════
    GameObject MakeActionButton(string label, Color color,
        System.Action onClick, Vector2 ancMin, Vector2 ancMax,
        Transform parent = null)
    {
        var canvas = FindObjectOfType<Canvas>();
        Transform p = parent != null ? parent : (canvas != null ? canvas.transform : null);
        if (p == null) return null;

        var go = new GameObject("Btn_"+label);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = color;
        MakeBorder(go, new Color(1f,1f,1f,0.4f), 1.5f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var bc  = btn.colors;
        bc.highlightedColor = new Color(
            Mathf.Min(color.r+0.15f,1f),
            Mathf.Min(color.g+0.15f,1f),
            Mathf.Min(color.b+0.15f,1f),1f);
        btn.colors = bc;
        btn.onClick.AddListener(() => onClick?.Invoke());
        btn.onClick.AddListener(() => AudioManager.PlayClick());
        MakeTMP("T",go.transform,V2(0,0),V2(1,1),V2(4,2),V2(-4,-2),
            label,18,DARK,TextAlignmentOptions.Center,true);
        StartCoroutine(PopIn(rt));
        return go;
    }

    Image MakeImg(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.AddComponent<Image>(); img.color = color;
        return img;
    }

    TextMeshProUGUI MakeTMP(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax,
        string text, float fontSize, Color color,
        TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp); tmp.text = text; tmp.fontSize = fontSize;
        tmp.color = color; tmp.alignment = align;
        tmp.enableWordWrapping = true;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    void MakeBorder(GameObject go, Color color, float thick)
    {
        void L(bool h, bool s)
        {
            var lg = new GameObject("L"); lg.transform.SetParent(go.transform, false);
            var rt = lg.AddComponent<RectTransform>();
            if (h) { rt.anchorMin=V2(0,s?1:0); rt.anchorMax=V2(1,s?1:0); rt.offsetMin=V2(0,s?-thick:0); rt.offsetMax=V2(0,s?0:thick); }
            else   { rt.anchorMin=V2(s?0:1,0); rt.anchorMax=V2(s?0:1,1); rt.offsetMin=V2(s?0:-thick,0); rt.offsetMax=V2(s?thick:0,0); }
            lg.AddComponent<Image>().color = color;
        }
        L(true,true); L(true,false); L(false,true); L(false,false);
    }

    void FillRect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void Show(GameObject go) { if (go != null) go.SetActive(true); }
    void Hide(GameObject go) { if (go != null) go.SetActive(false); }
    void ApplyFont(TextMeshProUGUI t) { if (chineseFont != null) t.font = chineseFont; }
    static Vector2 V2(float x, float y) => new Vector2(x, y);
    string EscapeJson(string s) =>
        s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");

    IEnumerator PopIn(RectTransform rt)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero; float t = 0f;
        while (t < 1f)
        {
            if (rt == null || rt.gameObject == null) yield break;
            t += Time.deltaTime * 5f;
            float s = t < 0.7f ? Mathf.Lerp(0f,1.1f,t/0.7f) : Mathf.Lerp(1.1f,1f,(t-0.7f)/0.3f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        if (rt != null && rt.gameObject != null) rt.localScale = Vector3.one;
    }

    IEnumerator DelayDo(float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);
        onDone?.Invoke();
    }
}
