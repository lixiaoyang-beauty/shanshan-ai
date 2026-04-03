using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// MainMenuManager
/// 挂在包含MainMenuPanel的场景的GameManager上
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("── 主菜单按钮 ──")]
    public Button startBtn;      // 开始游戏
    public Button introBtn;      // 游戏介绍
    public Button quitBtn;       // 退出游戏

    [Header("── 游戏介绍弹窗 ──")]
    public GameObject introPanel;
    public Button introCloseBtn; // 介绍面板里的关闭按钮

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    void Awake()
    {
        // 确保SceneTransitionManager存在（打包后可能找不到）
        if (FindObjectOfType<SceneTransitionManager>() == null)
        {
            var go = new GameObject("SceneTransitionManager");
            go.AddComponent<SceneTransitionManager>();
            Debug.Log("Created SceneTransitionManager");
        }
    }

    void Start()
    {
        Debug.Log("MainMenuManager Start begin");

        // 确保EventSystem存在（打包后可能缺少）
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 绑定按钮
        if (startBtn != null)
        {
            startBtn.onClick.RemoveAllListeners();
            startBtn.onClick.AddListener(OnStartGame);
            Debug.Log("startBtn bound");
        }
        else Debug.LogWarning("startBtn is null! Check Inspector.");

        if (introBtn != null)
        {
            introBtn.onClick.RemoveAllListeners();
            introBtn.onClick.AddListener(OnShowIntro);
        }
        else Debug.LogWarning("introBtn is null!");

        if (quitBtn != null)
        {
            quitBtn.onClick.RemoveAllListeners();
            quitBtn.onClick.AddListener(OnQuit);
        }
        else Debug.LogWarning("quitBtn is null!");

        if (introCloseBtn != null)
        {
            introCloseBtn.onClick.RemoveAllListeners();
            introCloseBtn.onClick.AddListener(OnCloseIntro);
        }

        AudioManager.AddClickSound(startBtn);
        AudioManager.AddClickSound(introBtn);
        AudioManager.AddClickSound(quitBtn);
        AudioManager.AddClickSound(introCloseBtn);

        // 确保介绍面板初始关闭
        if (introPanel != null)
            introPanel.SetActive(false);

        // 显示鼠标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AudioManager.PlayBGM();
        Debug.Log("MainMenuManager Start end");
    }

    // ── 开始游戏 ──
    void OnStartGame()
    {
        Debug.Log("OnStartGame called!");
        AudioManager.PlayClick();

        SceneTransitionManager.LoadScene("Chapter1_Museum");
    }

    // ── 游戏介绍 ──
    void OnShowIntro()
    {
        if (introPanel != null)
        {
            introPanel.SetActive(true);
            StartCoroutine(PopIn(introPanel.GetComponent<RectTransform>()));
        }
        else
            BuildFallbackIntro();
    }

    void OnCloseIntro()
    {
        if (introPanel != null)
            introPanel.SetActive(false);
    }

    // ── 退出游戏 ──
    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Fallback介绍面板（没有图片时自动创建）──
    void BuildFallbackIntro()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("IntroOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        var oRt = overlay.AddComponent<RectTransform>();
        oRt.anchorMin = Vector2.zero; oRt.anchorMax = Vector2.one;
        oRt.offsetMin = oRt.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRt = card.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.15f, 0.15f);
        cRt.anchorMax = new Vector2(0.85f, 0.88f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.21f, 1f);
        MakeBorder(card, new Color(0.72f, 0.53f, 0.04f, 1f), 3f);

        // 标题
        MakeTMP("Title", card.transform,
            new Vector2(0f, 0.82f), new Vector2(1f, 1f),
            new Vector2(20, 5), new Vector2(-20, -5),
            "消失的古币", 28,
            new Color(0.72f, 0.53f, 0.04f, 1f),
            TextAlignmentOptions.Center, true);

        // 分割线
        var div = new GameObject("Div");
        div.transform.SetParent(card.transform, false);
        var dRt = div.AddComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.02f, 0.815f);
        dRt.anchorMax = new Vector2(0.98f, 0.818f);
        dRt.offsetMin = dRt.offsetMax = Vector2.zero;
        div.AddComponent<Image>().color = new Color(0.72f, 0.53f, 0.04f, 0.4f);

        // 正文
        MakeTMP("Body", card.transform,
            new Vector2(0f, 0.15f), new Vector2(1f, 0.815f),
            new Vector2(30, 10), new Vector2(-30, -10),
            "【游戏简介】\n\n" +
            "博物馆展台上的古币神秘消失，没有人动过它，\n" +
            "监控也没有异常……\n\n" +
            "你将扮演小侦探柯南，深入调查这起离奇案件，\n" +
            "在探索过程中发现光学世界的奥秘！\n\n" +
            "【操作说明】\n\n" +
            "WASD — 移动\n" +
            "鼠标 — 转动视角\n" +
            "E键 — 交互/对话\n" +
            "Enter键 — 推进对话",
            16,
            new Color(0.96f, 0.93f, 0.82f, 1f),
            TextAlignmentOptions.TopLeft, false);

        // 关闭按钮
        var btnGo = new GameObject("CloseBtn");
        btnGo.transform.SetParent(card.transform, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.3f, 0.03f);
        bRt.anchorMax = new Vector2(0.7f, 0.13f);
        bRt.offsetMin = bRt.offsetMax = Vector2.zero;
        var bImg = btnGo.AddComponent<Image>();
        bImg.color = new Color(0.72f, 0.53f, 0.04f, 1f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bImg;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f, 0.82f, 0.25f, 1f);
        btn.colors = bc;
        btn.onClick.AddListener(() => Destroy(overlay));

        MakeTMP("T", btnGo.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            "关闭", 18,
            new Color(0.08f, 0.05f, 0.01f, 1f),
            TextAlignmentOptions.Center, true);

        StartCoroutine(PopIn(cRt));
    }

    // ══════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════
    void MakeBorder(GameObject go, Color color, float thick)
    {
        void L(bool h, bool s)
        {
            var lg = new GameObject("L");
            lg.transform.SetParent(go.transform, false);
            var rt = lg.AddComponent<RectTransform>();
            if (h) { rt.anchorMin = new Vector2(0, s?1:0); rt.anchorMax = new Vector2(1, s?1:0); rt.offsetMin = new Vector2(0, s?-thick:0); rt.offsetMax = new Vector2(0, s?0:thick); }
            else   { rt.anchorMin = new Vector2(s?0:1, 0); rt.anchorMax = new Vector2(s?0:1, 1); rt.offsetMin = new Vector2(s?0:-thick, 0); rt.offsetMax = new Vector2(s?thick:0, 0); }
            lg.AddComponent<Image>().color = color;
        }
        L(true,true); L(true,false); L(false,true); L(false,false);
    }

    TextMeshProUGUI MakeTMP(string name, Transform parent,
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

    IEnumerator PopIn(RectTransform rt)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            float s = t < 0.75f
                ? Mathf.Lerp(0f, 1.1f, t / 0.75f)
                : Mathf.Lerp(1.1f, 1f, (t - 0.75f) / 0.25f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
