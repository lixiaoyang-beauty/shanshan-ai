using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// 挂在 CaseSummaryPanel 上（stretch x stretch铺满Canvas）
/// Manager槽位连GameManager，或运行时自动找
/// </summary>
public class CaseSummaryBuilder : MonoBehaviour
{
    [Header("连线")]
    public TMP_FontAsset chineseFont;
    public Chapter4Manager manager;

    static readonly Color NAVY   = new Color(0.04f, 0.08f, 0.21f, 1f);
    static readonly Color NAVY2  = new Color(0.07f, 0.13f, 0.28f, 1f);
    static readonly Color GOLD   = new Color(0.72f, 0.53f, 0.04f, 1f);
    static readonly Color GOLDF  = new Color(0.72f, 0.53f, 0.04f, 0.18f);
    static readonly Color GOLDB  = new Color(0.72f, 0.53f, 0.04f, 0.40f);
    static readonly Color DIVD   = new Color(0.72f, 0.53f, 0.04f, 0.30f);
    static readonly Color CREAM  = new Color(0.96f, 0.93f, 0.82f, 1f);
    static readonly Color CREAM2 = new Color(0.75f, 0.70f, 0.55f, 1f);
    static readonly Color DARK   = new Color(0.08f, 0.05f, 0.01f, 1f);
    static readonly Color GREEN  = new Color(0.20f, 0.65f, 0.35f, 1f);
    static readonly Color CARDBG = new Color(0.08f, 0.13f, 0.26f, 1f);
    static readonly Color CARDH  = new Color(0.14f, 0.20f, 0.38f, 1f);

    private GameObject signRow;
    private GameObject stampBtn;

    void Start()
    {
        if (manager == null)
            manager = FindObjectOfType<Chapter4Manager>();
    }

    public void Build()
    {
        // 自动找manager
        if (manager == null)
            manager = FindObjectOfType<Chapter4Manager>();

        // 清空旧内容
        foreach (Transform c in transform) Destroy(c.gameObject);

        // 遮罩
        Img("Overlay", transform,
            V2(0,0), V2(1,1), V2(0,0), V2(0,0),
            new Color(0f,0f,0f,0.60f));

        // 主卡片
        var card = Img("Card", transform,
            V2(0.06f,0.03f), V2(0.94f,0.97f),
            V2(0,0), V2(0,0), NAVY);
        Border(card.gameObject, GOLD, 3f);
        Transform C = card.transform;

        // ── 1. 标题栏 ──
        var titleBar = Img("TitleBar", C,
            V2(0f,0.88f), V2(1f,1f), V2(0,0), V2(0,0), NAVY2);
        Line(titleBar.gameObject, true, false, GOLD, 2f);
        TMP("Sub",   titleBar.transform, V2(0f,0.52f), V2(1f,1f),   V2(0,3),  V2(0,-3),
            "CASE FILE  ·  第四章", 11, GOLD, TextAlignmentOptions.Center, false);
        TMP("Title", titleBar.transform, V2(0f,0f),    V2(1f,0.52f), V2(0,3),  V2(0,-3),
            "消失的古币  ·  结案报告", 20, CREAM, TextAlignmentOptions.Center, true);

        // ── 2. 案件信息 ──
        InfoCard("InfoNum",  C, V2(0.02f,0.74f), V2(0.49f,0.87f), "案件编号", "CASE-004",   false);
        InfoCard("InfoName", C, V2(0.51f,0.74f), V2(0.98f,0.87f), "案件名称", "消失的古币", false);
        Img("Div1",C, V2(0.01f,0.733f),V2(0.99f,0.736f),V2(0,0),V2(0,0),DIVD);

        // ── 3. 分析标签 ──
        TMP("AnalLabel", C, V2(0.02f,0.690f), V2(0.98f,0.733f), V2(8,0), V2(-8,0),
            "案件分析", 11, GOLD, TextAlignmentOptions.MidlineLeft, false);

        // ── 4. 填空行 ──
        var slot1 = FillRow("Row1", C,
            V2(0.01f,0.570f), V2(0.99f,0.685f),
            "消失原因", "古币消失是因为光发生了", "", "answer1");
        var slot2 = FillRow("Row2", C,
            V2(0.01f,0.450f), V2(0.99f,0.565f),
            "重现原因", "改变角度后光发生", "使古币重现", "answer2");
        Img("Div2",C, V2(0.01f,0.444f),V2(0.99f,0.447f),V2(0,0),V2(0,0),DIVD);

        // ── 5. 词语碎片池 ──
        TMP("WordLabel", C, V2(0.02f,0.400f), V2(0.98f,0.443f), V2(8,0), V2(-8,0),
            "▼  将词语拖入上方空格", 11, GOLD, TextAlignmentOptions.MidlineLeft, false);

        string[] words    = { "全反射", "折射", "反射", "散射" };
        bool[]   correct1 = { true,     false,  false,  false  };
        bool[]   correct2 = { false,    true,   false,  false  };
        float chipW = 0.19f, chipGap = 0.005f;
        float startX = (1f - (chipW * 4 + chipGap * 3)) / 2f;
        for (int i = 0; i < 4; i++)
        {
            float x0 = startX + i * (chipW + chipGap);
            WordChip(words[i], C,
                V2(x0, 0.305f), V2(x0 + chipW, 0.397f),
                correct1[i], correct2[i]);
        }
        Img("Div3",C, V2(0.01f,0.299f),V2(0.99f,0.302f),V2(0,0),V2(0,0),DIVD);

        // ── 6. 签名区（初始隐藏）──
        signRow = new GameObject("SignRow");
        signRow.transform.SetParent(C, false);
        var srRt = signRow.AddComponent<RectTransform>();
        srRt.anchorMin = V2(0.01f, 0.165f);
        srRt.anchorMax = V2(0.99f, 0.295f);
        srRt.offsetMin = srRt.offsetMax = V2(0,0);
        signRow.AddComponent<Image>().color = new Color(0,0,0,0);
        InfoCard("Sign1", signRow.transform, V2(0f,0f),    V2(0.48f,1f), "侦探签名", "Conan",   false);
        InfoCard("Sign2", signRow.transform, V2(0.52f,0f), V2(1f,1f),   "结论",     "案件告破", true);
        signRow.SetActive(false);

        // ── 7. 盖章按钮（初始隐藏）──
        stampBtn = BuildStampBtn(C, V2(0.22f,0.045f), V2(0.78f,0.150f));
        stampBtn.SetActive(false);

        // ── DropSlot 连线 ──
        var ds1 = slot1.AddComponent<DropSlot>();
        ds1.slotId = "answer1";
        var ds2 = slot2.AddComponent<DropSlot>();
        ds2.slotId = "answer2";
        ds1.other = ds2;       ds2.other = ds1;
        ds1.stampButton = stampBtn; ds2.stampButton = stampBtn;
        ds1.signRow = signRow; ds2.signRow = signRow;
        ds1.manager = manager; ds2.manager = manager;
    }

