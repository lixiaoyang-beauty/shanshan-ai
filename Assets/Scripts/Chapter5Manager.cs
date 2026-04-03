using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Chapter5Manager - 案件回顾章节
/// 流程：回顾对话 → 情景应用题 → 连线配对题 → 扩展知识 → 结束
/// UI结构：DialoguePanel / ChoicePanel / KnowledgePanel / EndingPanel
/// </summary>
public class Chapter5Manager : MonoBehaviour
{
    [Header("── 对话框（复用第四章）──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 选择题面板（复用第四章）──")]
    public GameObject choicePanel;
    public TextMeshProUGUI choiceQuestionText;
    public Button[] choiceButtons;
    public TextMeshProUGUI[] choiceTexts;

    [Header("── 扩展知识面板 ──")]
    public GameObject knowledgePanel;
    public Button knowledgeContinueBtn;  // KnowledgePanel里的按钮

    [Header("── 结束面板（含勋章）──")]
    public GameObject endingPanel;
    public Button endingBtn;
    public GameObject endingJiesuan;    // EndingJiesuan面板
    public Button turnButton;           // 返回首页按钮

    [Header("── 头像 ──")]
    public Sprite allyAvatar;    // 艾莉博士
    public Sprite conanAvatar;   // 柯南（选择题答对反馈用）

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    [Header("── 鼠标视角脚本（如果有）──")]
    public MonoBehaviour mouseLookScript;

    // 状态
    private bool isInDialogue = false;
    private string[] currentLines;
    private int lineIndex;
    private System.Action lineCallback;
    private Coroutine typingCoroutine;
    private bool typingDone = false;
    private string fullText = "";
    private bool[] currentCorrect;
    private System.Action<bool> choiceCallback;

    // 颜色（fallback用）
    static readonly Color NAVY   = new Color(0.04f, 0.08f, 0.21f, 1f);
    static readonly Color GOLD   = new Color(0.72f, 0.53f, 0.04f, 1f);
    static readonly Color GOLDF  = new Color(0.72f, 0.53f, 0.04f, 0.18f);
    static readonly Color GOLDB  = new Color(0.72f, 0.53f, 0.04f, 0.40f);
    static readonly Color DIVD   = new Color(0.72f, 0.53f, 0.04f, 0.30f);
    static readonly Color CREAM  = new Color(0.96f, 0.93f, 0.82f, 1f);
    static readonly Color CREAM2 = new Color(0.75f, 0.70f, 0.55f, 1f);
    static readonly Color DARK   = new Color(0.08f, 0.05f, 0.01f, 1f);
    static readonly Color NAVY2  = new Color(0.07f, 0.13f, 0.28f, 1f);
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

        // 禁用鼠标视角脚本，禁止摄像机旋转
        if (mouseLookScript != null) mouseLookScript.enabled = false;

        Hide(dialoguePanel);
        Hide(choicePanel);
        Hide(knowledgePanel);
        Hide(endingPanel);
        Hide(endingJiesuan);

