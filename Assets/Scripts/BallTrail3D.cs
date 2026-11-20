using UnityEngine;

/// <summary>
/// 球的 3D 拖尾特效 (VFX)：
///  1. 核心光帶：球自身的 TrailRenderer (面向攝影機)，橫截面亮芯 → 邊緣透明，寬度由粗到尖、顏色白熱 → 皮膚色 → 透明。
///  2. 光暈帶：獨立跟隨物件上的第二條較寬、較淡的 TrailRenderer，營造體積發光感。
///  3. 火花：世界座標的 ParticleSystem，沿路徑依距離噴出發光餘燼，逐漸縮小淡出。
/// 全部為獨立物件 / 元件，無碰撞體、不投影，不影響物理與觸發器。
/// 由 RollingSkyBallController.ApplyEquippedSkin() 呼叫 ApplySkin(themeColor)。
/// </summary>
[DisallowMultipleComponent]
public class BallTrail3D : MonoBehaviour
{
    const float TeleportDistance = 4f;   // 單幀位移超過此值視為傳送/重生，重置軌跡
    const float CoreTime = 0.5f;
    const float GlowTime = 0.55f;
    const float CoreWidth = 0.75f;       // 相對球直徑
    const float GlowWidth = 1.7f;

    TrailRenderer core;
    TrailRenderer glow;
    GameObject glowObj;
    GameObject sparkObj;
    ParticleSystem sparks;
    // 拖尾物件未掛在球底下，場景卸載時可能先被銷毀；使用前一律確認仍存在
    bool PartsAlive => core != null && glow != null && sparks != null;
    ParticleSystemRenderer sparkRenderer;
    Material ribbonMat;
    Material sparkMat;
    Texture2D ribbonTex;
    Texture2D dotTex;
    MeshRenderer ballRenderer;

    Color theme = new Color(0f, 0.75f, 1f);
    Vector3 lastPos;
    bool hasLast;
    int resumeFrame;
    bool built;

    void Awake()
    {
        ballRenderer = GetComponent<MeshRenderer>();
    }

