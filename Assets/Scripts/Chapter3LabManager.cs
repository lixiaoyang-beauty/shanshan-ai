using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Chapter3Manager
/// 负责：开场对话 → 颁发光路追踪眼镜道具 → 跳转Chapter3_Experiment
/// </summary>
public class Chapter3Manager : MonoBehaviour
{
    [Header("── 对话框 ──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 提示UI ──")]
    public GameObject hintUI;
    public TextMeshProUGUI hintText;

    [Header("── 道具面板 ──")]
    public GameObject itemPanel;       // 光路追踪眼镜道具面板
    public Button itemConfirmBtn;      // "收下"按钮（透明，盖在图片按钮上）

    [Header("── 头像 ──")]
    public Sprite allyAvatar;
    public Sprite conanAvatar;

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    // 对话状态
    private bool isInDialogue = false;
    private string[] currentLines;
    private int lineIndex;
    private System.Action lineCallback;
    private Coroutine typingCoroutine;
    private bool typingDone = false;
    private string fullText = "";

    // 颜色
    static readonly Color NAVY  = new Color(0.04f,0.08f,0.21f,1f);
    static readonly Color GOLD  = new Color(0.72f,0.53f,0.04f,1f);
    static readonly Color GOLDB = new Color(0.72f,0.53f,0.04f,0.4f);
    static readonly Color CYAN  = new Color(0f,0.82f,1f,1f);
    static readonly Color CREAM = new Color(0.96f,0.93f,0.82f,1f);
    static readonly Color DARK  = new Color(0.08f,0.05f,0.01f,1f);

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
        Hide(itemPanel);

        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }

        if (itemConfirmBtn != null)
        { itemConfirmBtn.onClick.RemoveAllListeners(); itemConfirmBtn.onClick.AddListener(OnItemConfirmed); }

        AudioManager.AddClickSound(continueButton);
        AudioManager.AddClickSound(itemConfirmBtn);

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
    // 开场流程
    // ══════════════════════════════════════════
    IEnumerator StartChapter()
    {
        yield return new WaitForSeconds(0.8f);
        ShowDialogue(new[]
        {
            "艾莉|柯南！你在这里啊，我刚从外面回来。",
            "艾莉|我听说博物馆的古币失踪案……你来找我是对的！",
            "我|艾莉博士！我在实验室发现了奇怪的现象！",
            "我|从侧面看古币消失，从上面看又出现了，这到底是为什么？",
            "艾莉|哦？你已经亲自观察到了！这正是光学最神奇的现象之一。",
            "艾莉|来，我先送你一样东西……"
        }, ShowItemPanel);
    }

    // ── 道具颁发 ──
    void ShowItemPanel()
    {
        if (itemPanel != null)
        {
            Show(itemPanel);
            StartCoroutine(PopIn(itemPanel.GetComponent<RectTransform>()));
        }
        else
            BuildFallbackItemPanel();
    }

    void OnItemConfirmed()
    {
        Hide(itemPanel);
        ShowDialogue(new[]
        {
            "艾莉|这是我研制的光路追踪眼镜，戴上它你能看见光线的实际路径！",
            "我|哇……真的能看见光线？！",
            "艾莉|当然！我带你去光影实验室，那里有专门的实验台。",
            "艾莉|亲眼看看光线在水面发生了什么，你就能明白古币消失的秘密！",
            "我|太好了，走！"
        }, GoToExperiment);
    }

    void GoToExperiment()
    {
        SceneTransitionManager.LoadScene("Chapter3_Experiment");
    }

    // ── Fallback道具面板 ──
    void BuildFallbackItemPanel()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("ItemOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0,0,0,0.7f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRt = card.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.2f,0.25f);
        cRt.anchorMax = new Vector2(0.8f,0.82f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = NAVY;
        MakeBorder(card, GOLD, 3f);

        MakeTMP("Title", card.transform,
            V2(0f,0.82f), V2(1f,1f), V2(20,5), V2(-20,-5),
            "获得道具！", 24, GOLD,
            TextAlignmentOptions.Center, true);

        MakeImg("Div", card.transform,
            V2(0.02f,0.815f), V2(0.98f,0.818f),
            V2(0,0), V2(0,0), GOLDB);

        MakeTMP("Icon", card.transform,
            V2(0f,0.50f), V2(1f,0.815f), V2(0,0), V2(0,0),
            "🔭", 56, CYAN, TextAlignmentOptions.Center, false);

        MakeTMP("Name", card.transform,
            V2(0f,0.35f), V2(1f,0.50f), V2(20,0), V2(-20,0),
            "光路追踪眼镜", 22, CREAM,
            TextAlignmentOptions.Center, true);

        MakeTMP("Desc", card.transform,
            V2(0f,0.18f), V2(1f,0.35f), V2(24,0), V2(-24,0),
            "戴上它能看见光线的实际传播路径！",
            15, new Color(0.75f,0.70f,0.55f,1f),
            TextAlignmentOptions.Center, false);

        var btnGo = new GameObject("Btn");
        btnGo.transform.SetParent(card.transform, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.25f,0.04f);
        bRt.anchorMax = new Vector2(0.75f,0.15f);
        bRt.offsetMin = bRt.offsetMax = Vector2.zero;
        var bImg = btnGo.AddComponent<Image>(); bImg.color = GOLD;
        MakeBorder(btnGo, new Color(1f,0.9f,0.5f,1f), 2f);
        var btn = btnGo.AddComponent<Button>(); btn.targetGraphic = bImg;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f,0.82f,0.25f,1f);
        btn.colors = bc;
        btn.onClick.AddListener(() => { Destroy(overlay); OnItemConfirmed(); });
        MakeTMP("T", btnGo.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
            "收下！", 18, DARK, TextAlignmentOptions.Center, true);

        StartCoroutine(PopIn(cRt));
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