    // ══════════════════════════════════════════
    // 填空行
    // ══════════════════════════════════════════
    GameObject FillRow(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        string label, string prefix, string suffix, string slotId)
    {
        var row = Img(name, parent, ancMin, ancMax,
            V2(4,3), V2(-4,-3), new Color(1,1,1,0.03f));
        Line(row.gameObject, true, true,  DIVD, 1f);
        Line(row.gameObject, true, false, DIVD, 1f);

        var lbBg = Img("LabelBg", row.transform,
            V2(0f,0f), V2(0.13f,1f), V2(0,0), V2(0,0), GOLDF);
        Border(lbBg.gameObject, GOLDB, 1f);
        TMP("LT", lbBg.transform, V2(0,0), V2(1,1), V2(4,0), V2(-4,0),
            label, 13, GOLD, TextAlignmentOptions.Center, true);

        Img("VSep", row.transform,
            V2(0.13f,0.1f), V2(0.132f,0.9f), V2(0,0), V2(0,0), GOLDB);

        TMP("Prefix", row.transform,
            V2(0.14f,0f), V2(0.50f,1f), V2(6,0), V2(0,0),
            prefix, 14, CREAM2, TextAlignmentOptions.MidlineLeft, false);

        var slot = Img("Slot_"+slotId, row.transform,
            V2(0.51f,0.10f), V2(0.71f,0.90f), V2(0,0), V2(0,0), GOLDF);
        Border(slot.gameObject, GOLDB, 1.5f);
        TMP("Hint", slot.transform, V2(0,0), V2(1,1), V2(3,0), V2(-3,0),
            "拖入词语", 12, new Color(0.72f,0.53f,0.04f,0.55f),
            TextAlignmentOptions.Center, false);

        if (!string.IsNullOrEmpty(suffix))
            TMP("Suffix", row.transform,
                V2(0.72f,0f), V2(1f,1f), V2(6,0), V2(-6,0),
                suffix, 14, CREAM2, TextAlignmentOptions.MidlineLeft, false);

        return slot.gameObject;
    }

    // ══════════════════════════════════════════
    // 信息卡片
    // ══════════════════════════════════════════
    void InfoCard(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        string label, string value, bool green)
    {
        var card = Img(name, parent, ancMin, ancMax, V2(4,3), V2(-4,-3), GOLDF);
        Border(card.gameObject, GOLDB, 1f);
        TMP("Label", card.transform, V2(0f,0.50f), V2(1f,1f),   V2(10,0), V2(-10,0),
            label, 10, GOLD, TextAlignmentOptions.MidlineLeft, false);
        TMP("Value", card.transform, V2(0f,0f),    V2(1f,0.50f), V2(10,0), V2(-10,0),
            value, 15, green ? GREEN : CREAM,
            TextAlignmentOptions.MidlineLeft, true);
    }

