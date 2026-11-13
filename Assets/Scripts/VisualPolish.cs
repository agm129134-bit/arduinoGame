using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// v1.0 全域視覺統一層（執行期自動掛載，不需修改場景）：
/// 1. 每次載入場景時由黑畫面平滑淡入，模式切換不再生硬跳切
/// 2. 依模式套用統一的霓虹夜空主題（天空、霧、環境光），賽道自然融入地平線
/// 3. 主選單背景加入緩慢漂浮的霓虹光點與標題呼吸光
/// 4. 將字型無法顯示的彩色 emoji 換成一般符號，避免 HUD 出現空白
/// </summary>
public class VisualPolish : MonoBehaviour
{
    static VisualPolish instance;
    Coroutine fadeRoutine;

    // 霓虹主題調色盤（與主選單深藍底色 0.04,0.06,0.12 同一色系）
    struct Theme
    {
        public Color skyTop, horizon, ground, fog, ambientSky, ambientEquator, ambientGround;
        public float fogStart, fogEnd, exposure;
    }

    static readonly Theme LevelTheme = new Theme
    {
        skyTop = new Color(0.10f, 0.14f, 0.38f), horizon = new Color(0.30f, 0.22f, 0.55f), ground = new Color(0.03f, 0.04f, 0.10f),
        fog = new Color(0.10f, 0.10f, 0.24f), ambientSky = new Color(0.42f, 0.48f, 0.75f), ambientEquator = new Color(0.30f, 0.26f, 0.48f), ambientGround = new Color(0.10f, 0.10f, 0.18f),
        fogStart = 55f, fogEnd = 210f, exposure = 1.0f
    };
    static readonly Theme EndlessTheme = new Theme
    {
        skyTop = new Color(0.20f, 0.06f, 0.32f), horizon = new Color(0.65f, 0.18f, 0.48f), ground = new Color(0.05f, 0.02f, 0.08f),
        fog = new Color(0.22f, 0.07f, 0.24f), ambientSky = new Color(0.60f, 0.40f, 0.75f), ambientEquator = new Color(0.45f, 0.22f, 0.45f), ambientGround = new Color(0.12f, 0.06f, 0.14f),
        fogStart = 50f, fogEnd = 170f, exposure = 1.05f
    };
    static readonly Theme TowerTheme = new Theme
    {
        skyTop = new Color(0.16f, 0.24f, 0.52f), horizon = new Color(0.95f, 0.62f, 0.45f), ground = new Color(0.08f, 0.10f, 0.20f),
        fog = new Color(0.26f, 0.29f, 0.50f), ambientSky = new Color(0.60f, 0.66f, 0.90f), ambientEquator = new Color(0.62f, 0.50f, 0.50f), ambientGround = new Color(0.18f, 0.18f, 0.26f),
        fogStart = 120f, fogEnd = 520f, exposure = 1.15f
    };
    static readonly Theme BuilderTheme = new Theme
    {
        skyTop = new Color(0.08f, 0.12f, 0.26f), horizon = new Color(0.16f, 0.24f, 0.40f), ground = new Color(0.03f, 0.05f, 0.09f),
        fog = new Color(0.08f, 0.12f, 0.20f), ambientSky = new Color(0.50f, 0.56f, 0.72f), ambientEquator = new Color(0.32f, 0.36f, 0.48f), ambientGround = new Color(0.12f, 0.13f, 0.18f),
        fogStart = 120f, fogEnd = 480f, exposure = 1.0f
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        GameObject go = new GameObject("VisualPolish");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<VisualPolish>();
        instance.BuildFadeOverlay();
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    // 淡入遮罩用 IMGUI 繪製，刻意不建立 Canvas：遊戲內多處以 FindAnyObjectByType<Canvas>() 尋找主畫布，
    // 常駐 Canvas 會被誤用而讓 UI 跨場景殘留
    float fadeAlpha = 1f;
    Texture2D fadeTex;
    static readonly Color FadeColor = new Color(0.02f, 0.03f, 0.07f, 1f);

    void BuildFadeOverlay()
    {
        fadeTex = new Texture2D(1, 1);
        fadeTex.SetPixel(0, 0, Color.white);
        fadeTex.Apply();
    }

    void OnGUI()
    {
        if (fadeAlpha <= 0.001f || fadeTex == null) return;
        GUI.depth = -10000;
        Color old = GUI.color;
        GUI.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, fadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fadeTex);
        GUI.color = old;
    }