        // 绑定继续按钮
        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }

        // 绑定选择题按钮
        if (choiceButtons != null)
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int idx = i;
                if (choiceButtons[i] != null)
                { choiceButtons[i].onClick.RemoveAllListeners(); choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(idx)); }
            }

        // 绑定知识拓展继续按钮
        if (knowledgeContinueBtn != null)
        {
            knowledgeContinueBtn.onClick.RemoveAllListeners();
            knowledgeContinueBtn.onClick.AddListener(OnKnowledgeContinue);
        }

        // 绑定结束按钮
        if (endingBtn != null)
        {
            endingBtn.onClick.RemoveAllListeners();
            endingBtn.onClick.AddListener(OnEndingBtnClicked);
        }

        // 绑定返回首页按钮
        if (turnButton != null)
        {
            turnButton.onClick.RemoveAllListeners();
            turnButton.onClick.AddListener(OnTurnToMenu);
        }
        else
        {
            // Fallback: 在endingJiesuan上找或创建返回按钮
            StartCoroutine(SetupTurnButtonFallback());
        }

        AudioManager.AddClickSound(continueButton);
        if (choiceButtons != null)
            foreach (var b in choiceButtons)
                AudioManager.AddClickSound(b);
        AudioManager.AddClickSound(knowledgeContinueBtn);
        AudioManager.AddClickSound(endingBtn);
        if (turnButton != null) AudioManager.AddClickSound(turnButton);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(StartChapter());
    }

    void Update()
    {
        if (isInDialogue && Input.GetKeyDown(KeyCode.Return))
            OnContinue();
    }

    // ══════════════════════════════════════════
    // 章节流程
    // ══════════════════════════════════════════
    IEnumerator StartChapter()
    {
        yield return new WaitForSeconds(0.8f);
        StartReviewDialogue();
    }

    // ── 回顾对话 ──
    void StartReviewDialogue()
    {
        ShowDialogue(new[]
        {
            "艾莉|柯南，你成功破解了消失的古币案，干得漂亮！",
            "艾莉|古币消失是因为光的全反射——光从水射向空气，角度超过临界角就会被完全反弹。",
            "艾莉|改变观察角度后，入射角变小，光线折射出水面，古币就重新出现了。",
            "艾莉|来，让我考考你，看你是否真的理解了这些光学原理！"
        }, ShowQuestion1);
    }

    // ══════════════════════════════════════════
    // 第1题：情景应用题
    // ══════════════════════════════════════════
    void ShowQuestion1()
    {
        ShowChoice(
            "潜水员从水下看水面，当视线角度超过临界角后，会看到什么？",
            new[] {
                "水面变成镜子，完全看不到水面以上的景象",
                "能看到变形的水面以上景象",
                "水面变得透明，看得更清楚"
            },
            new[] { true, false, false },
            ok => {
                if (ok)
                    ShowDialogue(new[] {
                        "艾莉|完全正确！超过临界角后，水面对潜水员来说就像一面镜子，全反射发生了！"
                    }, ShowQuestion2);
                else
                    ShowDialogue(new[] {
                        "艾莉|再想想——角度超过临界角，光会被完全反弹，潜水员还能看到外面吗？"
                    }, ShowQuestion1);
            });
    }

    // ══════════════════════════════════════════
    // 第2题：连线配对题
    // ══════════════════════════════════════════
    void ShowQuestion2()
    {
        ShowDialogue(new[] {
            "艾莉|最后一题！把下面的现象和对应的光学原理连线配对吧！"
        }, BuildMatchingQuestion);
    }

    void BuildMatchingQuestion()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var panel = new GameObject("MatchingPanel");
        panel.transform.SetParent(canvas.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.05f);
        rt.anchorMax = new Vector2(0.95f, 0.95f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = NAVY;
        MakeBorder(panel, GOLD, 3f);

        // 标题
        MakeTMP("Title", panel.transform,
            V2(0f,0.88f), V2(1f,1f), V2(20,4), V2(-20,-4),
            "连线配对：将左边现象拖到右边对应原理",
            20, GOLD, TextAlignmentOptions.Center, true);

        MakeImg("Div", panel.transform,
            V2(0.01f,0.875f), V2(0.99f,0.878f),
            V2(0,0), V2(0,0), DIVD);

        // 数据
        string[] phenomena  = { "海市蜃楼", "光纤通信", "钻石闪耀" };
        int[]    correctMap = { 0, 1, 2 };
        string[] slotLabels = { "大气折射", "全反射",   "全反射"   };
        float[]  yMids      = { 0.72f, 0.50f, 0.28f };
        float    cardH      = 0.14f;

        int totalPairs = phenomena.Length;
        int matchedCount = 0;

        // 右侧槽位
        var slots = new List<Ch5MatchSlot>();
        for (int i = 0; i < slotLabels.Length; i++)
        {
            float yBot = yMids[i] - cardH / 2f;
            var slotImg = MakeImg("RSlot_"+i, panel.transform,
                V2(0.58f, yBot), V2(0.95f, yBot+cardH),
                V2(4,4), V2(-4,-4), GOLDF);
            MakeBorder(slotImg.gameObject, GOLDB, 1.5f);
            MakeTMP("Label", slotImg.transform,
                V2(0,0), V2(1,1), V2(10,0), V2(-10,0),
                slotLabels[i], 16, CREAM2,
                TextAlignmentOptions.MidlineLeft, false);

            var slot = slotImg.gameObject.AddComponent<Ch5MatchSlot>();
            slot.slotIndex = i;
            slots.Add(slot);
        }

        // 左侧现象卡片
        for (int i = 0; i < phenomena.Length; i++)
        {
            float yBot = yMids[i] - cardH / 2f;
            var chipImg = MakeImg("LChip_"+i, panel.transform,
                V2(0.05f, yBot), V2(0.42f, yBot+cardH),
                V2(4,4), V2(-4,-4), CARDBG);
            MakeBorder(chipImg.gameObject, GOLD, 1.5f);
            MakeTMP("T", chipImg.transform,
                V2(0,0), V2(1,1), V2(8,0), V2(-8,0),
                phenomena[i], 18, GOLD,
                TextAlignmentOptions.Center, true);

            var drag = chipImg.gameObject.AddComponent<Ch5DragChip>();
            drag.correctPrincipleIndex = correctMap[i];
            drag.allSlots = slots;
            drag.onMatched = () =>
            {
                matchedCount++;
                if (matchedCount >= totalPairs)
                    StartCoroutine(MatchingSuccess(panel));
            };
        }

        // 中间箭头
        for (int i = 0; i < 3; i++)
            MakeTMP("Arrow_"+i, panel.transform,
                V2(0.43f, yMids[i]-0.05f), V2(0.57f, yMids[i]+0.05f),
                V2(0,0), V2(0,0),
                "→", 22, GOLDB, TextAlignmentOptions.Center, false);

        StartCoroutine(PopIn(rt));
    }

    IEnumerator MatchingSuccess(GameObject panel)
    {
        AudioManager.PlayStar();
        yield return new WaitForSeconds(0.6f);
        Destroy(panel);
        ShowDialogue(new[] {
            "艾莉|完美！海市蜃楼是大气折射，光纤通信和钻石都利用了全反射原理！",
            "艾莉|你已经完全掌握了光学原理在生活中的应用，真是出色的光学侦探！"
        }, ShowKnowledge);
    }

    // ══════════════════════════════════════════
    // 扩展知识面板
    // ══════════════════════════════════════════
    void ShowKnowledge()
    {
        if (knowledgePanel != null)
        {
            Show(knowledgePanel);
            StartCoroutine(FadeIn(knowledgePanel));
        }
        else
            BuildFallbackKnowledge();
    }

    void OnKnowledgeContinue()
    {
        Hide(knowledgePanel);
        ShowEnding();
    }

    // ══════════════════════════════════════════
    // 结束面板 → 结算面板
    // ══════════════════════════════════════════
    void ShowEnding()
    {
        Hide(dialoguePanel);
        Hide(choicePanel);
        Hide(knowledgePanel);
        AudioManager.PlayStar();
        if (endingPanel != null)
        {
            Show(endingPanel);
            StartCoroutine(PopIn(endingPanel.GetComponent<RectTransform>()));
        }
        else
            BuildFallbackEnding();
    }

    void OnEndingBtnClicked()
    {
        Debug.Log("OnEndingBtnClicked called! endingJiesuan=" + endingJiesuan);
        Hide(endingPanel);
        if (endingJiesuan != null)
        {
            Show(endingJiesuan);
            StartCoroutine(PopIn(endingJiesuan.GetComponent<RectTransform>()));
        }
    }

    void OnTurnToMenu()
    {
        Debug.Log("OnTurnToMenu called!");
        PlayerPrefs.DeleteAll();

        // 确保SceneTransitionManager存在
        if (FindObjectOfType<SceneTransitionManager>() == null)
        {
            var go = new GameObject("SceneTransitionManager");
            go.AddComponent<SceneTransitionManager>();
        }

        SceneTransitionManager.LoadScene("MainMenu");
    }

    System.Collections.IEnumerator SetupTurnButtonFallback()
    {
        // 等待endingJiesuan显示
        yield return new WaitUntil(() => endingJiesuan != null && endingJiesuan.activeSelf);
        yield return new WaitForSeconds(0.3f);

        // 在endingJiesuan上找返回按钮
        var existing = endingJiesuan.transform.Find("TurnToMenuBtn");
        if (existing != null)
        {
            turnButton = existing.GetComponent<Button>();
            if (turnButton != null)
            {
                turnButton.onClick.RemoveAllListeners();
                turnButton.onClick.AddListener(OnTurnToMenu);
                AudioManager.AddClickSound(turnButton);
                yield break;
            }
        }

        // 找不到则自己创建一个
        var btnGo = new GameObject("TurnToMenuBtn");
        btnGo.transform.SetParent(endingJiesuan.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.01f);
        rt.anchorMax = new Vector2(0.65f, 0.08f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = btnGo.AddComponent<Image>();
        img.color = GOLD;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnTurnToMenu);
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f, 0.82f, 0.25f, 1f);
        btn.colors = bc;
        AudioManager.AddClickSound(btn);

        var txt = new GameObject("T");
        txt.transform.SetParent(btnGo.transform, false);
        var tRT = txt.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = "返回首页";
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = DARK;
        tmp.alignment = TextAlignmentOptions.Center;
        if (chineseFont != null) tmp.font = chineseFont;

        turnButton = btn;
    }

    // ══════════════════════════════════════════
    // Fallback（没连线时自动创建）
    // ══════════════════════════════════════════
    void BuildFallbackKnowledge()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var panel = new GameObject("KnowledgeFallback");
        panel.transform.SetParent(canvas.transform, false);
        FillRect(panel);
        panel.AddComponent<Image>().color = NAVY;
        MakeBorder(panel, GOLD, 4f);

        MakeTMP("Title", panel.transform,
            V2(0f,0.88f), V2(1f,1f), V2(20,4), V2(-20,-4),
            "光学原理在生活中的应用", 24, GOLD,
            TextAlignmentOptions.Center, true);

        MakeImg("Div", panel.transform,
            V2(0.01f,0.875f), V2(0.99f,0.878f), V2(0,0), V2(0,0), DIVD);

        string[] titles     = { "海市蜃楼",   "光纤通信",             "钻石的闪耀"         };
        string[] principles = { "大气折射",   "全反射",               "全反射"             };
        string[] icons      = { "🌊",         "💡",                   "💎"                 };
        string[] bodies     = {
            "沙漠或海面上，光线经过不同密度的大气层发生折射，形成远处物体的虚像。",
            "光在玻璃纤维内全反射传播，以光速传输信号。现代互联网的基础！",
            "钻石临界角约24°，光在内部多次全反射后从顶部射出璀璨光芒。"
        };
        float[] xs = { 0.02f, 0.35f, 0.68f };

        for (int i = 0; i < 3; i++)
        {
            var card = MakeImg("Card_"+i, panel.transform,
                V2(xs[i],0.08f), V2(xs[i]+0.30f,0.87f),
                V2(4,4), V2(-4,-4), NAVY2);
            MakeBorder(card.gameObject, GOLDB, 1.5f);

            MakeTMP("Icon", card.transform,
                V2(0f,0.70f), V2(1f,0.96f), V2(0,0), V2(0,0),
                icons[i], 42, GOLD, TextAlignmentOptions.Center, false);

            MakeImg("Div2", card.transform,
                V2(0.05f,0.685f), V2(0.95f,0.688f), V2(0,0), V2(0,0), DIVD);

            MakeTMP("Title", card.transform,
                V2(0f,0.57f), V2(1f,0.685f), V2(8,0), V2(-8,0),
                titles[i], 17, GOLD, TextAlignmentOptions.Center, true);

            var pBg = MakeImg("PBg", card.transform,
                V2(0.08f,0.47f), V2(0.92f,0.57f), V2(0,2), V2(0,-2), GOLDF);
            MakeBorder(pBg.gameObject, GOLDB, 1f);
            MakeTMP("P", pBg.transform, V2(0,0),V2(1,1), V2(6,0),V2(-6,0),
                principles[i], 12, GOLD, TextAlignmentOptions.Center, false);

            MakeTMP("Body", card.transform,
                V2(0f,0.05f), V2(1f,0.47f), V2(10,6), V2(-10,-6),
                bodies[i], 14, CREAM, TextAlignmentOptions.TopLeft, false);
        }

        var btnGo = MakeImg("Btn", panel.transform,
            V2(0.35f,0.01f), V2(0.65f,0.075f), V2(0,2), V2(0,-2), GOLD);
        MakeBorder(btnGo.gameObject, new Color(1f,0.9f,0.5f,1f), 2f);
        MakeTMP("T", btnGo.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
            "我已了解，领取勋章 →", 15, DARK, TextAlignmentOptions.Center, true);
        var btn = btnGo.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnGo;
        btn.onClick.AddListener(() => { Destroy(panel); ShowEnding(); });

        StartCoroutine(FadeIn(panel));
    }

    void BuildFallbackEnding()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("EndingFallback");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0.02f,0.05f,0.15f,1f);

        MakeTMP("Title", overlay.transform,
            V2(0f,0.62f), V2(1f,0.82f), V2(40,0), V2(-40,0),
            "消失的古币\n全剧终", 42, GOLD,
            TextAlignmentOptions.Center, true);

        MakeTMP("Medal", overlay.transform,
            V2(0.35f,0.45f), V2(0.65f,0.62f), V2(0,0), V2(0,0),
            "★", 60, GOLD, TextAlignmentOptions.Center, false);

        MakeTMP("MedalTitle", overlay.transform,
            V2(0f,0.36f), V2(1f,0.46f), V2(20,0), V2(-20,0),
            "光学侦探勋章", 22, GOLD, TextAlignmentOptions.Center, true);

        MakeTMP("Sub", overlay.transform,
            V2(0.1f,0.20f), V2(0.9f,0.36f), V2(0,0), V2(0,0),
            "恭喜你掌握光的折射与全反射原理\n成为一名真正的光学侦探！",
            18, CREAM, TextAlignmentOptions.Center, false);

        var btnGo = MakeImg("EndBtn", overlay.transform,
            V2(0.3f,0.06f), V2(0.7f,0.16f), V2(0,2), V2(0,-2), GOLD);
        MakeBorder(btnGo.gameObject, new Color(1f,0.9f,0.5f,1f), 2f);
        MakeTMP("T", btnGo.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
            "结束游戏", 18, DARK, TextAlignmentOptions.Center, true);
        var btn = btnGo.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnGo;
        btn.onClick.AddListener(OnEndingBtnClicked);

        StartCoroutine(PopIn(overlay.GetComponent<RectTransform>()));
    }

    // ══════════════════════════════════════════
    // 对话系统
    // ══════════════════════════════════════════
    void ShowDialogue(string[] lines, System.Action onFinish)
    {
        isInDialogue = true;
        currentLines = lines; lineIndex = 0; lineCallback = onFinish;
        Hide(choicePanel);
        Show(dialoguePanel);
        DisplayLine();
    }

    void DisplayLine()
    {
        var parts  = currentLines[lineIndex].Split('|');
        string spk = parts.Length > 1 ? parts[0].Trim() : "";
        string txt = parts.Length > 1 ? parts[1].Trim() : currentLines[lineIndex];

        if (speakerNameText != null) { speakerNameText.text = spk; ApplyFont(speakerNameText); }
        if (avatarImage != null)
        {
            // 根据说话人切换头像
            Sprite sp = spk == "艾莉" ? allyAvatar : conanAvatar;
            avatarImage.sprite = sp;
            avatarImage.color  = sp != null ? Color.white : new Color(0.1f,0.15f,0.35f,0.8f);
        }
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        fullText = txt; typingDone = false;
        if (contentText != null) { ApplyFont(contentText); typingCoroutine = StartCoroutine(TypeText(contentText, txt)); }
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
        var panel = new GameObject("FC");
        panel.transform.SetParent(canvas.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.04f,0.08f); rt.anchorMax = new Vector2(0.96f,0.92f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.06f,0.10f,0.22f,0.97f);
        MakeBorder(panel, GOLD, 3f);

        MakeTMP("Q", panel.transform,
            V2(0.02f,0.80f), V2(0.98f,1f), V2(14,0), V2(-14,-8),
            question, 21, GOLD, TextAlignmentOptions.MidlineLeft, true);

        float h = 0.68f / opts.Length - 0.02f;
        string[] lbl = {"A","B","C"};
        for (int i = 0; i < opts.Length; i++)
        {
            int idx = i; bool ok = correct[i];
            float yb = 0.77f - (i+1)*(h+0.02f);
            var card = MakeImg("C"+i, panel.transform,
                V2(0.03f,yb), V2(0.97f,yb+h), V2(0,0), V2(0,0), CARDBG);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            var bc = btn.colors; bc.highlightedColor = CARDH; btn.colors = bc;
            btn.onClick.AddListener(()=>{ Destroy(panel); isInDialogue=false; cb(ok); });

            var lb = MakeImg("LB", card.transform, V2(0,0),V2(0,1), V2(0,0),V2(48,0), GOLD);
            MakeTMP("T", lb.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
                lbl[i], 24, DARK, TextAlignmentOptions.Center, true);
            MakeTMP("OT", card.transform, V2(0,0),V2(1,1), V2(56,6),V2(-10,-6),
                opts[i], 18, CREAM, TextAlignmentOptions.MidlineLeft, false);
        }
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
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
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

    IEnumerator FadeIn(GameObject panel)
    {
        var imgs = panel.GetComponentsInChildren<Image>(true);
        var txts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        // 先记录原始透明度
        var imgAlphas = new float[imgs.Length];
        var txtAlphas = new float[txts.Length];
        for (int i = 0; i < imgs.Length; i++) { imgAlphas[i] = imgs[i].color.a; var c = imgs[i].color; c.a = 0; imgs[i].color = c; }
        for (int i = 0; i < txts.Length; i++) { txtAlphas[i] = txts[i].color.a; var c = txts[i].color; c.a = 0; txts[i].color = c; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            for (int i = 0; i < imgs.Length; i++) { var c = imgs[i].color; c.a = Mathf.Lerp(0, imgAlphas[i], t); imgs[i].color = c; }
            for (int i = 0; i < txts.Length; i++) { var c = txts[i].color; c.a = Mathf.Lerp(0, txtAlphas[i], t); txts[i].color = c; }
            yield return null;
        }
    }
}

