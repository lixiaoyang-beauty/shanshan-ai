using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Chapter4Manager : MonoBehaviour
{
    [Header("── 场景对象 ──")]
    public Camera mainCamera;
    public Transform curatorNPC;
    public Transform securityNPC;

    [Header("── 对话框 ──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 提示UI ──")]
    public GameObject hintUI;
    public TextMeshProUGUI hintText;

    [Header("── 选择题面板 ──")]
    public GameObject choicePanel;
    public TextMeshProUGUI choiceQuestionText;
    public Button[] choiceButtons;
    public TextMeshProUGUI[] choiceTexts;

    [Header("── 结案第一帧 ──")]
    public GameObject caseClosedPanel;

    [Header("── 结案第二帧（空对象即可）──")]
    public GameObject caseSummaryPanel;

    [Header("── 头像 ──")]
    public Sprite curatorAvatar;
    public Sprite conanAvatar;
    public Sprite securityAvatar;

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    [Header("── 移动 ──")]
    public float moveSpeed = 4f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 3f;

    // 内部状态
    private bool canMove = false;
    private bool isInDialogue = false;
    private bool introFinished = false;
    private float cameraPitch = 0f;
    private int interactStage = 0;
    private bool caseClosedShowing = false;

    // 对话
    private string[] currentLines;
    private int lineIndex;
    private System.Action lineCallback;
    private Coroutine typingCoroutine;
    private bool typingDone = false;
    private string fullText = "";

    // 选择题
    private bool[] currentCorrect;
    private System.Action<bool> choiceCallback;

    // 颜色
    static readonly Color COL_NAVY    = new Color(0.04f, 0.08f, 0.21f, 1f);
    static readonly Color COL_NAVY2   = new Color(0.06f, 0.12f, 0.26f, 1f);
    static readonly Color COL_GOLD    = new Color(0.72f, 0.53f, 0.04f, 1f);
    static readonly Color COL_GOLDF   = new Color(0.72f, 0.53f, 0.04f, 0.15f);
    static readonly Color COL_GOLDB   = new Color(0.72f, 0.53f, 0.04f, 0.35f);
    static readonly Color COL_CREAM   = new Color(0.96f, 0.93f, 0.82f, 1f);
    static readonly Color COL_CREAM2  = new Color(0.83f, 0.78f, 0.60f, 1f);
    static readonly Color COL_DARK    = new Color(0.08f, 0.05f, 0.01f, 1f);
    static readonly Color COL_GREEN   = new Color(0.18f, 0.62f, 0.33f, 1f);
    static readonly Color COL_GREENF  = new Color(0.06f, 0.31f, 0.13f, 0.6f);
    static readonly Color COL_RED     = new Color(0.75f, 0.12f, 0.12f, 1f);
    static readonly Color COL_REDF    = new Color(0.75f, 0.12f, 0.12f, 0.5f);
    static readonly Color COL_CARDBG  = new Color(0.08f, 0.13f, 0.26f, 1f);
    static readonly Color COL_CARDH   = new Color(0.14f, 0.20f, 0.38f, 1f);
    static readonly Color COL_DIVIDER = new Color(0.72f, 0.53f, 0.04f, 0.28f);

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

        Hide(dialoguePanel); Hide(choicePanel);
        Hide(caseClosedPanel); Hide(caseSummaryPanel); Hide(hintUI);

        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }

        if (choiceButtons != null)
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int idx = i;
                if (choiceButtons[i] != null)
                { choiceButtons[i].onClick.RemoveAllListeners(); choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(idx)); }
            }

        AudioManager.AddClickSound(continueButton);
        if (choiceButtons != null)
            foreach (var b in choiceButtons)
                AudioManager.AddClickSound(b);

        StartCoroutine(OnSceneEnter());
    }

    // ══════════════════════════════════════════
    // 进场
    // ══════════════════════════════════════════
    IEnumerator OnSceneEnter()
    {
        yield return new WaitForSeconds(0.8f);
        ShowHint("发现馆长和保安正在交谈，走近看看！");
        introFinished = true; canMove = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    // ══════════════════════════════════════════
    // UPDATE
    // ══════════════════════════════════════════
    void Update()
    {
        if (canMove && !isInDialogue) HandleMovement();
        HandleMouseLook();
        if (isInDialogue && Input.GetKeyDown(KeyCode.Return)) OnContinue();
        if (caseClosedShowing && Input.GetKeyDown(KeyCode.Return)) EnterCaseSummary();
        if (introFinished && !isInDialogue && interactStage < 2) HandleInteract();
    }

    void HandleMovement()
    {
        if (mainCamera == null) return;
        float h = Input.GetAxis("Horizontal"), v = Input.GetAxis("Vertical");
        Vector3 dir = mainCamera.transform.right * h + mainCamera.transform.forward * v;
        dir.y = 0;
        if (dir.magnitude > 0.01f)
            mainCamera.transform.position += dir.normalized * moveSpeed * Time.deltaTime;
    }

    void HandleMouseLook()
    {
        if (mainCamera == null || Cursor.lockState != CursorLockMode.Locked) return;
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch - my, -60f, 60f);
        mainCamera.transform.Rotate(Vector3.up, mx, Space.World);
        Vector3 e = mainCamera.transform.localEulerAngles;
        mainCamera.transform.localEulerAngles = new Vector3(cameraPitch, e.y, 0);
    }

    void HandleInteract()
    {
        if (curatorNPC == null || mainCamera == null) return;
        bool near = Vector3.Distance(mainCamera.transform.position, curatorNPC.position) < interactDistance;
        if (interactStage == 0)
        {
            ShowHint(near ? "按 [ E ] 键，听听馆长和保安在说什么" : "发现馆长和保安正在交谈，走近看看！");
            if (near && Input.GetKeyDown(KeyCode.E)) { Hide(hintUI); PlayNPCDialogue(); }
        }
        else if (interactStage == 1)
        {
            ShowHint(near ? "按 [ E ] 键，向馆长汇报你的实验结果！" : "去和馆长汇报你的实验结果吧！");
            if (near && Input.GetKeyDown(KeyCode.E))
            {
                Hide(hintUI); interactStage = 2;
                EnterDialogueMode(); StartPlayerDialogue();
            }
        }
    }

    void ShowHint(string msg)
    {
        Show(hintUI);
        if (hintText != null) { hintText.text = msg; ApplyFont(hintText); }
    }

    void EnterDialogueMode()
    {
        isInDialogue = true; canMove = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    // ══════════════════════════════════════════
    // 对话流程
    // ══════════════════════════════════════════
    void PlayNPCDialogue()
    {
        EnterDialogueMode();
        ShowDialogue(new[] {
            "馆长|王大叔，那枚古币失踪案你还记得吗？当时你说亲眼看到古币消失的？",
            "保安|记得！就放在那个装了水的玻璃展示杯里，我一转眼，古币就没了！",
            "馆长|这件事一直困扰着我……难道真的有人偷走了它？",
            "保安|可是监控录像里根本没有人靠近展台啊，馆长……"
        }, () => {
            interactStage = 1; isInDialogue = false; canMove = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            ShowHint("去和馆长汇报你的实验结果吧！");
        });
    }

    void StartPlayerDialogue()
    {
        ShowDialogue(new[] {
            "我|馆长，我找到古币消失的真相了！",
            "馆长|你就是那个小侦探？真的找到答案了？",
            "我|请您从这个角度看那个展示杯……",
            "馆长|咦！杯子里的古币……不见了！这到底是怎么回事！",
            "我|这就是案件的关键——光的全反射现象！让我来解释……"
        }, ShowQuestion1);
    }

    void ShowQuestion1()
    {
        ShowChoice("馆长：古币消失是因为什么？",
            new[] { "光线在水面发生了全反射，无法从水中射出", "古币被水溶解了", "水太深，看不见底部" },
            new[] { true, false, false },
            ok => {
                if (ok) ShowDialogue(new[] {
                    "我|正确！光线从水射向空气时，角度太大会全部反弹，古币的光到不了您眼睛里！",
                    "馆长|原来如此！那为什么换个角度又能看见古币呢？"
                }, ShowQuestion2);
                else ShowDialogue(new[] { "我|再想想！这和光线有关，回忆一下实验室里的实验！" }, ShowQuestion1);
            });
    }

    void ShowQuestion2()
    {
        ShowChoice("馆长：换个角度能看见，是因为？",
            new[] { "角度变小后，光线可以折射出水面到达眼睛", "换角度后古币浮起来了", "换角度后光线变强了" },
            new[] { true, false, false },
            ok => {
                if (ok) ShowDialogue(new[] {
                    "我|对！观察角度小于临界角时，光线折射出水面，古币重新出现！",
                    "馆长|太精彩了！一枚古币，一杯水，竟隐藏着这么深奥的光学原理！",
                    "保安|所以我看到古币消失，完全是正常的物理现象？！",
                    "我|没错！案件告破——这是光的全反射引发的科学魔术！"
                }, OnResolved);
                else ShowDialogue(new[] { "我|不对，想想实验室里的实验——是光的折射角度发生了变化！" }, ShowQuestion2);
            });
    }

    void OnResolved() { StartCoroutine(ShowCaseClosedAnim()); }

    // ══════════════════════════════════════════
    // 第一帧：案件告破弹出动画
    // ══════════════════════════════════════════
    IEnumerator ShowCaseClosedAnim()
    {
        yield return new WaitForSeconds(0.4f);
        Hide(dialoguePanel);
        Show(caseClosedPanel);
        caseClosedShowing = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        // 找到面板的RectTransform做弹出动画
        var rt = caseClosedPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.zero;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.5f;
                float s = t < 0.75f
                    ? Mathf.Lerp(0f, 1.08f, t / 0.75f)
                    : Mathf.Lerp(1.08f, 1f, (t - 0.75f) / 0.25f);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // 更新提示文字
        var tmps = caseClosedPanel.GetComponentsInChildren<TextMeshProUGUI>();
        if (tmps != null && tmps.Length > 0)
            tmps[tmps.Length - 1].text = "按 Enter 填写结案报告";
    }

    // ══════════════════════════════════════════
    // 第二帧：结案报告展开
    // ══════════════════════════════════════════
    void EnterCaseSummary()
    {
        if (!caseClosedShowing) return;
        caseClosedShowing = false;
        Hide(caseClosedPanel);
        Show(caseSummaryPanel);
        // 用独立组件构建UI，避免anchor计算问题
        var builder = caseSummaryPanel.GetComponent<CaseSummaryBuilder>();
        if (builder == null) builder = caseSummaryPanel.AddComponent<CaseSummaryBuilder>();
        builder.chineseFont = chineseFont;
        builder.manager = this;
        builder.Build();
    }

    IEnumerator BuildAndSlideInSummary()
    {
        // 已由CaseSummaryBuilder接管，此方法保留避免编译错误
        yield return null;

        var canvas = FindObjectOfType<Canvas>();
        var canvasRt = canvas.GetComponent<RectTransform>();

        // ── 半透明遮罩 ──
        var overlay = MakeImg("Overlay", caseSummaryPanel.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.55f));

        // ── 报告卡片 ──
        var card = MakeImg("Card", caseSummaryPanel.transform,
            new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.96f),
            Vector2.zero, Vector2.zero, COL_NAVY);
        MakeBorder(card.gameObject, COL_GOLD, 3f);

        // 卡片从下方滑入
        var cardRt = card.GetComponent<RectTransform>();
        Vector2 finalMin = cardRt.anchorMin;
        Vector2 finalMax = cardRt.anchorMax;
        cardRt.anchorMin = finalMin + Vector2.down * 0.3f;
        cardRt.anchorMax = finalMax + Vector2.down * 0.3f;
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, elapsed / 0.45f);
            cardRt.anchorMin = Vector2.Lerp(finalMin + Vector2.down * 0.3f, finalMin, p);
            cardRt.anchorMax = Vector2.Lerp(finalMax + Vector2.down * 0.3f, finalMax, p);
            yield return null;
        }
        cardRt.anchorMin = finalMin; cardRt.anchorMax = finalMax;

        // ── 标题栏 ──
        var titleBar = MakeImg("TitleBar", card.transform,
            new Vector2(0, 0.88f), Vector2.one,
            Vector2.zero, Vector2.zero, COL_NAVY2);
        MakeLine(titleBar.gameObject, true, false, COL_GOLD, 2f);

        MakeTMP("SubLabel", titleBar.transform,
            new Vector2(0, 0.55f), Vector2.one,
            new Vector2(0, 0), new Vector2(0, 0),
            "CASE FILE  ·  第四章", 11, COL_GOLD, TextAlignmentOptions.Center, true);

        MakeTMP("Title", titleBar.transform,
            Vector2.zero, new Vector2(1, 0.55f),
            new Vector2(0, 0), new Vector2(0, 0),
            "消失的古币  ·  结案报告", 20, COL_CREAM, TextAlignmentOptions.Center, true);

        // ── 案件信息卡片行 ──
        var infoRow = MakeImg("InfoRow", card.transform,
            new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.87f),
            Vector2.zero, Vector2.zero, new Color(0,0,0,0));

        BuildInfoCard("InfoCard1", infoRow.transform,
            new Vector2(0, 0), new Vector2(0.48f, 1f),
            "案件编号", "CASE-004");
        BuildInfoCard("InfoCard2", infoRow.transform,
            new Vector2(0.52f, 0), new Vector2(1f, 1f),
            "案件名称", "消失的古币");

        // 分割线
        MakeImg("Div1", card.transform,
            new Vector2(0.02f, 0.735f), new Vector2(0.98f, 0.738f),
            Vector2.zero, Vector2.zero, COL_DIVIDER);

        // ── 案件分析标签 ──
        MakeTMP("AnalysisLabel", card.transform,
            new Vector2(0.03f, 0.68f), new Vector2(0.97f, 0.73f),
            new Vector2(0, 0), new Vector2(0, 0),
            "案件分析", 11, COL_GOLD, TextAlignmentOptions.MidlineLeft, false);

        // ── 填空第一行 ──
        var slot1GO = BuildFillRow("FillRow1", card.transform,
            new Vector2(0.02f, 0.56f), new Vector2(0.98f, 0.68f),
            "消失原因", "古币消失是因为光发生了", "", "answer1");

        // ── 填空第二行 ──
        var slot2GO = BuildFillRow("FillRow2", card.transform,
            new Vector2(0.02f, 0.44f), new Vector2(0.98f, 0.56f),
            "重现原因", "改变角度后光发生", "使古币重现", "answer2");

        MakeImg("Div2", card.transform,
            new Vector2(0.02f, 0.435f), new Vector2(0.98f, 0.438f),
            Vector2.zero, Vector2.zero, COL_DIVIDER);

        // ── 词语碎片池 ──
        MakeTMP("WordPoolLabel", card.transform,
            new Vector2(0.03f, 0.38f), new Vector2(0.97f, 0.435f),
            new Vector2(0, 0), new Vector2(0, 0),
            "▼  将词语拖入上方空格", 11, COL_GOLD, TextAlignmentOptions.MidlineLeft, false);

        string[] words = { "全反射", "折射", "反射", "散射" };
        bool[] c1 = { true, false, false, false };
        bool[] c2 = { false, true, false, false };
        float[] xs = { 0.04f, 0.28f, 0.52f, 0.76f };

        for (int i = 0; i < words.Length; i++)
            BuildWordChip(words[i], card.transform,
                new Vector2(xs[i], 0.28f), new Vector2(xs[i] + 0.20f, 0.38f),
                c1[i], c2[i]);

        MakeImg("Div3", card.transform,
            new Vector2(0.02f, 0.275f), new Vector2(0.98f, 0.278f),
            Vector2.zero, Vector2.zero, COL_DIVIDER);

        // ── 签名区（初始隐藏）──
        var signRow = MakeImg("SignRow", card.transform,
            new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.27f),
            Vector2.zero, Vector2.zero, new Color(0,0,0,0));
        signRow.gameObject.SetActive(false);

        BuildInfoCard("SignCard1", signRow.transform,
            new Vector2(0, 0), new Vector2(0.48f, 1f),
            "侦探签名", "Conan");
        BuildInfoCard("SignCard2", signRow.transform,
            new Vector2(0.52f, 0), new Vector2(1f, 1f),
            "结论", "案件告破", true);

        // ── 盖章按钮（初始隐藏）──
        var stampBtn = BuildStampButton(card.transform,
            new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.13f));
        stampBtn.SetActive(false);

        // ── 设置DropSlot引用 ──
        var slot1 = slot1GO.AddComponent<DropSlot>();
        slot1.slotId = "answer1";
        var slot2 = slot2GO.AddComponent<DropSlot>();
        slot2.slotId = "answer2";
        slot1.other = slot2; slot2.other = slot1;
        slot1.stampButton = stampBtn;
        slot2.stampButton = stampBtn;
        slot1.signRow = signRow.gameObject;
        slot2.signRow = signRow.gameObject;
        slot1.manager = this;
        slot2.manager = this;
    }

    // ══════════════════════════════════════════
    // 构建填空行，返回槽的GameObject
    // ══════════════════════════════════════════
    GameObject BuildFillRow(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        string label, string prefix, string suffix, string slotId)
    {
        var row = MakeImg(name, parent,
            ancMin, ancMax, new Vector2(8, 4), new Vector2(-8, -4),
            new Color(1, 1, 1, 0.03f));
        MakeLine(row.gameObject, true, true,  COL_DIVIDER, 1f);
        MakeLine(row.gameObject, true, false, COL_DIVIDER, 1f);

        // 标签
        MakeTMP("Label", row.transform,
            new Vector2(0, 0), new Vector2(0.14f, 1f),
            new Vector2(8, 0), new Vector2(0, 0),
            label, 13, COL_GOLD, TextAlignmentOptions.MidlineLeft, true);

        // 竖线
        MakeImg("VSep", row.transform,
            new Vector2(0.14f, 0.15f), new Vector2(0.142f, 0.85f),
            Vector2.zero, Vector2.zero, COL_DIVIDER);

        // 前缀文字
        MakeTMP("Prefix", row.transform,
            new Vector2(0.15f, 0), new Vector2(0.52f, 1f),
            new Vector2(6, 0), new Vector2(0, 0),
            prefix, 14, COL_CREAM2, TextAlignmentOptions.MidlineLeft, false);

        // 空格槽
        var slot = MakeImg("Slot_" + slotId, row.transform,
            new Vector2(0.53f, 0.12f), new Vector2(0.73f, 0.88f),
            Vector2.zero, Vector2.zero, COL_GOLDF);
        MakeBorder(slot.gameObject, COL_GOLDB, 1.5f);

        MakeTMP("SlotHint", slot.transform,
            Vector2.zero, Vector2.one,
            new Vector2(4, 0), new Vector2(-4, 0),
            "拖入词语", 12, new Color(0.72f, 0.53f, 0.04f, 0.5f),
            TextAlignmentOptions.Center, false);

        // 后缀文字
        if (!string.IsNullOrEmpty(suffix))
            MakeTMP("Suffix", row.transform,
                new Vector2(0.74f, 0), new Vector2(1f, 1f),
                new Vector2(6, 0), new Vector2(-6, 0),
                suffix, 14, COL_CREAM2, TextAlignmentOptions.MidlineLeft, false);

        return slot.gameObject;
    }

    // ══════════════════════════════════════════
    // 信息卡片
    // ══════════════════════════════════════════
    void BuildInfoCard(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        string label, string value, bool green = false)
    {
        var card = MakeImg(name, parent, ancMin, ancMax,
            new Vector2(4, 0), new Vector2(-4, 0), COL_GOLDF);
        MakeBorder(card.gameObject, COL_GOLDB, 1f);

        MakeTMP("Label", card.transform,
            new Vector2(0, 0.52f), Vector2.one,
            new Vector2(12, 0), new Vector2(-12, 0),
            label, 10, COL_GOLD, TextAlignmentOptions.MidlineLeft, false);

        MakeTMP("Value", card.transform,
            Vector2.zero, new Vector2(1, 0.52f),
            new Vector2(12, 0), new Vector2(-12, 0),
            value, 15, green ? COL_GREEN : COL_CREAM,
            TextAlignmentOptions.MidlineLeft, true);
    }

    // ══════════════════════════════════════════
    // 词语碎片
    // ══════════════════════════════════════════
    void BuildWordChip(string word, Transform parent,
        Vector2 ancMin, Vector2 ancMax, bool c1, bool c2)
    {
        var chip = MakeImg("Chip_" + word, parent,
            ancMin, ancMax, new Vector2(4, 4), new Vector2(-4, -4), COL_CARDBG);
        MakeBorder(chip.gameObject, COL_GOLD, 1.5f);

        MakeTMP("T", chip.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            word, 16, COL_GOLD, TextAlignmentOptions.Center, true);

        var dw = chip.gameObject.AddComponent<DragWord>();
        dw.wordText = word;
        dw.isCorrectForSlot1 = c1;
        dw.isCorrectForSlot2 = c2;
    }

    // ══════════════════════════════════════════
    // 盖章按钮
    // ══════════════════════════════════════════
    GameObject BuildStampButton(Transform parent, Vector2 ancMin, Vector2 ancMax)
    {
        var btnGO = MakeImg("StampBtn", parent,
            ancMin, ancMax, Vector2.zero, Vector2.zero, COL_GOLD);
        MakeBorder(btnGO.gameObject, new Color(1f, 0.9f, 0.5f, 1f), 2f);

        MakeTMP("T", btnGO.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            "盖章提交结案报告", 17, COL_DARK,
            TextAlignmentOptions.Center, true);

        var btn = btnGO.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnGO;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f, 0.82f, 0.25f, 1f);
        bc.pressedColor = new Color(0.55f, 0.38f, 0.02f, 1f);
        btn.colors = bc;
        btn.onClick.AddListener(() => StartCoroutine(StampAndFinish(btnGO.gameObject)));

        return btnGO.gameObject;
    }

    // ══════════════════════════════════════════
    // 盖章动画 + 跳转
    // ══════════════════════════════════════════
    public IEnumerator StampAndFinish(GameObject btn, GameObject summaryRoot = null)
    {
        btn.GetComponent<Button>().interactable = false;
        var root = summaryRoot != null ? summaryRoot.transform : caseSummaryPanel.transform;

        // 找卡片
        var card = root.Find("Card");
        if (card == null) card = root;
        if (card == null) { SceneTransitionManager.LoadScene("Chapter5_Museum"); yield break; }

        // 创建红色印章覆盖层
        var stamp = MakeImg("Stamp", card,
            new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.72f),
            Vector2.zero, Vector2.zero, new Color(0.7f, 0.08f, 0.08f, 0f));
        MakeBorder(stamp.gameObject, new Color(0.8f, 0.1f, 0.1f, 0f), 4f);

        var stampTmp = MakeTMP("T", stamp.transform,
            Vector2.zero, Vector2.one,
            new Vector2(10, 10), new Vector2(-10, -10),
            "案件\n告破", 52, new Color(0.85f, 0.10f, 0.10f, 0f),
            TextAlignmentOptions.Center, true);

        // 印章从大缩小弹入
        stamp.transform.localScale = Vector3.one * 2.5f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            float s = Mathf.Lerp(2.5f, 1f, Mathf.SmoothStep(0, 1, t));
            stamp.transform.localScale = Vector3.one * s;
            float a = Mathf.Lerp(0f, 1f, t);
            stamp.color = new Color(0.7f, 0.08f, 0.08f, a * 0.25f);
            stampTmp.color = new Color(0.85f, 0.10f, 0.10f, a * 0.9f);
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        // 显示徽章提示
        var canvas = FindObjectOfType<Canvas>();
        var badge = MakeImg("Badge", root,
            new Vector2(0.15f, 0.02f), new Vector2(0.85f, 0.12f),
            Vector2.zero, Vector2.zero, COL_GOLD);
        MakeBorder(badge.gameObject, new Color(1f, 0.9f, 0.5f, 1f), 2f);
        MakeTMP("T", badge.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            "获得：案件告破徽章  ★", 18, COL_DARK,
            TextAlignmentOptions.Center, true);

        // 徽章弹入
        badge.transform.localScale = Vector3.zero;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            float s = t < 0.8f ? Mathf.Lerp(0f, 1.1f, t / 0.8f) : Mathf.Lerp(1.1f, 1f, (t - 0.8f) / 0.2f);
            badge.transform.localScale = Vector3.one * s;
            yield return null;
        }

        yield return new WaitForSeconds(1.8f);
        AudioManager.PlayCorrect();
        SceneTransitionManager.LoadScene("Chapter5_Museum");
    }

    // ══════════════════════════════════════════
    // 对话系统
    // ══════════════════════════════════════════
    void ShowDialogue(string[] lines, System.Action onFinish)
    {
        EnterDialogueMode();
        currentLines = lines; lineIndex = 0; lineCallback = onFinish;
        Hide(choicePanel); Show(dialoguePanel);
        DisplayLine();
    }

    void DisplayLine()
    {
        var parts = currentLines[lineIndex].Split('|');
        string spk = parts.Length > 1 ? parts[0].Trim() : "";
        string txt = parts.Length > 1 ? parts[1].Trim() : currentLines[lineIndex];
        if (speakerNameText != null) { speakerNameText.text = spk; ApplyFont(speakerNameText); }
        UpdateAvatar(spk);
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        fullText = txt; typingDone = false;
        if (contentText != null) { ApplyFont(contentText); typingCoroutine = StartCoroutine(TypeText(contentText, txt)); }
    }

    void UpdateAvatar(string spk)
    {
        if (avatarImage == null) return;
        Sprite sp = spk == "馆长" ? curatorAvatar : spk == "我" ? conanAvatar : spk == "保安" ? securityAvatar : null;
        avatarImage.sprite = sp;
        avatarImage.color = sp != null ? Color.white : new Color(0.1f, 0.15f, 0.35f, 0.8f);
    }

    IEnumerator TypeText(TextMeshProUGUI target, string text)
    {
        target.text = "";
        foreach (char c in text) { target.text += c; yield return new WaitForSeconds(0.04f); }
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
    // 选择题
    // ══════════════════════════════════════════
    void ShowChoice(string question, string[] opts, bool[] correct, System.Action<bool> cb)
    {
        currentCorrect = correct; choiceCallback = cb;
        Hide(dialoguePanel);
        if (choicePanel != null)
        {
            Show(choicePanel);
            if (choiceQuestionText != null) { choiceQuestionText.text = question; ApplyFont(choiceQuestionText); }
            if (choiceTexts != null)
                for (int i = 0; i < choiceTexts.Length && i < opts.Length; i++)
                    if (choiceTexts[i] != null) { choiceTexts[i].text = opts[i]; ApplyFont(choiceTexts[i]); }
        }
        else BuildFallbackChoice(question, opts, correct, cb);
    }

    void OnChoiceSelected(int idx)
    {
        if (currentCorrect == null || choiceCallback == null) return;
        bool ok = idx < currentCorrect.Length && currentCorrect[idx];
        if (ok) AudioManager.PlayCorrect(); else AudioManager.PlayWrong();
        Hide(choicePanel); isInDialogue = false;
        var cb = choiceCallback; currentCorrect = null; choiceCallback = null; cb(ok);
    }

    void BuildFallbackChoice(string question, string[] opts, bool[] correct, System.Action<bool> cb)
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        var panel = MakeImg("FC", canvas.transform,
            new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
            Vector2.zero, Vector2.zero, new Color(0.06f, 0.10f, 0.22f, 0.97f));
        MakeBorder(panel.gameObject, COL_GOLD, 3f);

        MakeTMP("Q", panel.transform,
            new Vector2(0.02f, 0.8f), new Vector2(0.98f, 1f),
            new Vector2(14, 0), new Vector2(-14, -8),
            question, 21, COL_GOLD, TextAlignmentOptions.MidlineLeft, true);

        float h = 0.68f / opts.Length - 0.02f;
        string[] lbl = { "A", "B", "C" };
        for (int i = 0; i < opts.Length; i++)
        {
            int idx = i; bool ok = correct[i];
            float yb = 0.77f - (i + 1) * (h + 0.02f);
            var card = MakeImg("C" + i, panel.transform,
                new Vector2(0.03f, yb), new Vector2(0.97f, yb + h),
                Vector2.zero, Vector2.zero, COL_CARDBG);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            var bc = btn.colors; bc.highlightedColor = COL_CARDH; btn.colors = bc;
            btn.onClick.AddListener(() => { Destroy(panel.gameObject); isInDialogue = false; cb(ok); });

            var lb = MakeImg("LB", card.transform,
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(48, 0), COL_GOLD);
            MakeTMP("T", lb.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, lbl[i], 24, COL_DARK, TextAlignmentOptions.Center, true);

            MakeTMP("OT", card.transform, Vector2.zero, Vector2.one,
                new Vector2(56, 6), new Vector2(-10, -6),
                opts[i], 18, COL_CREAM, TextAlignmentOptions.MidlineLeft, false);
        }
    }

    // ══════════════════════════════════════════
    // UI 工厂方法
    // ══════════════════════════════════════════
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

    void MakeBorder(GameObject parent, Color color, float thick)
    {
        void L(bool h, bool s)
        {
            var go = new GameObject("L"); go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            if (h) { rt.anchorMin = new Vector2(0, s ? 1 : 0); rt.anchorMax = new Vector2(1, s ? 1 : 0); rt.offsetMin = new Vector2(0, s ? -thick : 0); rt.offsetMax = new Vector2(0, s ? 0 : thick); }
            else   { rt.anchorMin = new Vector2(s ? 0 : 1, 0); rt.anchorMax = new Vector2(s ? 0 : 1, 1); rt.offsetMin = new Vector2(s ? 0 : -thick, 0); rt.offsetMax = new Vector2(s ? thick : 0, 0); }
            go.AddComponent<Image>().color = color;
        }
        L(true, true); L(true, false); L(false, true); L(false, false);
    }

    void MakeLine(GameObject parent, bool horizontal, bool start, Color color, float thick)
    {
        var go = new GameObject("Line"); go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        if (horizontal) { rt.anchorMin = new Vector2(0, start ? 1 : 0); rt.anchorMax = new Vector2(1, start ? 1 : 0); rt.offsetMin = new Vector2(0, start ? -thick : 0); rt.offsetMax = new Vector2(0, start ? 0 : thick); }
        else            { rt.anchorMin = new Vector2(start ? 0 : 1, 0); rt.anchorMax = new Vector2(start ? 0 : 1, 1); rt.offsetMin = new Vector2(start ? 0 : -thick, 0); rt.offsetMax = new Vector2(start ? thick : 0, 0); }
        go.AddComponent<Image>().color = color;
    }

    GameObject MakeGO(string name, Transform parent)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }

    void Show(GameObject go) { if (go != null) go.SetActive(true); }
    void Hide(GameObject go) { if (go != null) go.SetActive(false); }
    void ApplyFont(TextMeshProUGUI t) { if (chineseFont != null) t.font = chineseFont; }
}
