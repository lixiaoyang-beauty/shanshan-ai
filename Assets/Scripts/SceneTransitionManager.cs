using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager instance;
    private GameObject fadeOverlay;
    private Image fadeImage;
    private GameObject loadingText;
    private Text loadingLabel;
    private Coroutine dotsCoroutine;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeOverlay();
    }

    void BuildFadeOverlay()
    {
        GameObject canvasGo = new GameObject("FadeCanvas");
        canvasGo.transform.SetParent(transform);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        fadeOverlay = new GameObject("FadeOverlay");
        fadeOverlay.transform.SetParent(canvasGo.transform, false);
        RectTransform rt = fadeOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        fadeImage = fadeOverlay.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeOverlay.SetActive(false);

        loadingText = new GameObject("loadingText");
        loadingText.transform.SetParent(canvasGo.transform, false);
        RectTransform textRt = loadingText.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(400f, 60f);
        textRt.anchoredPosition = Vector2.zero;
        loadingLabel = loadingText.AddComponent<Text>();
        loadingLabel.text = "Loading.";
        loadingLabel.fontSize = 24;
        loadingLabel.color = Color.white;
        loadingLabel.alignment = TextAnchor.MiddleCenter;
        loadingLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        loadingText.SetActive(false);
    }

    // 点点循环动画协程
    IEnumerator AnimateDots()
    {
        string[] frames = { "Loading.", "Loading..", "Loading..." };
        int i = 0;
        while (true)
        {
            loadingLabel.text = frames[i % frames.Length];
            i++;
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    public static void LoadScene(string sceneName)
    {
        if (instance != null)
            instance.StartCoroutine(instance.FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        // 淡入（变黑）
        fadeOverlay.SetActive(true);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t));
            yield return null;
        }

        // 显示Loading并启动点点动画
        loadingText.SetActive(true);
        dotsCoroutine = StartCoroutine(AnimateDots());

        // 加载场景，同时保证最少显示1.5秒
        float minDisplay = 1.5f;
        float elapsed = 0f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone || elapsed < minDisplay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 停止动画并隐藏Loading
        if (dotsCoroutine != null)
            StopCoroutine(dotsCoroutine);
        loadingText.SetActive(false);

        // 淡出（从黑变透明）
        t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime * 1.5f;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t));
            yield return null;
        }
        fadeOverlay.SetActive(false);
    }
}