    int handledSceneHandle = -1;

    void Start()
    {
        // 第一個場景在本物件建立前就已載入時不會收到 sceneLoaded，這裡補處理，避免開機停在黑畫面
        Scene active = SceneManager.GetActiveScene();
        if (active.handle != handledSceneHandle) OnSceneLoaded(active, LoadSceneMode.Single);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        handledSceneHandle = scene.handle;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());

        titleText = null; titleOutline = null; dotArea = null; dots = null;
        if (scene.name == "MainMenuScene")
        {
            DecorateMainMenu();
        }
        else
        {
            bool tower = PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1;
            bool endless = PlayerPrefs.GetInt("IsEndlessMode", 0) == 1;
            bool builder = PlayerPrefs.GetInt("IsBuilderMode", 0) == 1;
            ApplyTheme(tower ? TowerTheme : endless ? EndlessTheme : builder ? BuilderTheme : LevelTheme);
        }
        StartCoroutine(SweepTextsLater());
    }

    IEnumerator FadeIn()
    {
        fadeAlpha = 1f;
        yield return null; // 等一幀讓場景物件就緒，避免淡入時看到生成過程
        float t = 0f;
        const float dur = 0.55f;
        while (t < dur)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            fadeAlpha = 1f - Mathf.SmoothStep(0f, 1f, t / dur);
            yield return null;
        }
        fadeAlpha = 0f;
    }

    static void ApplyTheme(Theme th)
    {
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.SetColor("_SkyTint", th.skyTop);
            sky.SetColor("_GroundColor", th.ground);
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetFloat("_Exposure", th.exposure);
            sky.SetFloat("_SunSize", 0.025f);
            RenderSettings.skybox = sky;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = skyShader != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = th.fog;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = th.fog;
        RenderSettings.fogStartDistance = th.fogStart;
        RenderSettings.fogEndDistance = th.fogEnd;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = th.ambientSky;
        RenderSettings.ambientEquatorColor = th.ambientEquator;
        RenderSettings.ambientGroundColor = th.ambientGround;
    }

    void DecorateMainMenu()
    {
        RenderSettings.fog = false;
        RollingSkyMenuManager menu = FindAnyObjectByType<RollingSkyMenuManager>();
        if (menu == null || menu.mainMenuPanel == null) return;
        Canvas canvas = menu.mainMenuPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 標題統一為正式名稱，並加上呼吸光
        Transform title = menu.mainMenuPanel.transform.Find("Title");
        if (title != null)
        {
            Text t = title.GetComponent<Text>();
            if (t != null)
            {
                if (t.text.Contains("SKY")) t.text = "ROLLING LEAP 3D";
                SetupTitleGlow(t);
            }
        }
        Transform subtitle = menu.mainMenuPanel.transform.Find("Subtitle");
        if (subtitle != null && subtitle.GetComponent<Text>() != null)
            subtitle.GetComponent<Text>().text = "【 極速躍動 3D × Arduino 體感互動跑酷 】";

        // 霓虹光點層：放在 Canvas 最底層 (背景之上、所有面板之下)
        GameObject layer = new GameObject("NeonParticles", typeof(RectTransform));
        layer.transform.SetParent(canvas.transform, false);
        RectTransform lrt = layer.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
        Transform bg = canvas.transform.Find("Background");
        layer.transform.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);
        SetupParticles(lrt);
    }

    IEnumerator SweepTextsLater()
    {
        // 多數 UI 文字在 Start 之後才由程式填入，延後兩次掃描
        yield return null;
        SweepEmoji();
        yield return new WaitForSecondsRealtime(0.5f);
        SweepEmoji();
    }

    static readonly string[,] EmojiMap =
    {
        { "💎", "◆" }, { "🏆", "★" }, { "🗼", "▲" }, { "💡", "•" }, { "⚡", "»" }, { "🔄", "" }, { "🏠", "⌂" }, { "🔊", "♪" }, { "💾", "▣" }, { "🗑", "✕" }, { "🖥️", "▭" }, { "🪟", "▢" }, { "🎮", "" }
    };

    public static string StripEmoji(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        for (int i = 0; i < EmojiMap.GetLength(0); i++)
            if (s.Contains(EmojiMap[i, 0])) s = s.Replace(EmojiMap[i, 0], EmojiMap[i, 1]);
        return s;
    }

    static void SweepEmoji()
    {
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string s = StripEmoji(t.text);
            if (s != t.text) t.text = s;
        }
    }

    // ---------- 主選單裝飾（由常駐物件直接驅動，player 建置不允許 AddComponent 巢狀類別） ----------

    const int DotCount = 46;
    Text titleText; Outline titleOutline; Vector3 titleBaseScale;
    RectTransform dotArea; RectTransform[] dots; Image[] dotImgs; Vector2[] dotVel; float[] dotPhase; Color[] dotCol;

    void SetupTitleGlow(Text t)
    {
        titleText = t;
        titleBaseScale = t.transform.localScale;
        titleOutline = t.gameObject.AddComponent<Outline>();
        titleOutline.effectDistance = new Vector2(2f, -2f);
    }

    void SetupParticles(RectTransform area)
    {
        dotArea = area;
        dots = new RectTransform[DotCount]; dotImgs = new Image[DotCount]; dotVel = new Vector2[DotCount]; dotPhase = new float[DotCount]; dotCol = new Color[DotCount];
        Color[] palette = { new Color(0f, 0.95f, 1f), new Color(1f, 0.3f, 0.85f), new Color(1f, 0.82f, 0.25f), new Color(0.45f, 0.5f, 1f) };
        for (int i = 0; i < DotCount; i++)
        {
            GameObject d = new GameObject("Dot", typeof(RectTransform));
            d.transform.SetParent(area, false);
            Image im = d.AddComponent<Image>();
            im.raycastTarget = false;
            float size = Random.Range(3f, 9f);
            dots[i] = d.GetComponent<RectTransform>();
            dots[i].sizeDelta = new Vector2(size, size);
            dots[i].localRotation = Quaternion.Euler(0, 0, 45f);
            dots[i].anchoredPosition = new Vector2(Random.Range(-1000f, 1000f), Random.Range(-560f, 560f));
            dotVel[i] = new Vector2(Random.Range(-12f, 12f), Random.Range(10f, 34f));
            dotPhase[i] = Random.Range(0f, 6.28f);
            dotCol[i] = palette[i % palette.Length];
            dotImgs[i] = im;
        }
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (titleText != null)
        {
            float p = (Mathf.Sin(now * 1.6f) + 1f) * 0.5f;
            titleText.transform.localScale = titleBaseScale * (1f + p * 0.025f);
            titleText.color = Color.Lerp(new Color(0f, 0.95f, 0.88f), new Color(0.55f, 1f, 1f), p * 0.6f);
            if (titleOutline != null) titleOutline.effectColor = new Color(0.1f, 0.9f, 1f, 0.15f + p * 0.35f);
        }
        if (dotArea != null && dots != null)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 half = dotArea.rect.size * 0.5f + new Vector2(20f, 20f);
            for (int i = 0; i < DotCount; i++)
            {
                if (dots[i] == null) continue;
                Vector2 pos = dots[i].anchoredPosition + dotVel[i] * dt;
                if (pos.y > half.y) { pos.y = -half.y; pos.x = Random.Range(-half.x, half.x); }
                if (pos.x > half.x) pos.x = -half.x; else if (pos.x < -half.x) pos.x = half.x;
                dots[i].anchoredPosition = pos;
                Color c = dotCol[i];
                c.a = 0.18f + 0.32f * (Mathf.Sin(now * 1.3f + dotPhase[i]) + 1f) * 0.5f;
                dotImgs[i].color = c;
            }
        }
    }
}
