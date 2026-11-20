using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RollingSkyTrackGenerator : MonoBehaviour
{
    public static RollingSkyTrackGenerator Instance { get; private set; }

    [Header("Track Materials")]
    public Material matTileDark;
    public Material matTileCyan;
    public Material matHazard;
    public Material matJumpPad;
    public Material matGem;
    public Material matGoal;

    [Header("Endless Mode Settings")]
    public float chunkLength = 30.0f;
    public int activeChunksCount = 5;
    private float currentFarthestZ = 0f;

    private class EndlessChunkEntry
    {
        public GameObject go;
        public float startZ;
        public float endZ;
    }
    private List<EndlessChunkEntry> activeChunks = new List<EndlessChunkEntry>();
    private Transform ballTransform;
    private bool isEndlessActive = false;
    private int endlessChunkCount = 0;
    // 簡單: 5線道尖刺巡航、金鑽長廊、S彎、鋸齒、單跳台 / 中等再加連跳、孤島、量子斷層、躍崖、雙斷層
    private static readonly int[] EasyEndlessPool = { 0, 9, 8, 11, 5, 9 };
    private static readonly int[] MediumEndlessPool = { 0, 9, 8, 11, 5, 7, 12, 10, 1, 2 };
    private GameObject trackRoot;
    private PhysicsMaterial tilePhysicMaterial;

    private const float COL_WIDTH = 2.0f; // 每線道寬度 2m
    private const float ROW_LEN = 2.0f;   // 每地磚長度 2m

    void Awake()
    {
        Instance = this;
        LoadDefaultMaterials();

        // 無盡模式起跑速度調慢：必須在 BallController.Start 讀取 baseForwardSpeed 之前設定 (Awake 先於所有 Start)
        if (PlayerPrefs.GetInt("IsEndlessMode", 0) == 1)
        {
            RollingSkyBallController b = FindAnyObjectByType<RollingSkyBallController>();
            if (b != null) b.forwardSpeed = 8.5f;
        }
    }

    void Start()
    {
        RollingSkyBallController ball = FindAnyObjectByType<RollingSkyBallController>();
        if (ball != null) ballTransform = ball.transform;

        int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
        bool isEndless = PlayerPrefs.GetInt("IsEndlessMode", 0) == 1;
        bool isBuilder = PlayerPrefs.GetInt("IsBuilderMode", 0) == 1;
        bool isSkyTower = PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1;

        // 清理場景中任何預設的舊靜態賽道階層（如場景自帶的 --- Track Hierarchy ---）
        GameObject legacyTrack = GameObject.Find("--- Track Hierarchy ---");
        if (legacyTrack != null)
        {
            Destroy(legacyTrack);
        }

        if (isBuilder)
        {
            // 在自訂建造模式下，由 TrackBuilderManager 全權掌管建造與視覺，本生成器不預先生成靜態賽道
            // 且需徹底清空場景中預置的舊方塊、寶石、尖刺、跳台等物件，確保留給玩家完全空白的地圖
            GetOrCreateTrackRoot(clearExisting: true);

            foreach (var gem in FindObjectsByType<GemCollectible>(FindObjectsSortMode.None))
            {
                Destroy(gem.gameObject);
            }
            foreach (var pad in FindObjectsByType<JumpPad>(FindObjectsSortMode.None))
            {
                Destroy(pad.gameObject);
            }
            foreach (var haz in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
            {
                Destroy(haz.gameObject);
            }
            GameObject gate = GameObject.Find("FinishStarGate");
            if (gate != null) Destroy(gate);

            return;
        }
        else if (isSkyTower)
        {
            // 通天天梯模式：清理舊跑酷物件，建立 500 米立體高塔地圖
            GetOrCreateTrackRoot(clearExisting: true);

            foreach (var gem in FindObjectsByType<GemCollectible>(FindObjectsSortMode.None))
            {
                Destroy(gem.gameObject);
            }
            foreach (var pad in FindObjectsByType<JumpPad>(FindObjectsSortMode.None))
            {
                Destroy(pad.gameObject);
            }
            foreach (var haz in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
            {
                Destroy(haz.gameObject);
            }
            GameObject gate = GameObject.Find("FinishStarGate");
            if (gate != null) Destroy(gate);

            EnsureSkyTowerManager();
            return;
        }
        else if (isEndless)
        {
            StartEndlessMode();
        }
        else
        {
            BuildLevel(selectedLevel);
        }
    }

    void EnsureSkyTowerManager()
    {
        SkyTowerMapGenerator gen = FindAnyObjectByType<SkyTowerMapGenerator>();
        if (gen == null)
        {
            GameObject genObj = new GameObject("SkyTowerMapGenerator");
            gen = genObj.AddComponent<SkyTowerMapGenerator>();
        }
        gen.BuildTower();

        if (ballTransform != null)
        {
            ballTransform.position = new Vector3(0, 1.5f, -4f);
            Rigidbody rb = ballTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    void Update()
    {
        if (isEndlessActive && ballTransform != null)
        {
            float ballZ = ballTransform.position.z;

            // 確保前方恆定維持 180 米以上的連續安全賽道，永不中斷
            while (currentFarthestZ < ballZ + 180.0f)
            {
                SpawnEndlessChunk();
            }

            // 安全銷毀：只有當整個 Chunk 的結束點 (endZ) 徹底落後小球 50 米以上才銷毀
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                if (activeChunks[i] != null && (ballZ - activeChunks[i].endZ > 50.0f))
                {
                    if (activeChunks[i].go != null)
                    {
                        Destroy(activeChunks[i].go);
                    }
                    activeChunks.RemoveAt(i);
                }
            }
        }
    }

    void LoadDefaultMaterials()
    {
        if (matTileDark == null) matTileDark = Resources.Load<Material>("Materials/Mat_TileDark");
        if (matTileCyan == null) matTileCyan = Resources.Load<Material>("Materials/Mat_TileCyan");
        if (matHazard == null) matHazard = Resources.Load<Material>("Materials/Mat_Hazard");
        if (matJumpPad == null) matJumpPad = Resources.Load<Material>("Materials/Mat_JumpPad");
        if (matGem == null) matGem = Resources.Load<Material>("Materials/Mat_Gem");
        if (matGoal == null) matGoal = Resources.Load<Material>("Materials/Mat_Goal");

        // 若 Resources 無法讀取，由 Shader 動態建構備援材質
        if (matTileDark == null) matTileDark = CreateFallbackMat(new Color(0.08f, 0.12f, 0.22f));
        if (matTileCyan == null) matTileCyan = CreateFallbackMat(new Color(0f, 0.75f, 1f));
        if (matHazard == null) matHazard = CreateFallbackMat(new Color(1f, 0.15f, 0.15f));
        if (matJumpPad == null) matJumpPad = CreateFallbackMat(new Color(0.1f, 1f, 0.4f));
        if (matGem == null)
        {
            matGem = new Material(Shader.Find("Standard"));
            matGem.name = "Mat_Gem_RadiantGold";
            matGem.color = new Color(1.0f, 0.88f, 0.12f, 1.0f);
            matGem.SetColor("_EmissionColor", new Color(3.2f, 2.5f, 0.4f));
            matGem.EnableKeyword("_EMISSION");
            matGem.SetFloat("_Metallic", 0.7f);
            matGem.SetFloat("_Glossiness", 0.98f);
        }
        if (matGoal == null) matGoal = CreateFallbackMat(new Color(1f, 0.85f, 0.1f));

        if (tilePhysicMaterial == null)
        {
            tilePhysicMaterial = new PhysicsMaterial("TileZeroBounce");
            tilePhysicMaterial.bounciness = 0f;
            tilePhysicMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
            tilePhysicMaterial.dynamicFriction = 0.05f;
            tilePhysicMaterial.staticFriction = 0.05f;
            tilePhysicMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        }
    }

    Material CreateFallbackMat(Color col)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = col;
        m.SetFloat("_Glossiness", 0.7f);
        return m;
    }

    GameObject GetOrCreateTrackRoot(bool clearExisting = false)
    {
        if (trackRoot == null)
        {
            trackRoot = GameObject.Find("TrackRoot");
            if (trackRoot == null)
            {
                trackRoot = new GameObject("TrackRoot");
            }
        }

        if (clearExisting)
        {
            // 僅在切換關卡或啟動新模式時清理舊賽道，動態區塊生成絕不清除
            for (int i = trackRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(trackRoot.transform.GetChild(i).gameObject);
            }
        }
        return trackRoot;
    }

    // ==================== 10 大關卡動態生成系統 ====================
    public void BuildLevel(int level)
    {
        GameObject root = GetOrCreateTrackRoot(clearExisting: true);
        Transform parent = root.transform;

        RollingSkyBallController ball = FindAnyObjectByType<RollingSkyBallController>();
        if (ball != null)
        {
            // 隨關卡逐步提升小球滾動衝刺速度
            // (二次易化) 7.5 → 10.65 m/s，原為 11.0 → 17.3
            ball.forwardSpeed = 7.5f + (level - 1) * 0.35f;
        }

        // 尖刺保留比例：每 N 個只放 1 個 (0 = 全部移除)；L1 無尖刺，L7 本身僅 2 個全保留
        int[] keepEvery = { 0, 0, 3, 3, 2, 2, 2, 1, 2, 2, 2 };
        hazardKeepEvery = keepEvery[Mathf.Clamp(level, 1, 10)];
        hazardCounter = 0;
        int childrenBefore = parent.childCount;

        switch (level)
        {
            case 1: BuildLevel_1(parent); break;
            case 2: BuildLevel_2(parent); break;
            case 3: BuildLevel_3(parent); break;
            case 4: BuildLevel_4(parent); break;
            case 5: BuildLevel_5(parent); break;
            case 6: BuildLevel_6(parent); break;
            case 7: BuildLevel_7(parent); break;
            case 8: BuildLevel_8(parent); break;
            case 9: BuildLevel_9(parent); break;
            case 10: BuildLevel_10(parent); break;
            default: BuildLevel_1(parent); break;
        }

        // 起跑暖身再加長 10 排 (20m)：整條賽道往前平移，再於起點補一段 5 線道平臺與寶石
        float shiftZ = EXTRA_WARMUP_ROWS * ROW_LEN;
        for (int i = childrenBefore; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            c.position += new Vector3(0f, 0f, shiftZ);
        }
        BuildSection(parent, 0, EXTRA_WARMUP_ROWS, ALL5);
        GemLine(parent, 0, 3, EXTRA_WARMUP_ROWS, 3);
        RollingSkyGameManager gmShift = FindAnyObjectByType<RollingSkyGameManager>();
        if (gmShift != null)
        {
            gmShift.trackLength += shiftZ;
            gmShift.ResetLevelStats();
        }
        hazardKeepEvery = 1;
    }

    // ==================== 難度調整說明 (v1.0 easier pass) ====================
    // 設計原則：
    //  - 前進速度 9.0 (L1) → 13.05 (L10)，每關 +0.45；跳台後水平速度即 forwardSpeed (見 BallController.FixedUpdate)
    //  - 起跑安全區 30 排 (60m)；深淵斷層一律 4~5 排空缺，跳台鋪滿整個來源路段的所有線道
    //  - 落地後至少 6~12 排寬敞 5 線道平台，且落地後 13 排內不放障礙
    //  - 5 線道最多 1 個尖刺、3 線道尖刺間隔 ≥ 7 排並留 2 條安全線道
    //  - 路段結尾與終點平台無縫銜接 (最後路段結束排 = totalRows - 4)
    static readonly int[] ALL5 = { -2, -1, 0, 1, 2 };
    static readonly int[] MID3 = { -1, 0, 1 };
    private const int EXTRA_WARMUP_ROWS = 10;
    private int hazardKeepEvery = 1;
    private int hazardCounter = 0;

    void HazardAt(Transform parent, int col, int row)
    {
        if (hazardKeepEvery <= 0) return;
        if ((hazardCounter++ % hazardKeepEvery) != 0) return;
        CreateHazard(parent, new Vector3(col * COL_WIDTH, 0.5f, row * ROW_LEN));
    }

    void GemAt(Transform parent, float col, int row)
    {
        CreateGem(parent, new Vector3(col * COL_WIDTH, 0, row * ROW_LEN));
    }

    void GemLine(Transform parent, float col, int fromRow, int toRowExclusive, int step)
    {
        for (int r = fromRow; r < toRowExclusive; r += step) GemAt(parent, col, r);
    }

    // 寬幅跳台：來源路段每一條線道都有跳台，僅中間那一座附帶空中導引寶石
    void WideGap(Transform parent, int jumpRow, int landingRow, int[] cols)
    {
        for (int i = 0; i < cols.Length; i++)
            CreateJumpOverGap(parent, jumpRow, landingRow, cols[i], 0f, i == cols.Length / 2);
        // 斷層前緣多補 1 排地磚，縮短實際空洞 (手動跳也更容易過)
        BuildSection(parent, jumpRow + 1, jumpRow + 2, cols);
    }

    // ---------- LEVEL 1 : 霓虹大道 (Neon Avenue) ----------
    void BuildLevel_1(Transform parent)
    {
        // 總排數 110：長暖身 + 僅 2 處短斷層 + 全程 5 線道、僅 4 個尖刺 (極度寬容)
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 34, ALL5);

        BuildSection(parent, 34, 58, ALL5);
        for (int r = 36; r < 56; r += 3)
            CreateGem(parent, new Vector3(Mathf.Sin(r * 0.4f) * COL_WIDTH, 0, r * ROW_LEN));
        HazardAt(parent, -2, 48);
        HazardAt(parent, 2, 52);

        WideGap(parent, 57, 62, ALL5);

        BuildSection(parent, 62, 106, ALL5);
        for (int r = 66; r < 104; r += 4)
        {
            GemAt(parent, -1.5f, r);
            GemAt(parent, 1.5f, r);
        }
        HazardAt(parent, 0, 82);
        HazardAt(parent, -2, 96);

        FinishLevelSetup(parent, 110);
    }

    // ---------- LEVEL 2 : 虛空躍升 (Void Leap) ----------
    void BuildLevel_2(Transform parent)
    {
        // 總排數 124：3 處斷層，全程 5 線道、尖刺稀疏
        BuildSection(parent, 0, 28, ALL5);
        GemLine(parent, 0, 4, 27, 4);

        WideGap(parent, 27, 32, ALL5);

        BuildSection(parent, 32, 56, ALL5);
        GemLine(parent, -2, 36, 54, 4);
        GemLine(parent, 2, 38, 54, 4);
        HazardAt(parent, -2, 44);
        HazardAt(parent, 2, 52);

        WideGap(parent, 55, 60, ALL5);

        BuildSection(parent, 60, 84, ALL5);
        GemLine(parent, 0, 64, 82, 4);
        HazardAt(parent, -1, 70);
        HazardAt(parent, 1, 78);

        WideGap(parent, 83, 88, ALL5);

        BuildSection(parent, 88, 120, ALL5);
        GemLine(parent, 0, 92, 118, 4);
        HazardAt(parent, 0, 100);
        HazardAt(parent, 2, 110);

        FinishLevelSetup(parent, 124);
    }

    // ---------- LEVEL 3 : 疾速迴旋 (Speed Spiral) ----------
    void BuildLevel_3(Transform parent)
    {
        // 總排數 136：左偏/右偏 3 線道彎道 (前方有 5 線道緩衝)，尖刺大幅減少
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 34, ALL5);

        BuildSection(parent, 34, 40, ALL5);
        BuildSection(parent, 40, 60, new int[] { -2, -1, 0 });
        HazardAt(parent, 0, 48);
        GemAt(parent, -1, 48);
        HazardAt(parent, -2, 56);
        GemAt(parent, -1, 56);

        BuildSection(parent, 60, 66, ALL5);
        BuildSection(parent, 66, 86, new int[] { 0, 1, 2 });
        HazardAt(parent, 0, 74);
        GemAt(parent, 1, 74);
        HazardAt(parent, 2, 82);
        GemAt(parent, 1, 82);

        WideGap(parent, 85, 90, new int[] { 0, 1, 2 });

        BuildSection(parent, 90, 96, ALL5);
        BuildSection(parent, 96, 114, MID3);
        HazardAt(parent, -1, 102);
        GemAt(parent, 0, 102);
        HazardAt(parent, 1, 108);
        GemAt(parent, 0, 108);

        WideGap(parent, 113, 118, MID3);

        BuildSection(parent, 118, 132, ALL5);
        for (int r = 120; r < 130; r += 3)
            CreateGem(parent, new Vector3(Mathf.Sin(r * 0.3f) * COL_WIDTH, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 136);
    }

    // ---------- LEVEL 4 : 斷崖連跳 (Chasm Bounce) ----------
    void BuildLevel_4(Transform parent)
    {
        // 總排數 150：5 連跳 (斷層 5 排空缺)，浮島收窄為 3 線道但前有 5 線道緩衝，尖刺僅 2 個
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 35, ALL5);
        BuildSection(parent, 35, 50, ALL5);
        GemLine(parent, 0, 37, 49, 2);

        WideGap(parent, 49, 55, ALL5);
        BuildSection(parent, 55, 61, ALL5);
        BuildSection(parent, 61, 72, MID3);
        GemLine(parent, 0, 63, 71, 3);

        WideGap(parent, 71, 77, MID3);
        BuildSection(parent, 77, 83, ALL5);
        BuildSection(parent, 83, 100, MID3);
        HazardAt(parent, -1, 92);
        GemAt(parent, 1, 92);

        WideGap(parent, 99, 105, MID3);
        BuildSection(parent, 105, 111, ALL5);
        BuildSection(parent, 111, 130, MID3);
        HazardAt(parent, 1, 120);
        GemAt(parent, 0, 120);

        WideGap(parent, 129, 135, MID3);
        BuildSection(parent, 135, 146, ALL5);
        GemLine(parent, 0, 137, 145, 3);

        FinishLevelSetup(parent, 150);
    }

    // ---------- LEVEL 5 : 迷宮尖刺 (Spike Labyrinth) ----------
    void BuildLevel_5(Transform parent)
    {
        // 總排數 168：仍是尖刺主題，但每 5~8 排才 1 個、單排最多 1 個 (原本每 3 排 2 個)
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        // 迷宮第 1 區：5 線道，單一尖刺循環換位
        BuildSection(parent, 30, 70, ALL5);
        int[] cyc = { -1, 1, 0, -2, 2, 0 };
        int idx = 0;
        for (int r = 36; r < 66; r += 5, idx++)
        {
            int c = cyc[idx % cyc.Length];
            HazardAt(parent, c, r);
            GemAt(parent, -c, r);
        }

        WideGap(parent, 69, 75, ALL5);

        // 迷宮第 2 區：5 線道緩衝後 3 線道，僅中央釘刺，兩側皆安全
        BuildSection(parent, 75, 81, ALL5);
        BuildSection(parent, 81, 110, MID3);
        for (int r = 88; r < 108; r += 8)
        {
            HazardAt(parent, 0, r);
            GemAt(parent, (r / 8) % 2 == 0 ? -1 : 1, r);
        }

        WideGap(parent, 109, 115, MID3);

        // 迷宮第 3 區：5 線道，每 6 排 1 個尖刺
        BuildSection(parent, 115, 150, ALL5);
        idx = 0;
        for (int r = 126; r < 146; r += 6, idx++)
        {
            int c = cyc[(idx + 2) % cyc.Length];
            HazardAt(parent, c, r);
            GemAt(parent, -c, r);
        }

        WideGap(parent, 149, 155, ALL5);

        BuildSection(parent, 155, 164, ALL5);
        GemLine(parent, 0, 158, 164, 3);

        FinishLevelSetup(parent, 168);
    }

    // ---------- LEVEL 6 : 升降律動 (Piston Pulse) ----------
    void BuildLevel_6(Transform parent)
    {
        // 總排數 180：4 道連環大跳；障礙減為每路段 2~3 個，全程有 2 條以上安全線道
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 35, ALL5);
        BuildSection(parent, 35, 41, ALL5);
        BuildSection(parent, 41, 66, MID3);
        GemLine(parent, 0, 43, 65, 4);
        HazardAt(parent, -1, 50);
        HazardAt(parent, 1, 60);

        WideGap(parent, 65, 71, MID3);
        BuildSection(parent, 71, 77, ALL5);
        BuildSection(parent, 77, 102, MID3);
        GemLine(parent, 0, 79, 101, 4);
        HazardAt(parent, 1, 86);
        HazardAt(parent, -1, 96);

        WideGap(parent, 101, 107, MID3);
        BuildSection(parent, 107, 140, ALL5);
        GemLine(parent, 0, 110, 138, 5);
        HazardAt(parent, -2, 118);
        HazardAt(parent, 2, 125);
        HazardAt(parent, -1, 132);

        WideGap(parent, 139, 145, ALL5);
        BuildSection(parent, 145, 176, ALL5);
        GemLine(parent, 0, 148, 174, 4);

        FinishLevelSetup(parent, 180);
    }

    // ---------- LEVEL 7 : 浮島穿梭 (Floating Islands) ----------
    void BuildLevel_7(Transform parent)
    {
        // 總排數 202：島嶼接力；每座島先有 8 排 5 線道落地緩衝，窄橋放寬為 3 線道，尖刺僅 2 個
        BuildSection(parent, 0, 28, ALL5);
        GemLine(parent, 0, 4, 27, 4);

        WideGap(parent, 27, 33, ALL5);
        BuildSection(parent, 33, 41, ALL5);
        BuildSection(parent, 41, 59, new int[] { -2, -1, 0 });
        GemLine(parent, -1, 43, 58, 3);

        WideGap(parent, 58, 64, new int[] { -2, -1, 0 });
        BuildSection(parent, 64, 72, ALL5);
        BuildSection(parent, 72, 90, new int[] { 0, 1, 2 });
        GemLine(parent, 1, 74, 89, 3);

        WideGap(parent, 89, 95, new int[] { 0, 1, 2 });
        BuildSection(parent, 95, 103, ALL5);
        BuildSection(parent, 103, 121, MID3);
        HazardAt(parent, 0, 112);
        GemAt(parent, -1, 112);

        WideGap(parent, 120, 126, MID3);
        BuildSection(parent, 126, 134, ALL5);
        BuildSection(parent, 134, 152, new int[] { 0, 1, 2 });
        HazardAt(parent, 1, 142);
        GemAt(parent, 0, 142);

        WideGap(parent, 151, 157, new int[] { 0, 1, 2 });
        BuildSection(parent, 157, 175, ALL5);
        for (int r = 159; r < 173; r += 4)
            CreateGem(parent, new Vector3(Mathf.Sin(r) * COL_WIDTH, 0, r * ROW_LEN));

        WideGap(parent, 174, 180, ALL5);
        BuildSection(parent, 180, 198, ALL5);
        GemLine(parent, 0, 183, 197, 3);

        FinishLevelSetup(parent, 202);
    }

    // ---------- LEVEL 8 : 重力逆襲 (Gravity Shift) ----------
    void BuildLevel_8(Transform parent)
    {
        // 總排數 218：雙峽谷縮短、峽谷內無尖刺；其餘路段尖刺每 7 排 1 個
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 35, ALL5);
        BuildSection(parent, 35, 47, ALL5);

        // 雙子峽谷 (22 排，無障礙，邊緣有寶石導引)
        int[] canyonL = { -2, -1 };
        int[] canyonR = { 1, 2 };
        BuildSection(parent, 47, 69, canyonL);
        BuildSection(parent, 47, 69, canyonR);
        for (int r = 50; r < 68; r += 4)
        {
            GemAt(parent, -1.5f, r);
            GemAt(parent, 1.5f, r);
        }

        WideGap(parent, 68, 74, new int[] { -2, -1, 1, 2 });

        BuildSection(parent, 74, 110, ALL5);
        for (int r = 86; r < 108; r += 7)
        {
            int hx = ((r / 7) % 2 == 0) ? -1 : 1;
            HazardAt(parent, hx, r);
            GemAt(parent, -hx, r);
        }

        WideGap(parent, 109, 115, ALL5);
        BuildSection(parent, 115, 127, ALL5);
        BuildSection(parent, 127, 150, new int[] { -2, -1, 0 });
        GemLine(parent, -1, 129, 149, 4);

        WideGap(parent, 149, 155, new int[] { -2, -1, 0 });
        BuildSection(parent, 155, 167, ALL5);
        BuildSection(parent, 167, 190, new int[] { 0, 1, 2 });
        GemLine(parent, 1, 169, 189, 4);

        WideGap(parent, 189, 195, new int[] { 0, 1, 2 });
        BuildSection(parent, 195, 214, ALL5);
        GemLine(parent, 0, 198, 213, 3);

        FinishLevelSetup(parent, 218);
    }

    // ---------- LEVEL 9 : 混沌風暴 (Chaos Vortex) ----------
    void BuildLevel_9(Transform parent)
    {
        // 總排數 238：單格鋼絲橋縮短為 12 排且前後有 5 線道緩衝；其餘 3~5 線道、尖刺間隔 ≥ 6 排
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 35, ALL5);
        BuildSection(parent, 35, 47, ALL5);
        BuildSection(parent, 47, 59, MID3);           // 窄橋 1 (12 排，原單線道)
        GemLine(parent, 0, 49, 58, 3);
        BuildSection(parent, 59, 81, ALL5);
        HazardAt(parent, -1, 66);
        HazardAt(parent, 1, 73);
        GemLine(parent, 0, 62, 80, 4);

        WideGap(parent, 80, 86, ALL5);
        BuildSection(parent, 86, 98, ALL5);
        BuildSection(parent, 98, 114, MID3);
        HazardAt(parent, -1, 104);
        HazardAt(parent, 1, 110);
        GemAt(parent, 0, 104);
        GemAt(parent, 0, 110);

        WideGap(parent, 113, 119, MID3);
        BuildSection(parent, 119, 131, ALL5);
        BuildSection(parent, 131, 143, MID3);         // 窄橋 2 (12 排，原單線道)
        GemLine(parent, 0, 133, 142, 3);
        BuildSection(parent, 143, 149, ALL5);
        BuildSection(parent, 149, 168, MID3);
        HazardAt(parent, 0, 158);
        GemAt(parent, -1, 158);

        WideGap(parent, 167, 173, MID3);
        BuildSection(parent, 173, 185, ALL5);
        BuildSection(parent, 185, 214, ALL5);
        HazardAt(parent, -1, 192);
        HazardAt(parent, 1, 199);
        HazardAt(parent, 0, 206);
        GemLine(parent, 0, 188, 212, 4);

        WideGap(parent, 213, 219, ALL5);
        BuildSection(parent, 219, 234, ALL5);
        GemLine(parent, 0, 222, 234, 3);

        FinishLevelSetup(parent, 238);
    }

    // ---------- LEVEL 10 : 終極躍動 (Apex Transcendence) ----------
    void BuildLevel_10(Transform parent)
    {
        // 總排數 250：仍集結所有機關 (3 線道、雙峽谷、鋼絲橋、連續大跳)，但尖刺稀疏、緩衝平台加長
        BuildSection(parent, 0, 30, ALL5);
        GemLine(parent, 0, 4, 29, 4);

        WideGap(parent, 29, 35, ALL5);
        BuildSection(parent, 35, 41, ALL5);
        BuildSection(parent, 41, 65, MID3);
        GemLine(parent, 0, 43, 64, 4);
        HazardAt(parent, -1, 52);
        HazardAt(parent, 1, 60);

        WideGap(parent, 64, 70, MID3);
        BuildSection(parent, 70, 100, ALL5);
        GemLine(parent, 0, 74, 98, 4);
        HazardAt(parent, 1, 88);
        HazardAt(parent, -1, 95);

        WideGap(parent, 99, 105, ALL5);
        BuildSection(parent, 105, 115, ALL5);

        // 雙子峽谷
        BuildSection(parent, 115, 139, new int[] { -2, -1 });
        BuildSection(parent, 115, 139, new int[] { 1, 2 });
        for (int r = 118; r < 138; r += 4)
        {
            GemAt(parent, -1.5f, r);
            GemAt(parent, 1.5f, r);
        }

        WideGap(parent, 138, 144, new int[] { -2, -1, 1, 2 });
        BuildSection(parent, 144, 154, ALL5);
        BuildSection(parent, 154, 168, MID3);         // 窄橋 (14 排，原單線道)
        GemLine(parent, 0, 156, 167, 3);
        BuildSection(parent, 168, 186, ALL5);
        HazardAt(parent, -2, 176);

        WideGap(parent, 185, 191, ALL5);
        BuildSection(parent, 191, 203, ALL5);
        BuildSection(parent, 203, 226, MID3);
        HazardAt(parent, -1, 208);
        HazardAt(parent, 1, 215);
        HazardAt(parent, 0, 221);
        GemLine(parent, 0, 205, 224, 4);

        WideGap(parent, 225, 231, MID3);
        BuildSection(parent, 231, 246, ALL5);
        for (int r = 233; r <= 245; r += 2)
        {
            GemAt(parent, -1, r);
            GemAt(parent, 1, r);
        }

        FinishLevelSetup(parent, 250);
    }

    // ==================== 無盡模式 (Endless Mode - 保證連續可通行生成系統) ====================
    public void StartEndlessMode()
    {
        isEndlessActive = true;
        currentFarthestZ = 0f;
        activeChunks.Clear();

        GameObject root = GetOrCreateTrackRoot(clearExisting: true);

        // 初始前導安全跑道 (長度 40m，共 20 排，5 線道全通)
        GameObject startChunk = new GameObject("Endless_StartChunk");
        startChunk.transform.SetParent(root.transform);
        // (易化版) 起跑安全區加長為 30 排 (60m)
        endlessChunkCount = 0;
        BuildSection(startChunk.transform, 0, 30, new int[] { -2, -1, 0, 1, 2 });
        for (int z = 8; z < 56; z += 6)
        {
            CreateAirborneGem(startChunk.transform, new Vector3(0, 1.15f, z));
        }
        currentFarthestZ = 60.0f;
        activeChunks.Add(new EndlessChunkEntry { go = startChunk, startZ = 0f, endZ = 60.0f });

        // 預先生成前方 6 個區塊，使起跑時前方恆定維持 240 米以上連續跑道
        for (int i = 0; i < 6; i++)
        {
            SpawnEndlessChunk();
        }
    }

    void SpawnEndlessChunk()
    {
        // 關鍵修復：clearExisting 恆為 false，絕對不銷毀舊有跑道
        GameObject root = GetOrCreateTrackRoot(clearExisting: false);
        float chunkStartZ = currentFarthestZ;
        int rowsInChunk = 20; // 每個區塊 20 排 = 40 米
        float chunkEndZ = chunkStartZ + (rowsInChunk * ROW_LEN);

        GameObject chunk = new GameObject($"EndlessChunk_{chunkStartZ:F0}_{chunkEndZ:F0}");
        chunk.transform.SetParent(root.transform);

        int startRow = Mathf.RoundToInt(chunkStartZ / ROW_LEN);

        // 【無盡模式隨機性全面升級】：
        // 1. 動態左右鏡像翻轉 (50% 機率反轉軌道佈局)
        // 2. 擴展為 14 種獨特風格地圖區塊 (0 ~ 13)
        // 3. 障礙物與寶石動態隨機擾動
        bool mirror = (Random.value > 0.5f);
        float M(float val) => mirror ? -val : val;
        int M_Col(int val) => mirror ? -val : val;
        int[] MCols(int[] cols)
        {
            if (!mirror) return cols;
            int[] res = new int[cols.Length];
            for (int k = 0; k < cols.Length; k++) res[k] = -cols[k];
            return res;
        }

        // (易化版) 無盡模式前期只出簡單區塊，逐步開放全部 14 種
        int chunkType;
        if (endlessChunkCount < 14)
            chunkType = EasyEndlessPool[Random.Range(0, EasyEndlessPool.Length)];
        else if (endlessChunkCount < 28)
            chunkType = MediumEndlessPool[Random.Range(0, MediumEndlessPool.Length)];
        else
            chunkType = Random.Range(0, 14);
        endlessChunkCount++;

        switch (chunkType)
        {
            case 0: // 【霓虹高速巡航大道】寬敞 5 線道衝刺，交錯單側尖刺與高亮金鑽 (動態隨機障礙)
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                for (int r = startRow + 3; r < startRow + rowsInChunk - 2; r += 6) // 原每 4 排 → 每 6 排
                {
                    int spikeCol = (Random.value > 0.5f) ? M_Col(-1) : M_Col(1);
                    if (Random.value > 0.6f) spikeCol = (spikeCol == -1) ? M_Col(-2) : M_Col(2);
                    CreateHazard(chunk.transform, new Vector3(spikeCol * COL_WIDTH, 0.5f, r * ROW_LEN));
                    CreateAirborneGem(chunk.transform, new Vector3(M(0f), 1.15f, (r + 2) * ROW_LEN));
                }
                break;

            case 1: // 【虛空躍崖大斷層】彈跳墊跨越 10 米深淵！空中懸浮璀璨彩虹金鑽弧線，遠方寬敞 5 線道著陸台
                BuildSection(chunk.transform, startRow, startRow + 5, MCols(new int[] { -1, 0, 1 }));
                // 於斷層邊緣第 4 排設置彈跳墊 (受小球全域冷卻守護，絕不超高飛昇)
                int pRow1 = startRow + 4;
                CreateJumpPad(chunk.transform, new Vector3(M(-COL_WIDTH), 0.1f, pRow1 * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(0, 0.1f, pRow1 * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(M(COL_WIDTH), 0.1f, pRow1 * ROW_LEN));

                // 斷層空中懸浮引導金鑽 (5 顆拋物線金鑽)
                float[] arcH1 = new float[] { 2.4f, 3.8f, 4.8f, 3.8f, 2.4f };
                for (int h = 0; h < arcH1.Length; h++)
                {
                    CreateAirborneGem(chunk.transform, new Vector3(0, arcH1[h], (pRow1 + 1 + h) * ROW_LEN));
                }
                // 斷層深淵 (pRow1+1 至 pRow1+5 完全懸空)，著陸平台自第 10 排開始！
                BuildSection(chunk.transform, startRow + 10, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 2: // 【雙重手動跳躍斷層】考驗空白鍵精準起跳！兩處 4 米虛空斷崖，金鑽懸於裂口正上方導引
                BuildSection(chunk.transform, startRow, startRow + 5, MCols(new int[] { -1, 0, 1 }));
                CreateAirborneGem(chunk.transform, new Vector3(0, 2.4f, (startRow + 5.5f) * ROW_LEN)); // 1 號斷層導引金鑽
                // 裂口：第 5, 6 排為 4 米深淵斷層
                BuildSection(chunk.transform, startRow + 7, startRow + 12, MCols(new int[] { -1, 0, 1 }));
                CreateAirborneGem(chunk.transform, new Vector3(0, 2.4f, (startRow + 12.5f) * ROW_LEN)); // 2 號斷層導引金鑽
                // 裂口：第 12, 13 排為 4 米深淵斷層
                BuildSection(chunk.transform, startRow + 14, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 3: // 【虛空浮石連續斷層群】交替孤島飛躍！左右穿梭跳台，考驗搖桿/左右鍵靈敏變道
                BuildSection(chunk.transform, startRow, startRow + 3, MCols(new int[] { -2, -1 }));
                CreateAirborneGem(chunk.transform, new Vector3(M(-1.5f * COL_WIDTH), 1.15f, (startRow + 1) * ROW_LEN));
                // 第 3 排為斷層
                BuildSection(chunk.transform, startRow + 4, startRow + 7, MCols(new int[] { 1, 2 }));
                CreateAirborneGem(chunk.transform, new Vector3(M(1.5f * COL_WIDTH), 1.15f, (startRow + 5) * ROW_LEN));
                // 第 7 排為斷層
                BuildSection(chunk.transform, startRow + 8, startRow + 11, MCols(new int[] { -1, 0, 1 }));
                CreateAirborneGem(chunk.transform, new Vector3(0, 1.15f, (startRow + 9) * ROW_LEN));
                // 第 11 排為斷層
                BuildSection(chunk.transform, startRow + 12, startRow + 15, MCols(new int[] { -2, -1 }));
                CreateAirborneGem(chunk.transform, new Vector3(M(-1.5f * COL_WIDTH), 1.15f, (startRow + 13) * ROW_LEN));
                // 第 15 排為斷層
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 4: // 【虛空細樑獨木橋】兩側皆為無底深淵！僅中央單線道橫跨 22 米高空，極致心跳挑戰
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -2, -1, 0, 1, 2 });
                // 兩側截斷，僅保留 0 號線道高空窄樑 (隨機帶微偏)
                int bridgeCol = (Random.value > 0.7f) ? M_Col(1) : 0;
                BuildSection(chunk.transform, startRow + 3, startRow + 16, new int[] { bridgeCol });
                for (int r = startRow + 4; r < startRow + 16; r += 3)
                {
                    CreateAirborneGem(chunk.transform, new Vector3(bridgeCol * COL_WIDTH, 1.15f, r * ROW_LEN));
                }
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 5: // 【空中大跳躍長廊】深淵飛越與全速衝刺 (統一 0f 高度，消除階梯下墜落差)
                BuildSection(chunk.transform, startRow, startRow + 5, MCols(new int[] { -1, 0, 1 }));
                CreateJumpOverGap(chunk.transform, startRow + 4, startRow + 8, M_Col(0));
                BuildSection(chunk.transform, startRow + 8, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateGem(chunk.transform, new Vector3(0, 0, (startRow + 12) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(0, 0, (startRow + 16) * ROW_LEN));
                break;

            case 6: // 【雙分叉中央大深淵】正中間 6 米全鏤空深淵！左右兩側獨立飛馳
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -2, -1, 0, 1, 2 });
                // 中央 3 線道鏤空，僅留左右外側
                BuildSection(chunk.transform, startRow + 3, startRow + 16, new int[] { -2, 2 });
                CreateAirborneGem(chunk.transform, new Vector3(-COL_WIDTH * 2, 1.15f, (startRow + 6) * ROW_LEN));
                CreateHazard(chunk.transform, new Vector3(-COL_WIDTH * 2, 0.5f, (startRow + 10) * ROW_LEN));
                CreateHazard(chunk.transform, new Vector3(COL_WIDTH * 2, 0.5f, (startRow + 6) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(COL_WIDTH * 2, 1.15f, (startRow + 10) * ROW_LEN));
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateAirborneGem(chunk.transform, new Vector3(0, 1.15f, (startRow + 18) * ROW_LEN));
                break;

            case 7: // 【連環雙重彈射深淵】兩段式連續空中大跳躍！爽快感翻倍
                BuildSection(chunk.transform, startRow, startRow + 3, MCols(new int[] { -1, 0, 1 }));
                // 第 1 級跳台：由 startRow+2 跳至 startRow+6
                CreateJumpOverGap(chunk.transform, startRow + 2, startRow + 6, M_Col(0));

                // 第 3~5 排為斷層，落在第 6~8 排空中島
                BuildSection(chunk.transform, startRow + 6, startRow + 9, MCols(new int[] { -1, 0, 1 }));
                // 第 2 級跳台：由 startRow+8 跳至 startRow+12
                CreateJumpOverGap(chunk.transform, startRow + 8, startRow + 12, M_Col(0));

                // 第 9~11 排為斷層，落在第 12 排大平台
                BuildSection(chunk.transform, startRow + 12, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 8: // 【流暢 S 彎變道】3 線道蛇行穿梭，順滑過彎
                for (int i = 0; i < rowsInChunk; i++)
                {
                    int r = startRow + i;
                    int centerCol = (i < 5) ? -1 : (i < 10) ? 0 : (i < 15) ? 1 : 0;
                    centerCol = M_Col(centerCol);
                    int[] activeCols = new int[] { centerCol - 1, centerCol, centerCol + 1 };
                    foreach (int c in activeCols)
                    {
                        if (c >= -2 && c <= 2) CreateSingleTile(chunk.transform, c, r);
                    }
                    if (i % 3 == 0) CreateAirborneGem(chunk.transform, new Vector3(centerCol * COL_WIDTH, 1.15f, r * ROW_LEN));
                }
                break;

            case 9: // 【金鑽狂歡長廊】無障礙大獎勵！高密度金光閃閃鑽石陣列
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateAirborneGem(chunk.transform, new Vector3(M(-COL_WIDTH), 1.15f, (startRow + 3) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(COL_WIDTH), 1.15f, (startRow + 3) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(0, 1.15f, (startRow + 6) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(-COL_WIDTH * 1.5f), 1.15f, (startRow + 10) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(COL_WIDTH * 1.5f), 1.15f, (startRow + 10) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(0, 1.15f, (startRow + 14) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(-COL_WIDTH), 1.15f, (startRow + 17) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(COL_WIDTH), 1.15f, (startRow + 17) * ROW_LEN));
                break;

            case 10: // 【全新 10：量子交錯斷層帶】左右交替雙線道穿梭，中段深淵跳台飛越
                BuildSection(chunk.transform, startRow, startRow + 4, MCols(new int[] { -2, -1, 0 }));
                CreateHazard(chunk.transform, new Vector3(M(-COL_WIDTH * 2), 0.5f, (startRow + 2) * ROW_LEN));
                CreateJumpOverGap(chunk.transform, startRow + 3, startRow + 8, M_Col(-1));
                BuildSection(chunk.transform, startRow + 8, startRow + 13, MCols(new int[] { 0, 1, 2 }));
                CreateAirborneGem(chunk.transform, new Vector3(M(COL_WIDTH * 1.5f), 1.15f, (startRow + 10) * ROW_LEN));
                BuildSection(chunk.transform, startRow + 13, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 11: // 【全新 11：疾速鋸齒穿梭峽谷】交錯尖刺陣列，考驗左右蛇行反應與防側風抗干擾
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                for (int i = 3; i < rowsInChunk - 2; i += 5) // 原每 3 排 → 每 5 排
                {
                    int sCol = (i % 2 == 0) ? M_Col(-1) : M_Col(1);
                    CreateHazard(chunk.transform, new Vector3(sCol * COL_WIDTH, 0.5f, (startRow + i) * ROW_LEN));
                    CreateAirborneGem(chunk.transform, new Vector3(-sCol * COL_WIDTH, 1.15f, (startRow + i) * ROW_LEN));
                }
                break;

            case 12: // 【全新 12：深淵孤島單一彈跳躍進】中央孤立跳台，清爽單次超遠飛躍，著陸平穩
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -1, 0, 1 });
                // 孤立單跳台，前後皆為深淵
                CreateJumpPad(chunk.transform, new Vector3(M(0f), 0.1f, (startRow + 2) * ROW_LEN));
                // 空中拋物線寶石
                CreateAirborneGem(chunk.transform, new Vector3(M(0f), 2.8f, (startRow + 4) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(0f), 3.9f, (startRow + 6) * ROW_LEN));
                CreateAirborneGem(chunk.transform, new Vector3(M(0f), 2.8f, (startRow + 8) * ROW_LEN));
                // 安全寬敞著陸島
                BuildSection(chunk.transform, startRow + 9, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 13: // 【全新 13：霓虹棋盤殘破危道】交錯鏤空格子路，玩家須精準控球
                for (int i = 0; i < rowsInChunk; i++)
                {
                    int r = startRow + i;
                    for (int c = -2; c <= 2; c++)
                    {
                        if ((Mathf.Abs(c) + i) % 2 == 0 || c == 0)
                        {
                            CreateSingleTile(chunk.transform, c, r);
                        }
                    }
                    if (i % 4 == 0)
                    {
                        CreateAirborneGem(chunk.transform, new Vector3(M((i % 2 == 0) ? -COL_WIDTH : COL_WIDTH), 1.15f, r * ROW_LEN));
                    }
                }
                break;
        }

        currentFarthestZ = chunkEndZ;
        activeChunks.Add(new EndlessChunkEntry { go = chunk, startZ = chunkStartZ, endZ = chunkEndZ });
    }

    // ==================== 自訂場地模式 (Track Builder) ====================
    public void BuildCustomTrack()
    {
        string path = Application.persistentDataPath + "/custom_track.json";
        if (!File.Exists(path))
        {
            // 若無自訂存檔，建立一份預設自訂範例
            BuildLevel_1(GetOrCreateTrackRoot(clearExisting: true).transform);
            return;
        }

        string json = File.ReadAllText(path);
        CustomTrackData trackData = JsonUtility.FromJson<CustomTrackData>(json);
        if (trackData == null || trackData.tiles == null) return;

        GameObject root = GetOrCreateTrackRoot(clearExisting: true);
        Transform parent = root.transform;
        int highestRow = 0;
        foreach (var tile in trackData.tiles)
        {
            if (tile.row > highestRow) highestRow = tile.row;

            Vector3 pos = new Vector3(tile.col * COL_WIDTH, tile.y, tile.row * ROW_LEN);
            switch (tile.type)
            {
                case 1: // 普通磚
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    break;
                case 2: // 彈跳墊
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateJumpPad(parent, new Vector3(pos.x, pos.y, pos.z));
                    break;
                case 3: // 尖刺
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateHazard(parent, new Vector3(pos.x, pos.y + 0.5f, pos.z));
                    break;
                case 4: // 寶石
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateGem(parent, new Vector3(pos.x, pos.y, pos.z));
                    break;
            }
        }

        // 終點星門動態放置在玩家最高建造行之後 3 排
        int finishRow = Mathf.Max(highestRow + 3, 20);
        CreateFinishGate(parent, new Vector3(0, 0f, finishRow * ROW_LEN));

        RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
        if (gm != null)
        {
            gm.trackLength = finishRow * ROW_LEN;
            gm.ResetLevelStats();
        }
    }

    // ==================== 幾何元件生成輔助 ====================
    void BuildSection(Transform parent, int startRow, int endRow, int[] activeCols, float height = 0f)
    {
        for (int r = startRow; r < endRow; r++)
        {
            float z = r * ROW_LEN;
            foreach (int c in activeCols)
            {
                CreateSingleTile(parent, c, r, height);
            }
        }
    }

    void CreateSingleTile(Transform parent, int col, int row, float height = 0f)
    {
        float x = col * COL_WIDTH;
        float z = row * ROW_LEN;
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = $"Tile_{col}_{row}";
        tile.transform.SetParent(parent);
        tile.transform.position = new Vector3(x, height, z);
        tile.transform.localScale = new Vector3(COL_WIDTH * 0.96f, 0.4f, ROW_LEN * 0.96f);

        // 棋盤格交替色
        bool isAlt = (Mathf.Abs(col) + row) % 2 == 0;
        tile.GetComponent<MeshRenderer>().material = isAlt ? matTileDark : matTileCyan;

        // ★ 關鍵：將 BoxCollider 尺寸擴展為 100% 無縫覆蓋 (消除 0.96 留下的 8cm 縫隙撞角)
        BoxCollider bc = tile.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.size = new Vector3(1f / 0.96f, 1f, 1f / 0.96f);
            if (tilePhysicMaterial != null) bc.sharedMaterial = tilePhysicMaterial;
        }
    }

    void CreateHazard(Transform parent, Vector3 pos)
    {
        GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazard.name = "Hazard";
        hazard.transform.SetParent(parent);
        hazard.transform.position = pos;
        hazard.transform.localScale = new Vector3(1.4f, 1.0f, 1.4f);
        hazard.GetComponent<MeshRenderer>().material = matHazard;
        hazard.AddComponent<Hazard>();
    }

    void CreateJumpPad(Transform parent, Vector3 pos, float force = 0f)
    {
        // 跳躍墊抬高至 y = pos.y + 0.25f，座落於地磚 (頂面 0.20f) 之上，徹底根除 Z-Fighting 貼圖穿模閃爍
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "JumpPad";
        pad.transform.SetParent(parent);
        pad.transform.position = new Vector3(pos.x, pos.y + 0.25f, pos.z);
        pad.transform.localScale = new Vector3(COL_WIDTH * 0.92f, 0.12f, ROW_LEN * 0.92f);
        pad.GetComponent<MeshRenderer>().material = matJumpPad;
        BoxCollider bc = pad.GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = true;
            // 充分覆蓋左右邊緣 (X/Z: 1.45f) 並向上延伸感應範圍 (Y: 10.0f * 0.12f = 1.2m)
            bc.size = new Vector3(1.45f, 10.0f, 1.45f);
            bc.center = new Vector3(0, 5.0f, 0);
        }
        JumpPad jp = pad.AddComponent<JumpPad>();
        if (force > 0f) jp.launchForce = force;
    }

    void CreateGem(Transform parent, Vector3 pos)
    {
        // 寶石懸浮高度預設抬升至 y = pos.y + 1.15f，配合半徑 0.5f 與微幅漂浮，確保寶石下端絕不陷入地板
        GameObject gem = new GameObject("Gem");
        gem.transform.SetParent(parent);
        gem.transform.position = new Vector3(pos.x, pos.y + 1.15f, pos.z);
        gem.transform.localScale = Vector3.one;
        AddGemCollider(gem);
        gem.AddComponent<GemCollectible>();
    }

    // GemCollectible 需要 [RequireComponent(typeof(Collider))]，而 Collider 是抽象類別、Unity 無法自動補上；
    // 必須先掛好觸發用 SphereCollider，否則 AddComponent<GemCollectible> 會失敗，寶石不會出現且數量顯示 0 / 0
    static void AddGemCollider(GameObject gem)
    {
        SphereCollider sc = gem.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.55f;
    }

    void CreateAirborneGem(Transform parent, Vector3 pos)
    {
        // 高空/跳台拋物線專用懸浮寶石 (直接使用傳入之精確空中 Y 座標)
        GameObject gem = new GameObject("Gem_Airborne");
        gem.transform.SetParent(parent);
        gem.transform.position = pos;
        gem.transform.localScale = Vector3.one;
        AddGemCollider(gem);
        gem.AddComponent<GemCollectible>();
    }

    void CreateJumpOverGap(Transform parent, int jumpRow, int landingRow, int col, float height = 0f, bool withGems = true)
    {
        int deltaRows = landingRow - jumpRow;
        // 計算精準彈射力量：配合 forwardSpeed = 12m/s (每秒 6 排)
        // 飛行時間計算：使小球恰好飛越深淵，落在 landingRow 後方第 1~2 排之安全著陸區
        float flightRows = deltaRows + 1.2f;
        float airTime = flightRows * ROW_LEN / 12.0f;
        // 拋物線重力公式：v0 = 0.5 * g * airTime
        float requiredForce = Mathf.Clamp(0.5f * 9.81f * airTime, 8.5f, 18.0f);

        // 放置跳躍墊
        CreateJumpPad(parent, new Vector3(col * COL_WIDTH, height, jumpRow * ROW_LEN), requiredForce);

        // 空中拋物線最高點高度與導引寶石
        float apexY = height + (requiredForce * requiredForce) / (2f * 9.81f);
        float midZ = (jumpRow + (deltaRows * 0.5f)) * ROW_LEN;
        float q1Z = (jumpRow + (deltaRows * 0.25f)) * ROW_LEN;
        float q3Z = (jumpRow + (deltaRows * 0.75f)) * ROW_LEN;

        if (!withGems) return;
        CreateAirborneGem(parent, new Vector3(col * COL_WIDTH, height + (apexY - height) * 0.7f, q1Z));
        CreateAirborneGem(parent, new Vector3(col * COL_WIDTH, apexY, midZ));
        CreateAirborneGem(parent, new Vector3(col * COL_WIDTH, height + (apexY - height) * 0.7f, q3Z));
    }

    void CreateFinishGate(Transform parent, Vector3 pos)
    {
        GameObject gateRoot = new GameObject("FinishStarArchway");
        gateRoot.transform.SetParent(parent);
        gateRoot.transform.position = pos;

        // 1. 左側霓虹拱門立柱
        GameObject pillarL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillarL.name = "Pillar_Left";
        pillarL.transform.SetParent(gateRoot.transform);
        pillarL.transform.localPosition = new Vector3(-COL_WIDTH * 2.3f, 2.0f, 0);
        pillarL.transform.localScale = new Vector3(0.5f, 4.0f, 0.8f);
        pillarL.GetComponent<MeshRenderer>().material = matGoal;
        Destroy(pillarL.GetComponent<Collider>());

        // 2. 右側霓虹拱門立柱
        GameObject pillarR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillarR.name = "Pillar_Right";
        pillarR.transform.SetParent(gateRoot.transform);
        pillarR.transform.localPosition = new Vector3(COL_WIDTH * 2.3f, 2.0f, 0);
        pillarR.transform.localScale = new Vector3(0.5f, 4.0f, 0.8f);
        pillarR.GetComponent<MeshRenderer>().material = matGoal;
        Destroy(pillarR.GetComponent<Collider>());

        // 3. 頂部金色橫樑牌坊
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Arch_Beam";
        beam.transform.SetParent(gateRoot.transform);
        beam.transform.localPosition = new Vector3(0, 4.0f, 0);
        beam.transform.localScale = new Vector3(COL_WIDTH * 5.1f, 0.7f, 0.8f);
        beam.GetComponent<MeshRenderer>().material = matGoal;
        Destroy(beam.GetComponent<Collider>());

        // 4. 穿越式 Trigger 通關光幕 (徹底解決實心黃色方塊擋路撞停小球問題)
        GameObject curtain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        curtain.name = "FinishTriggerCurtain";
        curtain.transform.SetParent(gateRoot.transform);
        curtain.transform.localPosition = new Vector3(0, 2.0f, 0);
        curtain.transform.localScale = new Vector3(COL_WIDTH * 4.4f, 3.8f, 0.3f);

        Material curtainMat = new Material(Shader.Find("Standard"));
        curtainMat.color = new Color(1.0f, 0.9f, 0.2f, 0.5f);
        curtainMat.SetColor("_EmissionColor", new Color(2.5f, 2.0f, 0.5f));
        curtainMat.EnableKeyword("_EMISSION");
        curtain.GetComponent<MeshRenderer>().material = curtainMat;

        BoxCollider bc = curtain.GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;
        curtain.AddComponent<FinishTrigger>();
    }

    void FinishLevelSetup(Transform parent, int totalRows)
    {
        int finishRow = totalRows - 2;
        float finishZ = finishRow * ROW_LEN;

        // 終點前及身後鋪設 6 排安全 5 線道平臺，確保小球有立足之地絕不墜崖
        BuildSection(parent, finishRow - 2, finishRow + 6, new int[] { -2, -1, 0, 1, 2 });

        // 建立星門拱門
        CreateFinishGate(parent, new Vector3(0, 0, finishZ));

        // 精準設置 GameManager.trackLength，使到達終點星門時百分之百達到 100%
        RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
        if (gm != null)
        {
            gm.trackLength = finishZ;
            gm.ResetLevelStats();
        }
    }
}

public class FinishTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        RollingSkyBallController ball = other.GetComponent<RollingSkyBallController>() ?? other.GetComponentInParent<RollingSkyBallController>();
        if (ball != null)
        {
            RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
            if (gm != null) gm.TriggerVictory();
        }
    }
}

[System.Serializable]
public class CustomTrackTile
{
    public int col;  // -2 ~ 2
    public int row;  // 0 ~ N
    public float y;
    public int type; // 1:Tile, 2:JumpPad, 3:Hazard, 4:Gem
}

[System.Serializable]
public class CustomTrackData
{
    public int totalRows = 200;
    public List<CustomTrackTile> tiles = new List<CustomTrackTile>();
}
