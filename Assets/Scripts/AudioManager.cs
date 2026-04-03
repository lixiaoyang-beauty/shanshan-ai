using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [Header("音乐与音效")]
    public AudioClip bgmClip;
    public AudioClip correctClip;
    public AudioClip wrongClip;
    public AudioClip starClip;
    public AudioClip laserClip;
    public AudioClip clickClip;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private Coroutine fadeCoroutine;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // BGM音源：循环播放
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0.5f;

        // 音效音源：单次播放
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = 1f;
    }

    // ── BGM ──────────────────────────────────────
    public static void PlayBGM()
    {
        if (instance == null || instance.bgmClip == null) return;
        if (instance.bgmSource.isPlaying) return;
        instance.bgmSource.clip = instance.bgmClip;
        instance.bgmSource.Play();
    }

    public static void FadeOutBGM(float duration = 1f)
    {
        if (instance == null) return;
        if (instance.fadeCoroutine != null)
            instance.StopCoroutine(instance.fadeCoroutine);
        instance.fadeCoroutine = instance.StartCoroutine(instance._FadeOut(duration));
    }

    IEnumerator _FadeOut(float duration)
    {
        float startVol = bgmSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();
        bgmSource.volume = startVol;
    }

    // ── 音效 ─────────────────────────────────────
    public static void PlayCorrect()  => instance?.sfxSource.PlayOneShot(instance.correctClip);
    public static void PlayWrong()    => instance?.sfxSource.PlayOneShot(instance.wrongClip);
    public static void PlayStar()     => instance?.sfxSource.PlayOneShot(instance.starClip);
    public static void PlayLaser()    => instance?.sfxSource.PlayOneShot(instance.laserClip);
    public static void PlayClick()    => instance?.sfxSource.PlayOneShot(instance.clickClip);

    public static void AddClickSound(UnityEngine.UI.Button btn)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => PlayClick());
    }
}
