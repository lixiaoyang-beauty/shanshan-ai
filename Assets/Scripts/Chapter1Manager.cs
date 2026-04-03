using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Chapter1Manager : MonoBehaviour
{
    [Header("── 场景对象 ──")]
    public Camera mainCamera;
    public Transform curatorNPC;
    public Transform securityNPC;
    public Transform displayStand;

    [Header("── 对话框 ──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 提示UI（贯穿全程）──")]
    public GameObject hintUI;
    public TextMeshProUGUI hintText;

    [Header("── 开场委托面板 ──")]
    public GameObject missionPanel;   // MissionPanel，初始inactive
    public Button acceptMissionBtn;   // AcceptBtn，透明按钮

    [Header("── 线索计数面板（常驻显示，不要取消勾选）──")]
    public GameObject cluePanel;
    public TextMeshProUGUI clueCountText;  // 显示"线索收集：x/3"

    [Header("── 线索汇总弹窗 ──")]
    public GameObject clueSummaryPanel;
    public Button nextChapterBtn;

    [Header("── 头像 ──")]
    public Sprite curatorAvatar;
    public Sprite securityAvatar;
    public Sprite conanAvatar;

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    [Header("── 移动设置 ──")]
    public float moveSpeed = 4f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 3f;

    // 状态
    private bool canMove = true;
    private bool isInDialogue = false;
    private float cameraPitch = 0f;
    private bool allCluesFound = false;

    // 线索收集状态（独立标记）
    private bool clueStand    = false;
    private bool clueCurator  = false;
    private bool clueSecurity = false;

    // 收集顺序记录（按顺序存）
    private List<string> collectedClues = new List<string>();

    // 是否已接受委托（接受前禁止所有交互）
    private bool missionAccepted = false;

    // 靠近目标
    private enum NearTarget { None, Stand, Curator, Security }
    private NearTarget nearTarget = NearTarget.None;

    // 对话状态
    private string[] currentLines;
    private int lineIndex;
    private System.Action lineCallback;
    private Coroutine typingCoroutine;
    private bool typingDone = false;
    private string fullText = "";

    // 运行时线索计数器（没有连线时自动创建）
    private GameObject runtimeClueCounter;
    private TextMeshProUGUI runtimeClueText;

    // 颜色
    static readonly Color NAVY   = new Color(0.04f, 0.08f, 0.21f, 1f);
    static readonly Color NAVY2  = new Color(0.07f, 0.13f, 0.28f, 1f);
    static readonly Color GOLD   = new Color(0.72f, 0.53f, 0.04f, 1f);
    static readonly Color GOLDF  = new Color(0.72f, 0.53f, 0.04f, 0.18f);
    static readonly Color GOLDB  = new Color(0.72f, 0.53f, 0.04f, 0.40f);
    static readonly Color CREAM  = new Color(0.96f, 0.93f, 0.82f, 1f);
    static readonly Color CREAM2 = new Color(0.75f, 0.70f, 0.55f, 1f);
    static readonly Color DARK   = new Color(0.08f, 0.05f, 0.01f, 1f);
    static readonly Color GREEN  = new Color(0.20f, 0.65f, 0.35f, 1f);
    static readonly Color CARDBG = new Color(0.08f, 0.13f, 0.26f, 1f);
    static readonly Color CARDH  = new Color(0.14f, 0.20f, 0.38f, 1f);

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
        Hide(hintUI);
        Hide(clueSummaryPanel);
        Hide(missionPanel);

        // CluePanel先隐藏，接受委托后显示
        Hide(cluePanel);
        UpdateClueUI();

        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }

        if (nextChapterBtn != null)
        { nextChapterBtn.onClick.RemoveAllListeners(); nextChapterBtn.onClick.AddListener(GoToChapter2); }

        if (acceptMissionBtn != null)
        { acceptMissionBtn.onClick.RemoveAllListeners(); acceptMissionBtn.onClick.AddListener(OnAcceptMission); }

        AudioManager.AddClickSound(continueButton);
        AudioManager.AddClickSound(nextChapterBtn);
        AudioManager.AddClickSound(acceptMissionBtn);

        // 先显示委托面板，暂不锁定鼠标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canMove = false;

        StartCoroutine(ShowMissionPanel());
    }

    // ══════════════════════════════════════════
    // 开场委托面板
    // ══════════════════════════════════════════
    IEnumerator ShowMissionPanel()
    {
        yield return new WaitForSeconds(0.6f);
        Show(missionPanel);
        // 弹出动画
        var rt = missionPanel != null ? missionPanel.GetComponent<RectTransform>() : null;
        if (rt != null) StartCoroutine(PopIn(rt));
    }

    void OnAcceptMission()
    {
        AudioManager.PlayClick();
        AudioManager.FadeOutBGM(3f);
        missionAccepted = true;
        Hide(missionPanel);
        Show(cluePanel);
        StartCoroutine(EnterScene());
    }

    // ══════════════════════════════════════════
    // 进场流程（接受委托后触发）
    // ══════════════════════════════════════════
    IEnumerator EnterScene()
    {
        // 锁定鼠标，开放移动
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        canMove = true;

        yield return new WaitForSeconds(0.3f);

        // 进场HintUI，语气自然
        ShowHint("线索就藏在博物馆里……走近感兴趣的地方，按 [ E ] 键调查！");
        yield return new WaitForSeconds(5f);
        Hide(hintUI);
    }

    // ══════════════════════════════════════════
    // UPDATE
    // ══════════════════════════════════════════
    void Update()
    {
        if (canMove && !isInDialogue)
        {
            HandleMovement();
            HandleMouseLook();
        }

        if (missionAccepted && !isInDialogue && !allCluesFound)
            CheckNearTargets();

        if (missionAccepted && !isInDialogue && !allCluesFound && Input.GetKeyDown(KeyCode.E))
            TryInteract();

        if (isInDialogue && Input.GetKeyDown(KeyCode.Return))
            OnContinue();
    }

    void HandleMovement()
    {
        if (mainCamera == null) return;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
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

    // ══════════════════════════════════════════
    // 靠近检测 + HintUI贯穿全程
    // ══════════════════════════════════════════
    void CheckNearTargets()
    {
        if (mainCamera == null) return;
        Vector3 camPos = mainCamera.transform.position;

        bool nearStand    = displayStand  != null && !clueStand    && Vector3.Distance(camPos, displayStand.position)  < interactDistance;
        bool nearCurator  = curatorNPC    != null && !clueCurator  && Vector3.Distance(camPos, curatorNPC.position)    < interactDistance;
        bool nearSecurity = securityNPC   != null && !clueSecurity && Vector3.Distance(camPos, securityNPC.position)   < interactDistance;

        if (nearStand)
        {
            nearTarget = NearTarget.Stand;
            ShowHint("[ E ] 检查展台");
        }
        else if (nearCurator)
        {
            nearTarget = NearTarget.Curator;
            ShowHint("[ E ] 询问馆长");
        }
        else if (nearSecurity)
        {
            nearTarget = NearTarget.Security;
            ShowHint("[ E ] 询问保安");
        }
        else
        {
            nearTarget = NearTarget.None;
            // 没有靠近任何目标时，根据已收集情况给出引导
            int count = collectedClues.Count;
            if (count == 0)
                ShowHint("走近展台、馆长或保安，按 [ E ] 收集线索！");
            else if (count < 3)
                ShowHint("继续调查！还有 " + (3 - count) + " 条线索未收集");
            else
                Hide(hintUI);
        }
    }

    void TryInteract()
    {
        switch (nearTarget)
        {
            case NearTarget.Stand:    InteractStand();    break;
            case NearTarget.Curator:  InteractCurator();  break;
            case NearTarget.Security: InteractSecurity(); break;
        }
    }

    // ══════════════════════════════════════════
    // 三个交互（收集顺序动态记录）
    // ══════════════════════════════════════════
    void InteractStand()
    {
        AudioManager.PlayClick();
        AudioManager.PlayCorrect();
        Hide(hintUI);
        EnterDialogueMode();
        ShowDialogue(new[]
        {
            "我|这是放置古币的展示台……",
            "我|展台上有水渍痕迹，古币是放在装水的玻璃杯里展示的。",
            "我|【新线索】展台上有水渍痕迹！"
        }, () => {
            clueStand = true;
            collectedClues.Add("展台上有水渍痕迹");
            UpdateClueUI();
            ExitDialogueMode();
            CheckAllClues();
        });
    }

    void InteractCurator()
    {
        AudioManager.PlayClick();
        AudioManager.PlayCorrect();
        Hide(hintUI);
        EnterDialogueMode();
        ShowDialogue(new[]
        {
            "馆长|小侦探，你来调查古币失踪案？",
            "馆长|那枚古币昨天下午还好好的，我亲眼看到它在展示杯里。",
            "馆长|今天早上开馆，王大叔来报告说古币不见了，真是奇怪……",
            "我|【新线索】馆长说古币昨天还在！"
        }, () => {
            clueCurator = true;
            collectedClues.Add("馆长说古币昨天还在");
            UpdateClueUI();
            ExitDialogueMode();
            CheckAllClues();
        });
    }

    void InteractSecurity()
    {
        AudioManager.PlayClick();
        AudioManager.PlayCorrect();
        Hide(hintUI);
        EnterDialogueMode();
        ShowDialogue(new[]
        {
            "保安|我就是王大叔！这事太奇怪了……",
            "保安|我昨晚巡逻时，路过展台，从侧面一看，古币不见了！",
            "保安|我以为自己眼花了，但展台上什么都没动过，监控也没有异常。",
            "我|【新线索】保安说从侧面看古币消失了！"
        }, () => {
            clueSecurity = true;
            collectedClues.Add("保安说从侧面看古币消失了");
            UpdateClueUI();
            ExitDialogueMode();
            CheckAllClues();
        });
    }

    // ══════════════════════════════════════════
    // 检查是否全部收集
    // ══════════════════════════════════════════
    void CheckAllClues()
    {
        if (!clueStand || !clueCurator || !clueSecurity) return;
        allCluesFound = true;
        Hide(hintUI);
        StartCoroutine(ShowSummaryDelayed());
    }

    IEnumerator ShowSummaryDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (clueSummaryPanel != null)
        {
            Show(clueSummaryPanel);
            StartCoroutine(PopIn(clueSummaryPanel.GetComponent<RectTransform>()));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
            BuildFallbackSummary();
    }

    void GoToChapter2()
    {
        AudioManager.PlayCorrect();
        SceneTransitionManager.LoadScene("Chapter2_Lab");
    }

    // ══════════════════════════════════════════
    // 线索计数UI
    // ══════════════════════════════════════════
    void UpdateClueUI()
    {
        int count = collectedClues.Count;
        string txt = "线索收集：" + count + " / 3";

        if (clueCountText != null)
        {
            clueCountText.text = txt;
            ApplyFont(clueCountText);
        }
        else
            UpdateOrCreateClueCounter(txt);
    }

    void UpdateOrCreateClueCounter(string txt)
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (runtimeClueCounter == null)
        {
            runtimeClueCounter = new GameObject("ClueCounter");
            runtimeClueCounter.transform.SetParent(canvas.transform, false);
            var rt = runtimeClueCounter.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(180, 44);
            rt.anchoredPosition = new Vector2(100, -28);
            runtimeClueCounter.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.21f, 0.88f);
            MakeBorder(runtimeClueCounter, GOLD, 1.5f);

            var tGo = new GameObject("T");
            tGo.transform.SetParent(runtimeClueCounter.transform, false);
            var tRt = tGo.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            runtimeClueText = tGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(runtimeClueText);
            runtimeClueText.fontSize = 15;
            runtimeClueText.fontStyle = FontStyles.Bold;
            runtimeClueText.color = GOLD;
            runtimeClueText.alignment = TextAlignmentOptions.Center;
        }

        if (runtimeClueText != null)
            runtimeClueText.text = txt;
    }

    // ══════════════════════════════════════════
    // Fallback线索汇总弹窗
    // ══════════════════════════════════════════
    void BuildFallbackSummary()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var overlay = new GameObject("ClueSummary");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRt = card.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.1f, 0.1f);
        cRt.anchorMax = new Vector2(0.9f, 0.93f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = NAVY;
        MakeBorder(card, GOLD, 3f);

        MakeTMP("Title", card.transform,
            V2(0f, 0.87f), V2(1f, 1f), V2(20, 4), V2(-20, -4),
            "恭喜你，小侦探！线索收集完成！",
            20, GOLD, TextAlignmentOptions.Center, true);

        MakeImg("Div", card.transform,
            V2(0.02f, 0.863f), V2(0.98f, 0.866f),
            V2(0,0), V2(0,0), new Color(0.72f, 0.53f, 0.04f, 0.4f));

        // 按收集顺序显示线索
        float[] yBots = { 0.60f, 0.44f, 0.28f };
        for (int i = 0; i < collectedClues.Count && i < 3; i++)
        {
            var cc = new GameObject("Clue" + i);
            cc.transform.SetParent(card.transform, false);
            var ccRt = cc.AddComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.03f, yBots[i]);
            ccRt.anchorMax = new Vector2(0.97f, yBots[i] + 0.14f);
            ccRt.offsetMin = ccRt.offsetMax = Vector2.zero;
            cc.AddComponent<Image>().color = GOLDF;
            MakeBorder(cc, GOLDB, 1f);

            MakeTMP("Check", cc.transform,
                V2(0f,0f), V2(0.08f,1f), V2(0,0), V2(0,0),
                "✓", 20, GREEN, TextAlignmentOptions.Center, true);

            MakeTMP("Text", cc.transform,
                V2(0.09f,0f), V2(1f,1f), V2(8,4), V2(-8,-4),
                "线索" + (i+1) + "：" + collectedClues[i],
                14, CREAM, TextAlignmentOptions.MidlineLeft, false);
        }

        // 勋章
        MakeTMP("Medal", card.transform,
            V2(0.3f,0.13f), V2(0.7f,0.27f), V2(0,0), V2(0,0),
            "★  现场勘察勋章  ★",
            16, GOLD, TextAlignmentOptions.Center, true);

        // 提示
        MakeTMP("Hint", card.transform,
            V2(0.05f,0.07f), V2(0.95f,0.13f), V2(0,0), V2(0,0),
            "去实验室，探究古币消失的科学原理！",
            13, CREAM2, TextAlignmentOptions.Center, false);

        // 按钮
        var btnGo = new GameObject("NextBtn");
        btnGo.transform.SetParent(card.transform, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.25f, 0.01f);
        bRt.anchorMax = new Vector2(0.75f, 0.08f);
        bRt.offsetMin = bRt.offsetMax = Vector2.zero;
        var bImg = btnGo.AddComponent<Image>(); bImg.color = GOLD;
        MakeBorder(btnGo, new Color(1f,0.9f,0.5f,1f), 2f);
        var btn = btnGo.AddComponent<Button>(); btn.targetGraphic = bImg;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f,0.82f,0.25f,1f);
        btn.colors = bc;
        btn.onClick.AddListener(GoToChapter2);
        MakeTMP("T", btnGo.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
            "前往实验室分析 →", 15, DARK, TextAlignmentOptions.Center, true);

        StartCoroutine(PopIn(cRt));
    }

    // ══════════════════════════════════════════
    // 对话系统
    // ══════════════════════════════════════════
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

    void ExitDialogueMode()
    {
        isInDialogue = false; canMove = true;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    void ShowDialogue(string[] lines, System.Action onFinish)
    {
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
            Sprite sp = spk == "馆长" ? curatorAvatar :
                        spk == "保安" ? securityAvatar :
                        spk == "我"   ? conanAvatar : null;
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
        { Hide(dialoguePanel); lineCallback?.Invoke(); }
        else DisplayLine();
    }

    // ══════════════════════════════════════════
    // UI工具
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

    IEnumerator PopIn(RectTransform rt)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero; float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            float s = t < 0.75f ? Mathf.Lerp(0f,1.1f,t/0.75f) : Mathf.Lerp(1.1f,1f,(t-0.75f)/0.25f);
            rt.localScale = Vector3.one * s; yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