// ══════════════════════════════════════════
// 连线配对：可拖拽现象卡片
// ══════════════════════════════════════════
public class Ch5DragChip : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int correctPrincipleIndex;
    public List<Ch5MatchSlot> allSlots;
    public System.Action onMatched;

    [HideInInspector] public bool isMatched = false;

    private RectTransform rt;
    private Canvas rootCanvas;
    private Transform originalParent;
    private Vector2 origAnchorMin, origAnchorMax;
    private Vector2 origOffMin, origOffMax;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        originalParent  = transform.parent;
        origAnchorMin   = rt.anchorMin; origAnchorMax = rt.anchorMax;
        origOffMin      = rt.offsetMin; origOffMax    = rt.offsetMax;
    }

    void Start()
    {
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (isMatched) return;
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(160, 55);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 local);
        rt.anchoredPosition = local;
        GetComponent<Image>().color = new Color(0.72f, 0.53f, 0.04f, 0.9f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (isMatched || rootCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 local);
        rt.anchoredPosition = local;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (isMatched) return;
        GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.26f, 1f);
        bool dropped = false;
        foreach (var slot in allSlots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                slot.GetComponent<RectTransform>(), e.position, e.pressEventCamera))
            { slot.TryReceive(this); dropped = true; break; }
        }
        if (!dropped) ReturnToPool();
    }

    public void MatchSuccess(RectTransform slotRt)
    {
        isMatched = true;
        transform.SetParent(slotRt, false);
        rt.anchorMin = new Vector2(0.02f, 0.1f);
        rt.anchorMax = new Vector2(0.98f, 0.9f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(0.06f, 0.28f, 0.12f, 0.85f);
        onMatched?.Invoke();
    }

    public void ReturnToPool()
    {
        transform.SetParent(originalParent, false);
        rt.anchorMin = origAnchorMin; rt.anchorMax = origAnchorMax;
        rt.offsetMin = origOffMin;    rt.offsetMax = origOffMax;
        GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.26f, 1f);
    }
}

// ══════════════════════════════════════════
// 连线配对：原理槽位
// ══════════════════════════════════════════
public class Ch5MatchSlot : MonoBehaviour
{
    public int slotIndex;
    private bool isFilled = false;

    public void TryReceive(Ch5DragChip chip)
    {
        if (isFilled) { chip.ReturnToPool(); return; }

        if (chip.correctPrincipleIndex == slotIndex)
        {
            isFilled = true;
            chip.MatchSuccess(GetComponent<RectTransform>());
            GetComponent<Image>().color = new Color(0.06f, 0.28f, 0.12f, 0.4f);
            var lbl = transform.Find("Label");
            if (lbl != null) lbl.gameObject.SetActive(false);
        }
        else
            StartCoroutine(FlashRed(chip));
    }

    IEnumerator FlashRed(Ch5DragChip chip)
    {
        var img = GetComponent<Image>();
        Color orig = img.color;
        img.color = new Color(0.75f, 0.10f, 0.10f, 0.5f);
        yield return new WaitForSeconds(0.35f);
        img.color = orig;
        chip.ReturnToPool();
    }
}