    // ══════════════════════════════════════════
    // 词语碎片
    // ══════════════════════════════════════════
    void WordChip(string word, Transform parent,
        Vector2 ancMin, Vector2 ancMax, bool c1, bool c2)
    {
        var chip = Img("Chip_"+word, parent,
            ancMin, ancMax, V2(3,3), V2(-3,-3), CARDBG);
        Border(chip.gameObject, GOLD, 1.5f);
        TMP("T", chip.transform, V2(0,0), V2(1,1), V2(4,0), V2(-4,0),
            word, 16, GOLD, TextAlignmentOptions.Center, true);

        var dw = chip.gameObject.AddComponent<DragWord>();
        dw.wordText = word;
        dw.isCorrectForSlot1 = c1;
        dw.isCorrectForSlot2 = c2;
    }

    // ══════════════════════════════════════════
    // 盖章按钮
    // ══════════════════════════════════════════
    GameObject BuildStampBtn(Transform parent, Vector2 ancMin, Vector2 ancMax)
    {
        var go = Img("StampBtn", parent, ancMin, ancMax, V2(0,0), V2(0,0), GOLD);
        Border(go.gameObject, new Color(1f,0.9f,0.5f,1f), 2f);
        TMP("T", go.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0),
            "盖章提交结案报告", 17, DARK, TextAlignmentOptions.Center, true);

        var btn = go.gameObject.AddComponent<Button>();
        btn.targetGraphic = go;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f,0.82f,0.25f,1f);
        bc.pressedColor     = new Color(0.55f,0.38f,0.02f,1f);
        btn.colors = bc;

        var capturedManager = manager;
        var capturedGO      = go.gameObject;
        btn.onClick.AddListener(() =>
        {
            if (capturedManager != null)
                capturedManager.StartCoroutine(
                    capturedManager.StampAndFinish(capturedGO, gameObject));
        });
        return go.gameObject;
    }

    // ══════════════════════════════════════════
    // 工具
    // ══════════════════════════════════════════
    Image Img(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI TMP(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax,
        Vector2 offMin, Vector2 offMax,
        string text, float fontSize, Color color,
        TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (chineseFont != null) tmp.font = chineseFont;
        tmp.text = text; tmp.fontSize = fontSize;
        tmp.color = color; tmp.alignment = align;
        tmp.enableWordWrapping = true;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    void Border(GameObject go, Color color, float thick)
    {
        void L(bool h, bool s)
        {
            var lg = new GameObject("L");
            lg.transform.SetParent(go.transform, false);
            var rt = lg.AddComponent<RectTransform>();
            if (h) { rt.anchorMin = V2(0,s?1:0); rt.anchorMax = V2(1,s?1:0); rt.offsetMin = V2(0,s?-thick:0); rt.offsetMax = V2(0,s?0:thick); }
            else   { rt.anchorMin = V2(s?0:1,0); rt.anchorMax = V2(s?0:1,1); rt.offsetMin = V2(s?0:-thick,0); rt.offsetMax = V2(s?thick:0,0); }
            lg.AddComponent<Image>().color = color;
        }
        L(true,true); L(true,false); L(false,true); L(false,false);
    }

    void Line(GameObject go, bool horiz, bool start, Color color, float thick)
    {
        var lg = new GameObject("Line");
        lg.transform.SetParent(go.transform, false);
        var rt = lg.AddComponent<RectTransform>();
        if (horiz) { rt.anchorMin = V2(0,start?1:0); rt.anchorMax = V2(1,start?1:0); rt.offsetMin = V2(0,start?-thick:0); rt.offsetMax = V2(0,start?0:thick); }
        else       { rt.anchorMin = V2(start?0:1,0); rt.anchorMax = V2(start?0:1,1); rt.offsetMin = V2(start?0:-thick,0); rt.offsetMax = V2(start?thick:0,0); }
        lg.AddComponent<Image>().color = color;
    }

    static Vector2 V2(float x, float y) => new Vector2(x, y);
}

// ══════════════════════════════════════════
// 拖拽：词语碎片
// ══════════════════════════════════════════
public class DragWord : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string wordText;
    public bool isCorrectForSlot1;
    public bool isCorrectForSlot2;
    [HideInInspector] public bool isPlaced = false;

    private RectTransform rt;
    private Canvas rootCanvas;
    private Transform originalParent;
    private Vector2 origAnchorMin, origAnchorMax;
    private Vector2 origOffMin, origOffMax;
    private Vector2 origSizeDelta;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        CacheOriginal();
    }

    void Start()
    {
        if (rootCanvas == null)
            rootCanvas = FindObjectOfType<Canvas>();
    }

    void CacheOriginal()
    {
        if (rt == null) return;
        originalParent  = transform.parent;
        origAnchorMin   = rt.anchorMin;
        origAnchorMax   = rt.anchorMax;
        origOffMin      = rt.offsetMin;
        origOffMax      = rt.offsetMax;
        origSizeDelta   = rt.sizeDelta;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (isPlaced) return;
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();

        // 记录当前世界坐标，移到Canvas顶层后保持位置
        Vector3 worldPos = transform.position;
        transform.SetParent(rootCanvas.transform, false);
        transform.SetAsLastSibling();

        // 切换为固定大小锚点，便于跟随鼠标
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(110, 48);
        // 把世界坐标转回Canvas本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 local);
        rt.anchoredPosition = local;

        GetComponent<Image>().color = new Color(0.72f, 0.53f, 0.04f, 0.9f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (isPlaced || rootCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            e.position, e.pressEventCamera, out Vector2 local);
        rt.anchoredPosition = local;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (isPlaced) return;
        GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.26f, 1f);

        // 检测是否在槽上
        bool dropped = false;
        foreach (var slot in FindObjectsOfType<DropSlot>())
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                slot.GetComponent<RectTransform>(), e.position, e.pressEventCamera))
            {
                slot.ReceiveWord(this);
                dropped = true;
                break;
            }
        }
        if (!dropped) ReturnToPool();
    }

    public void PlaceInSlot(RectTransform slotRt)
    {
        isPlaced = true;
        transform.SetParent(slotRt, false);
        rt.anchorMin = new Vector2(0.05f, 0.1f);
        rt.anchorMax = new Vector2(0.95f, 0.9f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(0.06f, 0.28f, 0.12f, 0.85f);
    }

    public void ReturnToPool()
    {
        isPlaced = false;
        transform.SetParent(originalParent, false);
        rt.anchorMin  = origAnchorMin;
        rt.anchorMax  = origAnchorMax;
        rt.offsetMin  = origOffMin;
        rt.offsetMax  = origOffMax;
        rt.sizeDelta  = origSizeDelta;
        GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.26f, 1f);
    }
}

