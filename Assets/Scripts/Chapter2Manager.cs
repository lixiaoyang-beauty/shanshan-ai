using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class Chapter2Manager : MonoBehaviour
{
    [Header("── 场景对象 ──")]
    public Camera mainCamera;
    public Transform waterCup;
    public GameObject coinObject;
    public Transform coinTargetPos;
    public float cupRadius = 0.8f;

    [Header("── 摄像机高度设置 ──")]
    [Tooltip("俯视高度（阶段2初始，能看到古币）")]
    public float standHeight = 3f;
    [Tooltip("侧视高度（蹲下后，古币消失）")]
    public float crouchHeight = 1f;
    [Tooltip("摄像机下移/上移速度")]
    public float heightMoveSpeed = 2f;

    [Header("── 鼠标视角脚本 ──")]
    public MonoBehaviour mouseLookScript;

    [Header("── 对话框 ──")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI contentText;
    public Image avatarImage;
    public Button continueButton;

    [Header("── 提示UI ──")]
    public GameObject hintUI;
    public TextMeshProUGUI hintText;

    [Header("── 结束面板 ──")]
    public GameObject finishPanel;
    public Button finishBtn;

    [Header("── 头像 ──")]
    public Sprite conanAvatar;

    [Header("── 字体 ──")]
    public TMP_FontAsset chineseFont;

    [Header("── 移动设置 ──")]
    public float moveSpeed = 4f;
    public float interactDistance = 3f;

    // 状态
    private bool canMove = false;
    private bool isInDialogue = false;

    // 阶段
    // 0=开场对话 1=拖拽古币 2=俯视观察 3=蹲下侧视 4=站起重现 5=结束
    private int stage = 0;
    private bool coinPlaced    = false;
    private bool isCrouching   = false;
    private bool seenDisappear = false;
    private bool seenReappear  = false;

    // 拖拽
    private bool isDraggingCoin = false;
    private float dragHeight;

    // 摄像机高度动画
    private Coroutine heightCoroutine;

    // 对话
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
        Hide(finishPanel);

        if (continueButton != null)
        { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }

        if (finishBtn != null)
        { finishBtn.onClick.RemoveAllListeners(); finishBtn.onClick.AddListener(GoToChapter3); }

        AudioManager.AddClickSound(continueButton);
        AudioManager.AddClickSound(finishBtn);

        // 古币初始显示，设置渲染队列高于liquid
        if (coinObject != null)
        {
            coinObject.SetActive(true);
            foreach (var r in coinObject.GetComponentsInChildren<Renderer>())
                foreach (var mat in r.materials)
                    mat.renderQueue = 3500;
        }

        // 禁用鼠标视角脚本
        if (mouseLookScript != null) mouseLookScript.enabled = false;

        // 鼠标可见（阶段1拖拽用）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 记录拖拽高度
        if (coinObject != null)
            dragHeight = coinObject.transform.position.y + 0.1f;

        StartCoroutine(StartChapter());
    }

    // ══════════════════════════════════════════
    // UPDATE
    // ══════════════════════════════════════════
    void Update()
    {
        if (isInDialogue && Input.GetKeyDown(KeyCode.Return))
            OnContinue();

        // 阶段1：WASD移动 + 拖拽古币
        if (stage == 1 && !isInDialogue && !coinPlaced)
        {
            HandleMovement();
            HandleCoinDrag();
            UpdateDragHint();
        }

        // 阶段2/3/4：WASD移动，E键切换蹲/站
        if ((stage == 2 || stage == 3) && !isInDialogue)
        {
            HandleMovement();
            if (Input.GetKeyDown(KeyCode.E))
                ToggleCrouch();
        }
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

    // ══════════════════════════════════════════
    // 开场
    // ══════════════════════════════════════════
    IEnumerator StartChapter()
    {
        yield return new WaitForSeconds(0.8f);

        // 摄像机移到俯视高度
        if (mainCamera != null)
        {
            Vector3 p = mainCamera.transform.position;
            p.y = standHeight;
            mainCamera.transform.position = p;
        }

        ShowDialogue(new[]
        {
            "我|这是艾莉博士的实验室……她不在，但我可以先自己研究！",
            "我|用这里的水杯，重现古币消失的现象，说不定能找到线索。",
            "我|先把古币放入水杯，仔细观察……"
        }, () => {
            stage = 1;
            canMove = true;
            ShowHint("走近实验台，点击古币拖拽放入烧杯！");
        });
    }

    // ══════════════════════════════════════════
    // 拖拽逻辑
    // ══════════════════════════════════════════
    void HandleCoinDrag()
    {
        if (mainCamera == null || coinObject == null) return;

        if (Input.GetMouseButtonDown(0) && !isDraggingCoin)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 20f))
            {
                if (hit.collider != null &&
                    (hit.collider.gameObject == coinObject ||
                     hit.collider.transform.IsChildOf(coinObject.transform)))
                {
                    isDraggingCoin = true;
                    dragHeight = waterCup != null
                        ? waterCup.position.y + 0.3f
                        : coinObject.transform.position.y + 0.15f;
                    ShowHint("拖到烧杯正上方，松开放入！");
                }
            }
        }

        if (isDraggingCoin && Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(Vector3.up, new Vector3(0, dragHeight, 0));
            if (dragPlane.Raycast(ray, out float dist))
                coinObject.transform.position = ray.GetPoint(dist);
        }

        if (isDraggingCoin && Input.GetMouseButtonUp(0))
        {
            isDraggingCoin = false;
            CheckDropOnCup();
        }
    }

    void CheckDropOnCup()
    {
        if (waterCup == null || coinObject == null) return;
        Vector3 coinPos = coinObject.transform.position;
        Vector3 cupPos  = waterCup.position;
        float horizDist = Vector2.Distance(
            new Vector2(coinPos.x, coinPos.z),
            new Vector2(cupPos.x,  cupPos.z));

        if (horizDist <= cupRadius)
        {
            AudioManager.PlayClick();
            StartCoroutine(CoinSinkIn());
        }
        else
            ShowHint("没放准！把古币拖到烧杯正上方再松开~");
    }

    IEnumerator CoinSinkIn()
    {
        if (coinObject == null) { AfterCoinPlaced(); yield break; }

        Vector3 startPos  = coinObject.transform.position;
        Vector3 targetPos = coinTargetPos != null
            ? coinTargetPos.position
            : waterCup.position + Vector3.down * 0.1f;
        Vector3 waterSurface = new Vector3(targetPos.x, waterCup.position.y, targetPos.z);

        // 快速落到水面
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            coinObject.transform.position = Vector3.Lerp(
                startPos, waterSurface, Mathf.SmoothStep(0,1,t));
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // 缓慢沉入杯底
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            coinObject.transform.position = Vector3.Lerp(
                waterSurface, targetPos, Mathf.SmoothStep(0,1,t));
            yield return null;
        }
        coinObject.transform.position = targetPos;

        yield return new WaitForSeconds(0.3f);
        AfterCoinPlaced();
    }

    void AfterCoinPlaced()
    {
        coinPlaced = true;
        stage = 2;
        canMove = true;
        Hide(hintUI);

        // 确保古币完全可见，并设置更高渲染队列确保在liquid上层渲染
        if (coinObject != null)
        {
            coinObject.SetActive(true);
            foreach (var r in coinObject.GetComponentsInChildren<Renderer>())
                foreach (var mat in r.materials)
                {
                    Color c = mat.color; c.a = 1f; mat.color = c;
                    mat.renderQueue = 3500; // 高于liquid的3000，确保后渲染
                }
        }

        ShowDialogue(new[]
        {
            "我|好，古币放进去了。从上方能看见古币……",
        }, () => {
            ShowHint("按 [ E ] 键蹲下，从侧面观察烧杯！");
        });
    }

    // ══════════════════════════════════════════
    // E键切换蹲/站
    // ══════════════════════════════════════════
    void ToggleCrouch()
    {
        if (isCrouching)
            StandUp();
        else
            CrouchDown();
    }

    void CrouchDown()
    {
        isCrouching = true;
        stage = 3;
        Hide(hintUI);

        if (heightCoroutine != null) StopCoroutine(heightCoroutine);
        heightCoroutine = StartCoroutine(MoveCamera(crouchHeight, () =>
        {
            // 摄像机到侧视高度，直接隐藏古币
            if (coinObject != null) { coinObject.SetActive(false); Debug.Log("古币隐藏：CrouchDown"); }

            if (!seenDisappear)
            {
                seenDisappear = true;
                AudioManager.PlayLaser();
                StartCoroutine(DelayDo(0.5f, () =>
                {
                    ShowDialogue(new[]
                    {
                        "我|古币……消失了？！明明刚才还在的！"
                    }, () => {
                        ShowHint("按 [ E ] 键站起来，从上方俯视烧杯！");
                    });
                }));
            }
            else
                ShowHint("按 [ E ] 键站起来，从上方俯视烧杯！");
        }));
    }

    void StandUp()
    {
        isCrouching = false;
        stage = 4; // 立刻锁定stage，防止动画过程中再次触发E键
        Hide(hintUI);

        if (heightCoroutine != null) StopCoroutine(heightCoroutine);
        heightCoroutine = StartCoroutine(MoveCamera(standHeight, () =>
        {
            // 摄像机回到俯视高度，直接显示古币
            if (coinObject != null) { coinObject.SetActive(true); Debug.Log("古币显示：StandUp"); }

            if (seenDisappear && !seenReappear)
            {
                seenReappear = true;
                stage = 4;
                StartCoroutine(DelayDo(1f, () =>
                {
                    ShowDialogue(new[]
                    {
                        "我|等等……从上面看又出现了！",
                        "我|从侧面看消失，从上面看出现……",
                        "我|这到底是什么原理？我要去找艾莉博士求助一下！"
                    }, ShowFinishPanel);
                }));
            }
            else if (!seenDisappear)
            {
                stage = 2;
                ShowHint("按 [ E ] 键蹲下，从侧面观察烧杯！");
            }
        }));
    }

    // 摄像机高度平滑移动
    IEnumerator MoveCamera(float targetY, System.Action onDone)
    {
        if (mainCamera == null) { onDone?.Invoke(); yield break; }
        Vector3 startPos = mainCamera.transform.position;
        Vector3 endPos   = startPos; endPos.y = targetY;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * heightMoveSpeed;
            mainCamera.transform.position = Vector3.Lerp(
                startPos, endPos, Mathf.SmoothStep(0,1,t));
            yield return null;
        }
        mainCamera.transform.position = endPos;
        onDone?.Invoke();
    }

    // 古币渐隐/渐显（Standard Shader Transparent模式）
    IEnumerator FadeCoin(bool show, System.Action onDone = null)
    {
        if (coinObject == null) { onDone?.Invoke(); yield break; }

        var renderers = coinObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            coinObject.SetActive(show);
            onDone?.Invoke();
            yield break;
        }

        coinObject.SetActive(true);

        // 收集所有材质
        var mats = new System.Collections.Generic.List<Material>();
        foreach (var r in renderers)
            foreach (var mat in r.materials)
                mats.Add(mat);

        float startAlpha = show ? 0f : 1f;
        float endAlpha   = show ? 1f : 0f;

        // 设置初始Alpha
        foreach (var mat in mats)
        {
            Color c = mat.color; c.a = startAlpha; mat.color = c;
        }

        // 渐变
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.SmoothStep(0, 1, t));
            foreach (var mat in mats)
            {
                Color c = mat.color; c.a = alpha; mat.color = c;
            }
            yield return null;
        }

        // 渐变结束
        foreach (var mat in mats)
        {
            Color c = mat.color; c.a = endAlpha; mat.color = c;
        }

        if (!show)
            coinObject.SetActive(false);
        else
        {
            // 显示完成后恢复Alpha为1，防止下次显示时Alpha还是0
            foreach (var mat in mats)
            {
                Color c = mat.color; c.a = 1f; mat.color = c;
            }
        }
        onDone?.Invoke();
    }

    IEnumerator DelayDo(float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);
        onDone?.Invoke();
    }

    // ══════════════════════════════════════════
    // 结束面板
    // ══════════════════════════════════════════
    void ShowFinishPanel()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (finishPanel != null)
        {
            Show(finishPanel);
            StartCoroutine(PopIn(finishPanel.GetComponent<RectTransform>()));
        }
        else
            BuildFallbackFinish();
    }

    void GoToChapter3()
    {
        AudioManager.PlayCorrect();
        SceneTransitionManager.LoadScene("Chapter3_Lab");
    }

    void BuildFallbackFinish()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var overlay = new GameObject("FinishOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        FillRect(overlay);
        overlay.AddComponent<Image>().color = new Color(0,0,0,0.65f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRt = card.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.15f,0.20f);
        cRt.anchorMax = new Vector2(0.85f,0.85f);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = NAVY;
        MakeBorder(card, GOLD, 3f);

        MakeTMP("Title", card.transform,
            V2(0f,0.82f), V2(1f,1f), V2(20,6), V2(-20,-6),
            "实验观察完成！", 24, GOLD,
            TextAlignmentOptions.Center, true);
        MakeImg("Div", card.transform,
            V2(0.02f,0.815f), V2(0.98f,0.818f),
            V2(0,0),V2(0,0), GOLDB);

        var c1 = MakeImg("C1", card.transform,
            V2(0.04f,0.60f), V2(0.96f,0.74f),
            V2(0,0),V2(0,0), new Color(0.72f,0.53f,0.04f,0.15f));
        MakeBorder(c1.gameObject, GOLDB, 1f);
        MakeTMP("T", c1.transform, V2(0,0),V2(1,1), V2(12,4),V2(-12,-4),
            "从侧面看：古币消失！", 16, CREAM,
            TextAlignmentOptions.MidlineLeft, false);

        var c2 = MakeImg("C2", card.transform,
            V2(0.04f,0.44f), V2(0.96f,0.58f),
            V2(0,0),V2(0,0), new Color(0.72f,0.53f,0.04f,0.15f));
        MakeBorder(c2.gameObject, GOLDB, 1f);
        MakeTMP("T", c2.transform, V2(0,0),V2(1,1), V2(12,4),V2(-12,-4),
            "从上方看：古币重现！", 16, CREAM,
            TextAlignmentOptions.MidlineLeft, false);

        MakeTMP("Medal", card.transform,
            V2(0f,0.28f), V2(1f,0.42f), V2(0,0),V2(0,0),
            "现象观察勋章 ★", 18, GOLD,
            TextAlignmentOptions.Center, true);
        MakeTMP("Hint", card.transform,
            V2(0f,0.16f), V2(1f,0.27f), V2(20,0),V2(-20,0),
            "去找艾莉博士探究原理！", 14,
            new Color(0.75f,0.70f,0.55f,1f),
            TextAlignmentOptions.Center, false);

        var btnGo = new GameObject("Btn");
        btnGo.transform.SetParent(card.transform, false);
        var bRt = btnGo.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.2f,0.03f);
        bRt.anchorMax = new Vector2(0.8f,0.14f);
        bRt.offsetMin = bRt.offsetMax = Vector2.zero;
        var bImg = btnGo.AddComponent<Image>(); bImg.color = GOLD;
        MakeBorder(btnGo, new Color(1f,0.9f,0.5f,1f), 2f);
        var btn = btnGo.AddComponent<Button>(); btn.targetGraphic = bImg;
        var bc = btn.colors;
        bc.highlightedColor = new Color(0.95f,0.82f,0.25f,1f);
        btn.colors = bc;
        btn.onClick.AddListener(GoToChapter3);
        MakeTMP("T", btnGo.transform, V2(0,0),V2(1,1), V2(0,0),V2(0,0),
            "前往艾莉博士的实验室 →", 15, DARK,
            TextAlignmentOptions.Center, true);

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
        isInDialogue = false;
        canMove = true;
        // 阶段1不锁鼠标（需要拖拽），其他阶段也不锁（E键控制）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ShowDialogue(string[] lines, System.Action onFinish)
    {
        EnterDialogueMode();
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
            avatarImage.sprite = conanAvatar;
            avatarImage.color  = conanAvatar != null ? Color.white
                                                     : new Color(0.1f,0.15f,0.35f,0.8f);
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
        { Hide(dialoguePanel); ExitDialogueMode(); lineCallback?.Invoke(); }
        else DisplayLine();
    }

    // ══════════════════════════════════════════
    // UI工具
    // ══════════════════════════════════════════
    void UpdateDragHint()
    {
        if (!isDraggingCoin || waterCup == null || coinObject == null) return;
        float dist = Vector2.Distance(
            new Vector2(coinObject.transform.position.x, coinObject.transform.position.z),
            new Vector2(waterCup.position.x, waterCup.position.z));
        if (dist <= cupRadius * 1.5f)
            ShowHint("正上方！松开放入！");
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

    IEnumerator PopIn(RectTransform rt)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero; float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            float s = t < 0.75f ? Mathf.Lerp(0f,1.1f,t/0.75f)
                                : Mathf.Lerp(1.1f,1f,(t-0.75f)/0.25f);
            rt.localScale = Vector3.one * s; yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
