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
            { idleTimer = 0f; hintLevel = -1; lastAngle = cur; }
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

    // ══════════════════════════════════════════
    // 提示等级
    // ══════════════════════════════════════════
    void GiveHint(int level)
    {
        string context = $"玩家在阶段{stage}停留太久，hint等级{level}，当前角度{currentIncidentAngle:.1f}度，生成一句简短提示引导玩家继续探索";
        StartCoroutine(ShanShanAsk(context));
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
        StartCoroutine(ClearSession());
        StartCoroutine(DelayDo(0.5f, () =>
            StartCoroutine(ShanShanAsk("玩家刚进入实验，闪闪打招呼并引导玩家点击开启入射光线按钮"))
        ));
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
        if (angleSlider != null) angleSlider.interactable = true;
        stage = 2;
        idleTimer = 0f; hintLevel = -1;

        StartCoroutine(DelayDo(0.8f, () => {
            StartCoroutine(ShanShanAsk("玩家刚开启光线，问他能看到几条光线。选项：1条|2条|3条|4条"));
        }));
    }

    // ══════════════════════════════════════════
    // Slider
    // ══════════════════════════════════════════
    void OnSliderChanged(float value)
    {
        UpdateRayLines(value);
        if (stage < 2) return;

        // 猜测挑战
        if (stage == 2 && !stage2Triggered && value > 20f && !isTotalReflection)
        {
            stage2Triggered = true;
            StartCoroutine(DelayDo(1f, ShowPredictionChallenge));
        }

        // 折射规律选择题
        if (stage == 3 && !stage3Triggered && value > 38f && !isTotalReflection)
        {
            stage3Triggered = true;
            stage = 4;
            idleTimer = 0f; hintLevel = -1;
            StartCoroutine(DelayDo(0.8f, () => {
                StartCoroutine(ShanShanAsk("玩家正在探索折射规律，问他入射角变大时折射角怎么变", () => {
                    ShowChoiceBubble(
                        new[]{ "也变大", "变小", "不变" },
                        new System.Action[]{
                            () => OnRefractionRuleAnswer(true),
                            () => OnRefractionRuleAnswer(false),
                            () => OnRefractionRuleAnswer(false)
                        });
                }));
            }));
        }

        // 临界角选择题（必须折射规律答完才触发）
        if (stage == 4 && !stage4Triggered && value > 45f && !isTotalReflection && refractionRuleAnswered)
        {
            stage4Triggered = true;
            StartCoroutine(DelayDo(0.8f, () => {
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
        }

        // 全反射
        if (stage >= 3 && isTotalReflection && !totalReflFound)
            OnTotalReflectionFirst();
    }

    // ══════════════════════════════════════════
    // 答题回调
    // ══════════════════════════════════════════
    void OnLineCountAnswer(int count)
    {
        ClearBubbles();
        if (count == 3)
        {
            EarnStar(0);
            AudioManager.PlayCorrect();
            StartCoroutine(ShanShanAsk("玩家答对了3条光线，引导他继续探索增大角度观察折射"));
            StartCoroutine(DelayDo(3f, () =>
                StartCoroutine(CallShanShanApi("玩家认识了3条光线，已了解折射概念，引导他拖动滑块增大角度探索折射规律", 2))));
        }
        else
        {
            AudioManager.PlayWrong();
            StartCoroutine(ShanShanAsk("玩家答错了，引导他再仔细数一数有3条光线"));
            learningTracker?.OnAnswerRecorded("line_count");
        }
    }

    void OnRefractionRuleAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            refractionRuleAnswered = true;
            AudioManager.PlayCorrect();  // 解锁临界角题
            StartCoroutine(ShanShanAsk("玩家答对了折射规律，鼓励并引导他继续增大角度接近临界角"));
            StartCoroutine(DelayDo(3f, () =>
                StartCoroutine(CallShanShanApi("玩家答对折射规律，引导他继续增大角度接近临界角48度", 3))));
        }
        else
        {
            AudioManager.PlayWrong();
            StartCoroutine(ShanShanAsk("玩家答错了折射规律，引导他对比30度和45度的折射角变化"));
            learningTracker?.OnAnswerRecorded("refraction_rule");
            StartCoroutine(DelayDo(2f, () => {
                StartCoroutine(ShanShanAsk("再次问玩家：入射角变大时，折射角怎么变？", () => {
                    ShowChoiceBubble(
                        new[]{ "也变大", "变小", "不变" },
                        new System.Action[]{
                            () => OnRefractionRuleAnswer(true),
                            () => OnRefractionRuleAnswer(false),
                            () => OnRefractionRuleAnswer(false)
                        });
                }));
            }));
        }
    }

    void OnCriticalAngleAnswer(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            AudioManager.PlayCorrect();
            StartCoroutine(ShanShanAsk("玩家答对了临界角，引导他把角度增大超过48度观察全反射"));
            StartCoroutine(DelayDo(3f, () =>
                StartCoroutine(CallShanShanApi("玩家理解临界角，引导他把角度增大超过48度发现全反射", 4))));
        }
        else
        {
            AudioManager.PlayWrong();
            StartCoroutine(ShanShanAsk("玩家答错了临界角概念，引导他再选一次"));
            learningTracker?.OnAnswerRecorded("critical_angle");
            StartCoroutine(DelayDo(1.5f, () => {
                StartCoroutine(ShanShanAsk("再次问玩家：折射角=90度时的入射角叫什么？", () => {
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

    // ══════════════════════════════════════════
    // 猜测挑战
    // ══════════════════════════════════════════
    void ShowPredictionChallenge()
    {
        if (predictionMade) return;
        predictionMade = true;
        StartCoroutine(ShanShanAsk("玩家接近临界角（47度以上），折射光越来越弱，继续增大角度会发生什么？选项：变得更强|逐渐消失|方向不变"));
    }

    void OnPrediction(bool correct, string reply)
    {
        ClearBubbles();
        stage = 3;
        idleTimer = 0f; hintLevel = -1;
        if (correct)
        {
            StartCoroutine(CallShanShanApi("玩家预测折射光会消失，猜对了方向！请鼓励他继续增大角度验证", 3));
        }
        else
        {
            ShanShanSayLocal(reply);
            learningTracker?.OnAnswerRecorded("prediction");
            // 答错了，给一次重选机会
            StartCoroutine(DelayDo(1.5f, ShowPredictionRetryBubble));
        }
    }

    void ShowPredictionRetryBubble()
    {
        StartCoroutine(ShanShanAsk("玩家预测折射光会变强，引导他再仔细观察折射光强度变化", () => {
            ShowChoiceBubble(
                new[]{ "变得更强", "逐渐消失", "方向不变" },
                new System.Action[]{
                    () => OnPredictionSecond(false),
                    () => OnPredictionSecond(true),
                    () => OnPredictionSecond(false)
                });
        }));
    }

    void OnPredictionSecond(bool correct)
    {
        ClearBubbles();
        if (correct)
        {
            StartCoroutine(ShanShanAsk("玩家答对了折射光变弱，鼓励他继续增大角度"));
            StartCoroutine(CallShanShanApi("玩家发现折射光逐渐消失，正确预测折射规律，请鼓励他继续增大角度接近临界角", 3));
        }
        else
        {
            StartCoroutine(ShanShanAsk("玩家答错了折射光强度变化，引导他继续增大角度观察"));
            StartCoroutine(CallShanShanApi("玩家预测折射光方向或强度变化，继续引导他观察并增大角度", 3));
        }
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
        AudioManager.PlayLaser();
        StartCoroutine(FlashScreen(new Color(1f,0.3f,0.1f,0.4f)));
        StartCoroutine(TotalReflectionSequence());
    }

    IEnumerator TotalReflectionSequence()
    {
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(ShanShanAsk("全反射发生了！折射光消失了！用惊讶语气问玩家光去哪了。选项：光消失了|光全部反射回水中|光被水吸收了"));
    }

    void ShowTotalReflectionRetryBubble()
    {
        StartCoroutine(ShanShanAsk("玩家答错了折射光消失问题，引导他再想想光去了哪里", () => {
            ShowChoiceBubble(
                new[]{ "光消失了", "光全部反射回水中", "光被水吸收了" },
                new System.Action[]{
                    () => OnTotalReflAnswerSecond(false, "还是不对哦！注意观察反射光——它变亮了！"),
                    () => OnTotalReflAnswerSecond(true,  "对！光全部反射回水中——这就是全反射！"),
                    () => OnTotalReflAnswerSecond(false, "不是被吸收！注意观察反射光——它变亮了！")
                });
        }));
    }

    void OnTotalReflAnswerSecond(bool correct, string reply)
    {
        ClearBubbles();
        ShanShanSayLocal(reply);
        if (correct)
        {
            StartCoroutine(CallShanShanApi("玩家理解了光全部反射回水中，请确认这就是全反射", 5, ShowDiscoveryCard));
        }
        else
        {
            learningTracker?.OnAnswerRecorded("total_reflection");
            // 第二次还错，继续给机会，提示反射光变亮
            StartCoroutine(DelayDo(1.5f, ShowTotalReflectionThirdBubble));
        }
    }

    void ShowTotalReflectionThirdBubble()
    {
        StartCoroutine(ShanShanAsk("玩家需要判断光去哪了，引导他注意观察反射光变亮，提示光全部反射回去了", () => {
            ShowChoiceBubble(
                new[]{ "光消失了", "光全部反射回水中", "光被水吸收了" },
                new System.Action[]{
                    () => OnTotalReflAnswerThird(false, "不是消失哦！反射光变亮了，说明光只是反射回去了！"),
                    () => OnTotalReflAnswerThird(true,  "对！光全部反射回水中——这就是全反射！"),
                    () => OnTotalReflAnswerThird(false, "不是被吸收！反射光变亮了，说明光反射回去了！")
                });
        }));
    }

    void OnTotalReflAnswerThird(bool correct, string reply)
    {
        ClearBubbles();
        ShanShanSayLocal(reply);
        if (correct)
        {
            StartCoroutine(CallShanShanApi("玩家理解了光全部反射回水中，请确认这就是全反射", 5, ShowDiscoveryCard));
        }
        else
        {
            // 三次机会都用完了，不再给选项，直接解释并展示发现卡片
            StartCoroutine(ShanShanAsk("玩家多次答错，直接告诉他光全部反射回水中，这就是全反射"));
            StartCoroutine(DelayDo(2f, ShowDiscoveryCard));
        }
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
            StartCoroutine(ShanShanAsk("玩家答错了古币问题，解释从侧面看角度大导致全反射光出不来，引导再想"));
            learningTracker?.OnAnswerRecorded("coin_angle");
            // 答错了，给第二次选择机会
            StartCoroutine(DelayDo(1.5f, ShowCoinRetryBubble));
        }
    }

    void ShowCoinRetryBubble()
    {
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
            StartCoroutine(ShanShanAsk("玩家答对了，解释古币消失原因并引导回博物馆"));
            StartCoroutine(DelayDo(1.5f, ShowGoButton));
        }
        else
        {
            StartCoroutine(ShanShanAsk("玩家答错了，解释从侧面看角度大导致全反射，引导回博物馆"));
            StartCoroutine(DelayDo(1.5f, ShowGoButton));
        }
    }

    void ShowGoButton()
    {
        goBtn = MakeActionButton("已明白原理，回博物馆破案！",GOLD,
            () => {
                if (goBtn != null) { Destroy(goBtn); goBtn = null; }
                StartAllyEnding();
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
        bubbleContainer.AddComponent<Image>().color = new Color(0,0,0,0);

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
        waitingForChoice = false;  // 恢复idle提示
        idleTimer = 0f;
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
        StartCoroutine(CallShanShanApi(q, stage));
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

    // AI生成问题+选项，并显示
    IEnumerator ShanShanAsk(string context, System.Action onReplyDone = null)
    {
        if (shanShanBusy) yield break;
        shanShanBusy = true;
        wrongAttempts = 0;

        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question\":\"" + EscapeJson(context) + "\"," +
            "\"incident_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"is_total_reflection\":" + (isTotalReflection ? "true" : "false") + "," +
            "\"refract_angle\":" + currentRefractAngle.ToString("F1") + "," +
            "\"exploration_stage\":" + stage + "}";

        using var req = new UnityWebRequest(shanShanServerUrl + "/chat", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        shanShanBusy = false;

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            // 解析 question 字段
            int qIdx = json.IndexOf("\"question\":\"");
            if (qIdx >= 0)
            {
                int qStart = qIdx + 12;
                int qEnd = json.IndexOf("\"", qStart);
                string question = qEnd > qStart ? json.Substring(qStart, qEnd - qStart) : "";

                // 解析 options 字段
                string[] options = new string[0];
                int optIdx = json.IndexOf("\"options\":[");
                if (optIdx >= 0)
                {
                    int arrStart = json.IndexOf("[", optIdx);
                    int arrEnd = json.IndexOf("]", arrStart);
                    if (arrEnd > arrStart)
                    {
                        string arrStr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                        string[] parts = arrStr.Split(new char[] { '"' });
                        var optList = new System.Collections.Generic.List<string>();
                        foreach (var p in parts)
                        {
                            string trimmed = p.Trim().Trim(',', ' ');
                            if (!string.IsNullOrEmpty(trimmed) && trimmed != ",")
                                optList.Add(trimmed);
                        }
                        options = optList.ToArray();
                    }
                }

                aiCurrentQuestion = question;
                aiCurrentOptions = options;
                ShanShanSayLocal(question, true);

                if (options.Length > 0)
                {
                    yield return new WaitForSeconds(1f);
                    // 所有选项共用同一个callback：发给AI评判
                    System.Action[] cbs = new System.Action[options.Length];
                    for (int i = 0; i < cbs.Length; i++) cbs[i] = () => StartCoroutine(SendAnswerToAI());
                    ShowChoiceBubble(options, cbs);
                }
                shanShanBusy = false;
                onReplyDone?.Invoke();
                yield break;
            }
        }
        ShanShanSayLocal("闪闪暂时累了，继续观察实验台吧！", true);
        shanShanBusy = false;
        onReplyDone?.Invoke();
    }

    IEnumerator SendAnswerToAI()
    {
        // 等一下让玩家看清选中的效果
        yield return new WaitForSeconds(0.3f);

        string body =
            "{\"session_id\":\"" + sessionId + "\"," +
            "\"question\":\"" + EscapeJson(aiCurrentQuestion) + "\"," +
            "\"incident_angle\":" + currentIncidentAngle.ToString("F1") + "," +
            "\"is_total_reflection\":" + (isTotalReflection ? "true" : "false") + "," +
            "\"refract_angle\":" + currentRefractAngle.ToString("F1") + "," +
            "\"exploration_stage\":" + stage + "," +
            "\"selected_option\":\"" + EscapeJson(lastSelectedOption) + "\"}";

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

        ShanShanSayLocal(feedback, true);
        lastSelectedOption = "";

        if (nextAction == "show_discovery_card")
        {
            AudioManager.PlayCorrect();
            StartCoroutine(DelayDo(1.5f, ShowDiscoveryCard));
            return;
        }

        if (nextAction == "advance" || (correct && string.IsNullOrEmpty(nextAction)))
        {
            if (!string.IsNullOrEmpty(question) && options.Length > 0)
            {
                AudioManager.PlayCorrect();
                aiCurrentQuestion = question;
                aiCurrentOptions = options;
                StartCoroutine(DelayDo(1.5f, () => {
                    ShanShanSayLocal(question, true);
                    ShowAIOptionBubble(options);
                }));
            }
            return;
        }

        // retry 或答错
        AudioManager.PlayWrong();
        wrongAttempts++;
        learningTracker?.OnAnswerRecorded("ai_wrong");
        if (!string.IsNullOrEmpty(question) && options.Length > 0)
        {
            aiCurrentQuestion = question;
            aiCurrentOptions = options;
            StartCoroutine(DelayDo(1.5f, () => {
                ShanShanSayLocal(question, true);
                ShowAIOptionBubble(options);
            }));
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
            t += Time.deltaTime * 5f;
            float s = t < 0.7f ? Mathf.Lerp(0f,1.1f,t/0.7f) : Mathf.Lerp(1.1f,1f,(t-0.7f)/0.3f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator DelayDo(float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);
        onDone?.Invoke();
    }
}