// ══════════════════════════════════════════
// 拖拽：接收槽
// ══════════════════════════════════════════
public class DropSlot : MonoBehaviour
{
    public string slotId;
    public DropSlot other;
    public GameObject stampButton;
    public GameObject signRow;
    public Chapter4Manager manager;

    private DragWord placedWord;

    public bool IsFilled  => placedWord != null;
    public bool IsCorrect => placedWord != null &&
        (slotId == "answer1"
            ? placedWord.isCorrectForSlot1
            : placedWord.isCorrectForSlot2);

    public void ReceiveWord(DragWord word)
    {
        // 已有词语先退回
        if (placedWord != null)
        {
            placedWord.ReturnToPool();
            placedWord = null;
            GetComponent<Image>().color = new Color(0.72f, 0.53f, 0.04f, 0.18f);
        }

        bool correct = slotId == "answer1"
            ? word.isCorrectForSlot1
            : word.isCorrectForSlot2;

        if (correct)
        {
            placedWord = word;
            word.PlaceInSlot(GetComponent<RectTransform>());
            GetComponent<Image>().color = new Color(0.06f, 0.28f, 0.12f, 0.4f);
            var hint = transform.Find("Hint");
            if (hint != null) hint.gameObject.SetActive(false);
            CheckBoth();
        }
        else
        {
            StartCoroutine(FlashRed(word));
        }
    }

    void CheckBoth()
    {
        if (!IsFilled || !IsCorrect) return;
        if (other == null || !other.IsFilled || !other.IsCorrect) return;
        AudioManager.PlayStar();
        if (signRow    != null) StartCoroutine(PopIn(signRow));
        if (stampButton != null) StartCoroutine(PopIn(stampButton));
    }

    IEnumerator PopIn(GameObject go)
    {
        go.SetActive(true);
        var goRt = go.GetComponent<RectTransform>();
        if (goRt == null) yield break;
        goRt.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            float s = t < 0.75f
                ? Mathf.Lerp(0f, 1.1f, t / 0.75f)
                : Mathf.Lerp(1.1f, 1f, (t - 0.75f) / 0.25f);
            goRt.localScale = Vector3.one * s;
            yield return null;
        }
        goRt.localScale = Vector3.one;
    }

    IEnumerator FlashRed(DragWord word)
    {
        var img = GetComponent<Image>();
        Color orig = img.color;
        img.color = new Color(0.75f, 0.10f, 0.10f, 0.5f);
        yield return new WaitForSeconds(0.35f);
        img.color = orig;
        word.ReturnToPool();
    }
}