    void Build()
    {
        if (built) return;
        built = true;

        // 材質：優先使用加法混合的粒子 shader (發光感最佳)，找不到時退回一定包含在 build 內的 Sprites/Default
        Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh == null) sh = Shader.Find("Particles/Additive");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        ribbonTex = MakeRibbonTexture();
        dotTex = MakeDotTexture();
        ribbonMat = new Material(sh) { mainTexture = ribbonTex };
        sparkMat = new Material(sh) { mainTexture = dotTex };
        // Legacy Additive 使用 _TintColor，不影響頂點色；兩者皆以頂點色為主
        if (ribbonMat.HasProperty("_TintColor")) { ribbonMat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); sparkMat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); }

        // 核心光帶 = 球身上的舊 TrailRenderer (保留，外部 Clear() 仍可用)
        core = GetComponent<TrailRenderer>();
        if (core == null) core = gameObject.AddComponent<TrailRenderer>();
        SetupRibbon(core, CoreTime);

        // 光暈帶：獨立物件，每幀跟隨球位置
        glowObj = new GameObject("BallTrailGlow");
        glowObj.layer = 2;
        glowObj.transform.position = transform.position;
        glow = glowObj.AddComponent<TrailRenderer>();
        SetupRibbon(glow, GlowTime);

        // 火花粒子
        sparkObj = new GameObject("BallTrailSparks");
        sparkObj.layer = 2;
        sparkObj.transform.position = transform.position;
        sparks = sparkObj.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparkRenderer = sparkObj.GetComponent<ParticleSystemRenderer>();
        SetupSparks();
        sparks.Play();
    }

    void SetupRibbon(TrailRenderer tr, float time)
    {
        tr.sharedMaterial = ribbonMat;
        tr.time = time;
        tr.minVertexDistance = 0.06f;
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.numCapVertices = 4;
        tr.numCornerVertices = 3;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        tr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        tr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, 0.8f),
            new Keyframe(1f, 0f));
        tr.autodestruct = false;
        tr.emitting = true;
        tr.enabled = true;
    }

    void SetupSparks()
    {
        var main = sparks.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.gravityModifier = -0.05f; // 微微上飄的餘燼
        main.maxParticles = 300;

        var em = sparks.emission;
        em.enabled = true;
        em.rateOverTime = 14f;
        em.rateOverDistance = 9f;

        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.28f;

        var sol = sparks.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

        var rot = sparks.rotationOverLifetime;
        rot.enabled = false;

        sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        sparkRenderer.sharedMaterial = sparkMat;
        sparkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sparkRenderer.receiveShadows = false;
        sparkRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        sparkRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        ApplySparkColors();
    }

    void ApplySparkColors()
    {
        if (sparks == null) return;
        Color hot = Color.Lerp(theme, Color.white, 0.75f);
        var col = sparks.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(theme, 0.45f), new GradientColorKey(theme * 0.6f, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static Gradient RibbonGradient(Color theme, float alpha)
    {
        Color hot = Color.Lerp(theme, Color.white, 0.7f);
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(theme, 0.35f), new GradientColorKey(theme * 0.5f, 1f) },
            new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.55f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>套用裝備皮膚的主題色 (可重複呼叫)。</summary>
    public void ApplySkin(Color themeColor)
    {
        theme = themeColor;
        Build();
        core.colorGradient = RibbonGradient(theme, 1f);
        glow.colorGradient = RibbonGradient(theme, 0.5f);
        ApplySparkColors();
        ResetTrail();
    }

    /// <summary>清除軌跡 (傳送、重生時呼叫)，避免出現橫跨賽道的拖痕。</summary>
    public void ResetTrail()
    {
        if (!built || !PartsAlive) return;
        core.Clear();
        glow.Clear();
        if (glowObj != null) glowObj.transform.position = transform.position;
        if (sparkObj != null) sparkObj.transform.position = transform.position;
        if (sparks != null) sparks.Clear(true);
        // 暫停一幀再繼續發射，避免新位置接到舊點
        core.emitting = false;
        glow.emitting = false;
        resumeFrame = Time.frameCount + 2;
        hasLast = false;
    }

    void LateUpdate()
    {
        if (!built || !PartsAlive) return;

        Vector3 pos = transform.position;
        bool visible = gameObject.activeInHierarchy && ballRenderer != null && ballRenderer.enabled;

        if (!visible)
        {
            if (core.emitting || glow.emitting || sparks.isEmitting)
            {
                core.Clear(); glow.Clear(); sparks.Clear(true);
            }
            core.emitting = false; glow.emitting = false;
            if (sparks.isEmitting) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            glowObj.transform.position = pos;
            sparkObj.transform.position = pos;
            hasLast = false;
            resumeFrame = Time.frameCount + 2;
            return;
        }

        if (hasLast && (pos - lastPos).sqrMagnitude > TeleportDistance * TeleportDistance)
            ResetTrail();
        lastPos = pos;
        hasLast = true;

        glowObj.transform.position = pos;
        sparkObj.transform.position = pos;

        // 寬度隨球縮放 (Sky Tower 等不同尺寸)
        float s = Mathf.Max(0.1f, transform.lossyScale.x);
        core.widthMultiplier = s * CoreWidth;
        glow.widthMultiplier = s * GlowWidth;
        var shape = sparks.shape;
        shape.radius = 0.28f * s;

        if (Time.frameCount >= resumeFrame)
        {
            if (!core.emitting) core.emitting = true;
            if (!glow.emitting) glow.emitting = true;
            if (!sparks.isPlaying) sparks.Play();
        }
    }

    void OnDisable()
    {
        if (!built || !PartsAlive) return;
        core.Clear(); glow.Clear(); sparks.Clear(true);
        core.emitting = false; glow.emitting = false;
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        hasLast = false;
        resumeFrame = Time.frameCount + 2;
    }

    void OnDestroy()
    {
        if (glowObj != null) Destroy(glowObj);
        if (sparkObj != null) Destroy(sparkObj);
        if (ribbonMat != null) Destroy(ribbonMat);
        if (sparkMat != null) Destroy(sparkMat);
        if (ribbonTex != null) Destroy(ribbonTex);
        if (dotTex != null) Destroy(dotTex);
    }

    // 橫截面 (V 方向) 亮芯 → 邊緣透明，製造圓管/體積感
    static Texture2D MakeRibbonTexture()
    {
        const int W = 4, H = 64;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < H; y++)
        {
            float v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);      // 0 中心, 1 邊緣
            float a = Mathf.Pow(Mathf.Clamp01(1f - v), 1.6f);
            float core = Mathf.Pow(Mathf.Clamp01(1f - v), 4f);    // 中心額外加亮
            float c = Mathf.Clamp01(0.55f + core * 0.45f);
            for (int x = 0; x < W; x++) t.SetPixel(x, y, new Color(c, c, c, a));
        }
        t.Apply(false, true);
        return t;
    }

    // 柔邊圓點 (火花)
    static Texture2D MakeDotTexture()
    {
        const int N = 32;
        Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply(false, true);
        return t;
    }
}
