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
    private GameObject trackRoot;
    private PhysicsMaterial tilePhysicMaterial;

    private const float COL_WIDTH = 2.0f; // 每線道寬度 2m
    private const float ROW_LEN = 2.0f;   // 每地磚長度 2m

    void Awake()
    {
        Instance = this;
        LoadDefaultMaterials();
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
            ball.forwardSpeed = 11.0f + (level - 1) * 0.7f;
        }

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

        // 各關卡已在 FinishLevelSetup 內依 totalRows 動態設置精準的 gm.trackLength 與 ResetLevelStats
    }

    // ---------- LEVEL 1 : 霓虹大道 (Neon Avenue) ----------
    void BuildLevel_1(Transform parent)
    {
        // 總排數 120 (240m)，適合新手入門：熟悉 5 線道與 3 線道切換、跳躍墊越過深淵斷層與手動跳躍
        BuildSection(parent, 0, 20, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 4; r <= 16; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 1 處深淵斷層 (第 20~22 排懸空，第 19 排配置跳台越過斷層)
        CreateJumpOverGap(parent, 19, 23, 0);

        // 落地接 3 線道，微幅蛇行寶石與邊緣障礙
        BuildSection(parent, 23, 42, new int[] { -1, 0, 1 });
        for (int r = 25; r < 40; r += 3)
            CreateGem(parent, new Vector3(Mathf.Sin(r * 0.4f) * COL_WIDTH, 0, r * ROW_LEN));
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 30 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 36 * ROW_LEN));

        // 第 2 處深淵斷層 (第 42~47 排懸空，第 41 排配置跳台)
        CreateJumpOverGap(parent, 41, 48, 0);

        // 落地接寬敞 5 線道，交錯避障訓練
        BuildSection(parent, 48, 70, new int[] { -2, -1, 0, 1, 2 });
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 54 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 62 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 54 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 62 * ROW_LEN));

        // 第 3 處深淵斷層 (第 70~75 排懸空，第 69 排配置跳台)
        CreateJumpOverGap(parent, 69, 76, 0);

        // 落地接雙分流軌道 (左軌道與右軌道，中間為虛空斷層)
        BuildSection(parent, 76, 95, new int[] { -2, -1 });
        BuildSection(parent, 76, 95, new int[] { 1, 2 });
        for (int r = 78; r < 94; r += 4)
        {
            CreateGem(parent, new Vector3(-1.5f * COL_WIDTH, 0, r * ROW_LEN));
            CreateGem(parent, new Vector3(1.5f * COL_WIDTH, 0, r * ROW_LEN));
        }

        // 匯合回 3 線道衝刺段
        BuildSection(parent, 95, 114, new int[] { -1, 0, 1 });
        CreateHazard(parent, new Vector3(0, 0.5f, 100 * ROW_LEN));
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 106 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 106 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 100 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 106 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 110 * ROW_LEN));

        // 終點星門與 100% 精準結算
        FinishLevelSetup(parent, 120);
    }

    // ---------- LEVEL 2 : 虛空躍升 (Void Leap) ----------
    void BuildLevel_2(Transform parent)
    {
        // 總排數 135 (270m)：跳躍頻率提高，分流跳台與兩翼寶石
        BuildSection(parent, 0, 20, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 4; r <= 16; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 1 級深淵彈射
        CreateJumpOverGap(parent, 19, 27, 0);

        // 落地接雙子單軌窄橋 (-1 與 +1)，考驗平穩微調
        BuildSection(parent, 27, 50, new int[] { -1 });
        BuildSection(parent, 27, 50, new int[] { 1 });
        for (int r = 29; r < 49; r += 4)
        {
            CreateGem(parent, new Vector3(-COL_WIDTH, 0, r * ROW_LEN));
            CreateGem(parent, new Vector3(COL_WIDTH, 0, r * ROW_LEN));
        }

        // 第 2 級深淵彈射 (左右雙跳台同步)
        CreateJumpOverGap(parent, 49, 57, -1);
        CreateJumpOverGap(parent, 49, 57, 1);

        // 落地接 3 線道，穿梭避障
        BuildSection(parent, 57, 80, new int[] { -1, 0, 1 });
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 63 * ROW_LEN));
        CreateHazard(parent, new Vector3(0, 0.5f, 69 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 75 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 63 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 69 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 75 * ROW_LEN));

        // 第 3 級深淵彈射
        CreateJumpOverGap(parent, 79, 88, 0);

        // 偏右 3 線道
        BuildSection(parent, 88, 110, new int[] { 0, 1, 2 });
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 95 * ROW_LEN));
        CreateHazard(parent, new Vector3(2 * COL_WIDTH, 0.5f, 103 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 95 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 103 * ROW_LEN));

        // 第 4 級深淵彈射
        CreateJumpOverGap(parent, 109, 117, 1);

        // 落地衝刺段
        BuildSection(parent, 117, 128, new int[] { -1, 0, 1 });
        for (int r = 119; r <= 127; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 135);
    }

    // ---------- LEVEL 3 : 疾速迴旋 (Speed Spiral) ----------
    void BuildLevel_3(Transform parent)
    {
        // 總排數 150 (300m)：蛇行過彎、切角深淵斷層與跳台變道
        BuildSection(parent, 0, 20, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 4; r <= 16; r += 4)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 左偏 3 線道彎道
        BuildSection(parent, 20, 42, new int[] { -2, -1, 0 });
        CreateHazard(parent, new Vector3(0, 0.5f, 28 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 28 * ROW_LEN));
        CreateHazard(parent, new Vector3(-2 * COL_WIDTH, 0.5f, 36 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 36 * ROW_LEN));

        // 斜向斜跨彈射深淵
        CreateJumpOverGap(parent, 41, 49, -1);

        // 右偏 3 線道彎道
        BuildSection(parent, 49, 72, new int[] { 0, 1, 2 });
        CreateHazard(parent, new Vector3(0, 0.5f, 56 * ROW_LEN));
        CreateHazard(parent, new Vector3(2 * COL_WIDTH, 0.5f, 64 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 56 * ROW_LEN));
        CreateGem(parent, new Vector3(COL_WIDTH, 0, 64 * ROW_LEN));

        // 彈跳切回中央
        CreateJumpOverGap(parent, 71, 79, 1);

        // 中央 3 線道蛇行避障
        BuildSection(parent, 79, 105, new int[] { -1, 0, 1 });
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 85 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 92 * ROW_LEN));
        CreateHazard(parent, new Vector3(0, 0.5f, 99 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 85 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 92 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 99 * ROW_LEN));

        // 飛越深淵
        CreateJumpOverGap(parent, 104, 113, 0);

        // S 彎切換
        BuildSection(parent, 113, 128, new int[] { -2, -1, 0 });
        BuildSection(parent, 128, 143, new int[] { 0, 1, 2 });
        for (int r = 115; r < 140; r += 4)
            CreateGem(parent, new Vector3(Mathf.Sin(r * 0.3f) * COL_WIDTH, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 150);
    }

    // ---------- LEVEL 4 : 斷崖連跳 (Chasm Bounce) ----------
    void BuildLevel_4(Transform parent)
    {
        // 總排數 165 (330m)：連續大跨度彈射深淵與狹窄浮島
        BuildSection(parent, 0, 20, new int[] { -2, -1, 0, 1, 2 });

        // 第 1 跳
        CreateJumpOverGap(parent, 19, 28, 0);
        BuildSection(parent, 28, 40, new int[] { -1, 0, 1 });
        for (int r = 30; r < 38; r += 2)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 2 跳
        CreateJumpOverGap(parent, 39, 48, 0);
        BuildSection(parent, 48, 68, new int[] { -1, 0 }); // 左側雙線窄道
        CreateHazard(parent, new Vector3(0, 0.5f, 56 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 56 * ROW_LEN));

        // 第 3 跳
        CreateJumpOverGap(parent, 67, 77, -1);
        BuildSection(parent, 77, 100, new int[] { 0, 1 }); // 右側雙線窄道
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 86 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 86 * ROW_LEN));

        // 第 4 跳
        CreateJumpOverGap(parent, 99, 109, 1);
        BuildSection(parent, 109, 132, new int[] { -1, 0, 1 });
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 116 * ROW_LEN));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 124 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 120 * ROW_LEN));

        // 第 5 跳 (終極大跳躍)
        CreateJumpOverGap(parent, 131, 142, 0);
        BuildSection(parent, 142, 158, new int[] { -1, 0, 1 });
        for (int r = 144; r <= 156; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 165);
    }

    // ---------- LEVEL 5 : 迷宮尖刺 (Spike Labyrinth) ----------
    void BuildLevel_5(Transform parent)
    {
        // 總排數 180 (360m)：高密度尖刺陣列，考驗左右傾角微調操作與穿縫走位
        BuildSection(parent, 0, 24, new int[] { -2, -1, 0, 1, 2 });

        // 尖刺迷宮第 1 區
        BuildSection(parent, 24, 60, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 26; r < 58; r += 6)
        {
            int blocked = ((r / 6) % 2 == 0) ? 1 : -1;
            CreateHazard(parent, new Vector3(blocked * COL_WIDTH, 0.5f, r * ROW_LEN));
            CreateHazard(parent, new Vector3(0, 0.5f, (r + 2) * ROW_LEN));
            CreateGem(parent, new Vector3(-blocked * COL_WIDTH, 0, r * ROW_LEN));
        }

        // 跳台飛躍迷宮斷層
        CreateJumpOverGap(parent, 59, 68, 0);

        // 尖刺迷宮第 2 區 (中央釘刺，需沿邊緣滑行)
        BuildSection(parent, 68, 105, new int[] { -1, 0, 1 });
        for (int r = 70; r < 102; r += 5)
        {
            CreateHazard(parent, new Vector3(0, 0.5f, r * ROW_LEN));
            float safeX = ((r / 5) % 2 == 0) ? -COL_WIDTH : COL_WIDTH;
            CreateGem(parent, new Vector3(safeX, 0, r * ROW_LEN));
        }

        // 跳台飛躍
        CreateJumpOverGap(parent, 104, 114, 0);

        // 尖刺迷宮第 3 區 (密集蛇形陣列)
        BuildSection(parent, 114, 150, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 116; r < 146; r += 6)
        {
            float side = ((r / 6) % 2 == 0) ? -1.5f : 1.5f;
            CreateHazard(parent, new Vector3(side * COL_WIDTH, 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(-side * COL_WIDTH * 0.7f, 0, r * ROW_LEN));
        }

        CreateJumpOverGap(parent, 149, 159, 0);

        // 衝刺收尾
        BuildSection(parent, 159, 173, new int[] { -1, 0, 1 });
        for (int r = 161; r <= 170; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 180);
    }

    // ---------- LEVEL 6 : 升降律動 (Piston Pulse) ----------
    void BuildLevel_6(Transform parent)
    {
        // 總排數 195 (390m)：深淵連環大跳躍與全平面平滑衝刺 (全關卡統一 0f 高度，消除著陸階梯落差小跳)
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });

        // 第 1 段深淵彈射：飛越第 25~31 排深淵
        CreateJumpOverGap(parent, 24, 32, 0);

        // 第 1 區 3 線道，懸浮寶石與左右障礙
        BuildSection(parent, 32, 60, new int[] { -1, 0, 1 });
        for (int r = 34; r < 58; r += 4)
        {
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
            if (r % 8 == 0) CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, r * ROW_LEN));
        }

        // 第 2 段深淵彈射：飛越第 60~67 排深淵
        CreateJumpOverGap(parent, 59, 68, 0);

        // 第 2 區 3 線道
        BuildSection(parent, 68, 100, new int[] { -1, 0, 1 });
        for (int r = 70; r < 98; r += 4)
        {
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
            if (r % 8 == 4) CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, r * ROW_LEN));
        }

        // 第 3 段大跳躍：飛越第 100~108 排深淵
        CreateJumpOverGap(parent, 99, 109, 0);

        // 地面平台恢復衝刺
        BuildSection(parent, 109, 145, new int[] { -1, 0, 1 });
        for (int r = 112; r < 142; r += 5)
        {
            CreateHazard(parent, new Vector3(((r % 2 == 0) ? -COL_WIDTH : COL_WIDTH), 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
        }

        // 終曲跳躍
        CreateJumpOverGap(parent, 144, 155, 0);
        BuildSection(parent, 155, 187, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 158; r <= 182; r += 4)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        FinishLevelSetup(parent, 195);
    }

    // ---------- LEVEL 7 : 浮島穿梭 (Floating Islands) ----------
    void BuildLevel_7(Transform parent)
    {
        // 總排數 210 (420m)：浮空群島接力跳躍
        BuildSection(parent, 0, 22, new int[] { -2, -1, 0, 1, 2 });

        // 島嶼 1 -> 島嶼 2 (偏左)
        CreateJumpOverGap(parent, 21, 30, 0);
        BuildSection(parent, 30, 52, new int[] { -2, -1, 0 });
        for (int r = 32; r < 50; r += 3)
            CreateGem(parent, new Vector3(-COL_WIDTH, 0, r * ROW_LEN));

        // 島嶼 2 -> 島嶼 3 (偏右)
        CreateJumpOverGap(parent, 51, 60, -1);
        BuildSection(parent, 60, 82, new int[] { 0, 1, 2 });
        for (int r = 62; r < 80; r += 3)
            CreateGem(parent, new Vector3(COL_WIDTH, 0, r * ROW_LEN));

        // 島嶼 3 -> 島嶼 4 (中央窄橋)
        CreateJumpOverGap(parent, 81, 90, 1);
        BuildSection(parent, 90, 112, new int[] { -1, 0 });
        CreateHazard(parent, new Vector3(0, 0.5f, 100 * ROW_LEN));
        CreateGem(parent, new Vector3(-COL_WIDTH, 0, 100 * ROW_LEN));

        // 島嶼 4 -> 島嶼 5 (右側窄橋)
        CreateJumpOverGap(parent, 111, 120, -1);
        BuildSection(parent, 120, 142, new int[] { 0, 1 });
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 130 * ROW_LEN));
        CreateGem(parent, new Vector3(0, 0, 130 * ROW_LEN));

        // 島嶼 5 -> 島嶼 6 (大跳躍至主大陸)
        CreateJumpOverGap(parent, 141, 152, 0);
        BuildSection(parent, 152, 176, new int[] { -1, 0, 1 });
        for (int r = 154; r < 174; r += 4)
            CreateGem(parent, new Vector3(Mathf.Sin(r) * COL_WIDTH, 0, r * ROW_LEN));

        // 最後一道彈跳
        CreateJumpOverGap(parent, 175, 185, 0);
        BuildSection(parent, 185, 202, new int[] { -2, -1, 0, 1, 2 });

        FinishLevelSetup(parent, 210);
    }

    // ---------- LEVEL 8 : 重力逆襲 (Gravity Shift) ----------
    void BuildLevel_8(Transform parent)
    {
        // 總排數 225 (450m)：雙峽谷分流、深淵對決與高難度急轉
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });

        // 第 1 級彈射
        CreateJumpOverGap(parent, 24, 33, 0);

        // 雙子深谷分流 (左峽谷 -2, -1 / 右峽谷 1, 2，中間虛空)
        BuildSection(parent, 33, 66, new int[] { -2, -1 });
        BuildSection(parent, 33, 66, new int[] { 1, 2 });
        for (int r = 36; r < 64; r += 4)
        {
            CreateGem(parent, new Vector3(-1.5f * COL_WIDTH, 0, r * ROW_LEN));
            CreateGem(parent, new Vector3(1.5f * COL_WIDTH, 0, r * ROW_LEN));
            if (r % 8 == 0)
            {
                CreateHazard(parent, new Vector3(-2 * COL_WIDTH, 0.5f, r * ROW_LEN));
                CreateHazard(parent, new Vector3(2 * COL_WIDTH, 0.5f, r * ROW_LEN));
            }
        }

        // 雙子峽谷同步越崖跳台
        CreateJumpOverGap(parent, 65, 74, -1);
        CreateJumpOverGap(parent, 65, 74, 1);

        // 匯合至蛇行 3 線道
        BuildSection(parent, 74, 110, new int[] { -1, 0, 1 });
        for (int r = 76; r < 108; r += 5)
        {
            float hx = (r % 2 == 0) ? -COL_WIDTH : COL_WIDTH;
            CreateHazard(parent, new Vector3(hx, 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(-hx, 0, r * ROW_LEN));
        }

        // 第 3 級深淵彈射
        CreateJumpOverGap(parent, 109, 119, 0);

        // 左偏窄橋
        BuildSection(parent, 119, 155, new int[] { -2, -1, 0 });
        for (int r = 122; r < 152; r += 4)
            CreateGem(parent, new Vector3(-COL_WIDTH, 0, r * ROW_LEN));

        // 第 4 級深淵彈射
        CreateJumpOverGap(parent, 154, 164, -1);

        // 右偏窄橋
        BuildSection(parent, 164, 192, new int[] { 0, 1, 2 });
        for (int r = 166; r < 190; r += 4)
            CreateGem(parent, new Vector3(COL_WIDTH, 0, r * ROW_LEN));

        // 第 5 級深淵彈射至終點衝刺區
        CreateJumpOverGap(parent, 191, 201, 1);
        BuildSection(parent, 201, 218, new int[] { -2, -1, 0, 1, 2 });

        FinishLevelSetup(parent, 225);
    }

    // ---------- LEVEL 9 : 混沌風暴 (Chaos Vortex) ----------
    void BuildLevel_9(Transform parent)
    {
        // 總排數 240 (480m)：單格獨木橋鋼索 + 連續深淵彈射 + 極速避障
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });

        // 第 1 跳
        CreateJumpOverGap(parent, 24, 33, 0);

        // 第 1 段單格鋼絲橋 (只有 center col 0)
        BuildSection(parent, 33, 58, new int[] { 0 });
        for (int r = 35; r < 56; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 2 跳
        CreateJumpOverGap(parent, 57, 66, 0);

        // 3 線道密集障礙
        BuildSection(parent, 66, 96, new int[] { -1, 0, 1 });
        for (int r = 68; r < 94; r += 4)
        {
            float side = (r % 2 == 0) ? -COL_WIDTH : COL_WIDTH;
            CreateHazard(parent, new Vector3(side, 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
        }

        // 第 3 跳
        CreateJumpOverGap(parent, 95, 105, 0);

        // 第 2 段單格鋼絲橋
        BuildSection(parent, 105, 132, new int[] { 0 });
        for (int r = 107; r < 130; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 4 跳
        CreateJumpOverGap(parent, 131, 140, 0);

        // 雙線窄橋
        BuildSection(parent, 140, 175, new int[] { -1, 0 });
        for (int r = 142; r < 172; r += 5)
        {
            CreateHazard(parent, new Vector3(0, 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(-COL_WIDTH, 0, r * ROW_LEN));
        }

        // 第 5 跳
        CreateJumpOverGap(parent, 174, 184, -1);

        // 衝刺回歸 3 線道
        BuildSection(parent, 184, 212, new int[] { -1, 0, 1 });
        for (int r = 186; r < 210; r += 4)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 第 6 跳
        CreateJumpOverGap(parent, 211, 220, 0);
        BuildSection(parent, 220, 233, new int[] { -2, -1, 0, 1, 2 });

        FinishLevelSetup(parent, 240);
    }

    // ---------- LEVEL 10 : 終極躍動 (Apex Transcendence) ----------
    void BuildLevel_10(Transform parent)
    {
        // 總排數 260 (520m) 史詩高難度終章：全機關大集結 (鋼索、多層垂直跳台、雙峽谷、全速衝刺)
        BuildSection(parent, 0, 28, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 4; r <= 24; r += 4)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 階段 1：大彈射飛越首道深淵
        CreateJumpOverGap(parent, 27, 37, 0);

        // 階段 2：衝刺 3 線道 (全關卡統一 0f 高度，消除著陸階梯落差小跳)
        BuildSection(parent, 37, 65, new int[] { -1, 0, 1 });
        for (int r = 39; r < 63; r += 4)
        {
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
            if (r % 8 == 0) CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, r * ROW_LEN));
        }

        // 階段 3：大跳躍
        CreateJumpOverGap(parent, 64, 74, 0);
        BuildSection(parent, 74, 105, new int[] { -1, 0, 1 });
        for (int r = 76; r < 103; r += 4)
        {
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));
            if (r % 8 == 4) CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, r * ROW_LEN));
        }

        // 階段 4：大跳躍
        CreateJumpOverGap(parent, 104, 114, 0);

        // 階段 5：雙子峽谷分流 (-2, -1 與 1, 2)
        BuildSection(parent, 114, 146, new int[] { -2, -1 });
        BuildSection(parent, 114, 146, new int[] { 1, 2 });
        for (int r = 116; r < 144; r += 4)
        {
            CreateGem(parent, new Vector3(-1.5f * COL_WIDTH, 0, r * ROW_LEN));
            CreateGem(parent, new Vector3(1.5f * COL_WIDTH, 0, r * ROW_LEN));
        }

        // 階段 6：雙峽谷大躍升
        CreateJumpOverGap(parent, 145, 155, -1);
        CreateJumpOverGap(parent, 145, 155, 1);

        // 階段 7：極速單格鋼絲橋
        BuildSection(parent, 155, 185, new int[] { 0 });
        for (int r = 157; r < 183; r += 3)
            CreateGem(parent, new Vector3(0, 0, r * ROW_LEN));

        // 階段 8：鋼索盡頭大飛越
        CreateJumpOverGap(parent, 184, 194, 0);

        // 階段 9：終曲 3 線道密集避障陣列
        BuildSection(parent, 194, 226, new int[] { -1, 0, 1 });
        for (int r = 196; r < 224; r += 4)
        {
            float side = ((r / 4) % 2 == 0) ? -COL_WIDTH : COL_WIDTH;
            CreateHazard(parent, new Vector3(side, 0.5f, r * ROW_LEN));
            CreateGem(parent, new Vector3(-side, 0, r * ROW_LEN));
        }

        // 階段 10：終極壯麗大跳躍直衝星門
        CreateJumpOverGap(parent, 225, 236, 0);
        BuildSection(parent, 236, 252, new int[] { -2, -1, 0, 1, 2 });
        for (int r = 238; r <= 250; r += 2)
        {
            CreateGem(parent, new Vector3(-COL_WIDTH, 0, r * ROW_LEN));
            CreateGem(parent, new Vector3(COL_WIDTH, 0, r * ROW_LEN));
        }

        FinishLevelSetup(parent, 260);
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
        BuildSection(startChunk.transform, 0, 20, new int[] { -2, -1, 0, 1, 2 });
        for (int z = 8; z < 36; z += 6)
        {
            CreateGem(startChunk.transform, new Vector3(0, 0.8f, z));
        }
        currentFarthestZ = 40.0f;
        activeChunks.Add(new EndlessChunkEntry { go = startChunk, startZ = 0f, endZ = 40.0f });

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
        int chunkType = Random.Range(0, 10);

        switch (chunkType)
        {
            case 0: // 【霓虹高速巡航大道】寬敞 5 線道衝刺，交錯單側尖刺與高亮金鑽
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                for (int r = startRow + 2; r < startRow + rowsInChunk - 2; r += 4)
                {
                    int spikeCol = (Random.value > 0.5f) ? -1 : 1;
                    if (Random.value > 0.6f) spikeCol = (spikeCol == -1) ? -2 : 2;
                    CreateHazard(chunk.transform, new Vector3(spikeCol * COL_WIDTH, 0.5f, r * ROW_LEN));
                    CreateGem(chunk.transform, new Vector3(0, 0.95f, (r + 2) * ROW_LEN));
                }
                break;

            case 1: // 【虛空躍崖大斷層】彈跳墊跨越 10 米深淵！空中懸浮璀璨彩虹金鑽弧線，遠方寬敞 5 線道著陸台
                BuildSection(chunk.transform, startRow, startRow + 5, new int[] { -1, 0, 1 });
                // 於斷層邊緣第 4 排設置彈跳墊
                int pRow1 = startRow + 4;
                CreateJumpPad(chunk.transform, new Vector3(-COL_WIDTH, 0.1f, pRow1 * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(0, 0.1f, pRow1 * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(COL_WIDTH, 0.1f, pRow1 * ROW_LEN));

                // 斷層空中懸浮引導金鑽 (5 顆拋物線金鑽)
                float[] arcH1 = new float[] { 2.4f, 3.8f, 4.8f, 3.8f, 2.4f };
                for (int h = 0; h < arcH1.Length; h++)
                {
                    CreateGem(chunk.transform, new Vector3(0, arcH1[h], (pRow1 + 1 + h) * ROW_LEN));
                }
                // 斷層深淵 (pRow1+1 至 pRow1+5 完全懸空)，著陸平台自第 10 排開始！
                BuildSection(chunk.transform, startRow + 10, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 2: // 【雙重手動跳躍斷層】考驗空白鍵精準起跳！兩處 4 米虛空斷崖，金鑽懸於裂口正上方導引
                BuildSection(chunk.transform, startRow, startRow + 5, new int[] { -1, 0, 1 });
                CreateGem(chunk.transform, new Vector3(0, 1.4f, (startRow + 5.5f) * ROW_LEN)); // 1 號斷層導引金鑽
                // 裂口：第 5, 6 排為 4 米深淵斷層
                BuildSection(chunk.transform, startRow + 7, startRow + 12, new int[] { -1, 0, 1 });
                CreateGem(chunk.transform, new Vector3(0, 1.4f, (startRow + 12.5f) * ROW_LEN)); // 2 號斷層導引金鑽
                // 裂口：第 12, 13 排為 4 米深淵斷層
                BuildSection(chunk.transform, startRow + 14, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 3: // 【虛空浮石連續斷層群】交替孤島飛躍！左右穿梭跳台，考驗搖桿/左右鍵靈敏變道
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -2, -1 });
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH * 1.5f, 0.95f, (startRow + 1) * ROW_LEN));
                // 第 3 排為斷層
                BuildSection(chunk.transform, startRow + 4, startRow + 7, new int[] { 1, 2 });
                CreateGem(chunk.transform, new Vector3(COL_WIDTH * 1.5f, 0.95f, (startRow + 5) * ROW_LEN));
                // 第 7 排為斷層
                BuildSection(chunk.transform, startRow + 8, startRow + 11, new int[] { -1, 0, 1 });
                CreateGem(chunk.transform, new Vector3(0, 0.95f, (startRow + 9) * ROW_LEN));
                // 第 11 排為斷層
                BuildSection(chunk.transform, startRow + 12, startRow + 15, new int[] { -2, -1 });
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH * 1.5f, 0.95f, (startRow + 13) * ROW_LEN));
                // 第 15 排為斷層
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 4: // 【虛空細樑獨木橋】兩側皆為無底深淵！僅中央單線道橫跨 22 米高空，極致心跳挑戰
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -2, -1, 0, 1, 2 });
                // 兩側截斷，僅保留 0 號線道高空窄樑
                BuildSection(chunk.transform, startRow + 3, startRow + 16, new int[] { 0 });
                for (int r = startRow + 4; r < startRow + 16; r += 3)
                {
                    CreateGem(chunk.transform, new Vector3(0, 0.95f, r * ROW_LEN));
                }
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 5: // 【空中大跳躍長廊】深淵飛越與全速衝刺 (統一 0f 高度，消除階梯下墜落差)
                BuildSection(chunk.transform, startRow, startRow + 5, new int[] { -1, 0, 1 });
                CreateJumpOverGap(chunk.transform, startRow + 4, startRow + 8, 0);
                BuildSection(chunk.transform, startRow + 8, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateGem(chunk.transform, new Vector3(0, 0, (startRow + 12) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(0, 0, (startRow + 16) * ROW_LEN));
                break;

            case 6: // 【雙分叉中央大深淵】正中間 6 米全鏤空深淵！左右兩側獨立飛馳
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -2, -1, 0, 1, 2 });
                // 中央 3 線道鏤空，僅留左右外側
                BuildSection(chunk.transform, startRow + 3, startRow + 16, new int[] { -2, 2 });
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH * 2, 0.95f, (startRow + 6) * ROW_LEN));
                CreateHazard(chunk.transform, new Vector3(-COL_WIDTH * 2, 0.5f, (startRow + 10) * ROW_LEN));
                CreateHazard(chunk.transform, new Vector3(COL_WIDTH * 2, 0.5f, (startRow + 6) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(COL_WIDTH * 2, 0.95f, (startRow + 10) * ROW_LEN));
                BuildSection(chunk.transform, startRow + 16, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateGem(chunk.transform, new Vector3(0, 0.95f, (startRow + 18) * ROW_LEN));
                break;

            case 7: // 【連環雙重彈射深淵】兩段式連續空中大跳躍！爽快感翻倍
                BuildSection(chunk.transform, startRow, startRow + 3, new int[] { -1, 0, 1 });
                // 第 1 級跳台：由 startRow+2 跳至 startRow+6
                CreateJumpOverGap(chunk.transform, startRow + 2, startRow + 6, 0);

                // 第 3~5 排為斷層，落在第 6~8 排空中島
                BuildSection(chunk.transform, startRow + 6, startRow + 9, new int[] { -1, 0, 1 });
                // 第 2 級跳台：由 startRow+8 跳至 startRow+12
                CreateJumpOverGap(chunk.transform, startRow + 8, startRow + 12, 0);

                // 第 9~11 排為斷層，落在第 12 排大平台
                BuildSection(chunk.transform, startRow + 12, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;

            case 8: // 【流暢 S 彎變道】3 線道蛇行穿梭，順滑過彎
                for (int i = 0; i < rowsInChunk; i++)
                {
                    int r = startRow + i;
                    int centerCol = (i < 5) ? -1 : (i < 10) ? 0 : (i < 15) ? 1 : 0;
                    int[] activeCols = new int[] { centerCol - 1, centerCol, centerCol + 1 };
                    foreach (int c in activeCols) CreateSingleTile(chunk.transform, c, r);
                    if (i % 3 == 0) CreateGem(chunk.transform, new Vector3(centerCol * COL_WIDTH, 0.95f, r * ROW_LEN));
                }
                break;

            case 9: // 【金鑽狂歡長廊】無障礙大獎勵！高密度金光閃閃鑽石陣列
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH, 0.95f, (startRow + 3) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(COL_WIDTH, 0.95f, (startRow + 3) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(0, 0.95f, (startRow + 6) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH * 1.5f, 0.95f, (startRow + 10) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(COL_WIDTH * 1.5f, 0.95f, (startRow + 10) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(0, 0.95f, (startRow + 14) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH, 0.95f, (startRow + 17) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(COL_WIDTH, 0.95f, (startRow + 17) * ROW_LEN));
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
            // 向上延伸為 3.6m 高立體垂直感應柱 (0.12 * 30 = 3.6m)，小球無論微浮空、顛簸或空中小跳均能 100% 觸發
            bc.size = new Vector3(1.0f, 30.0f, 1.0f);
            bc.center = new Vector3(0, 14.5f, 0);
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
        gem.AddComponent<GemCollectible>();
    }

    void CreateAirborneGem(Transform parent, Vector3 pos)
    {
        // 高空/跳台拋物線專用懸浮寶石 (直接使用傳入之精確空中 Y 座標)
        GameObject gem = new GameObject("Gem_Airborne");
        gem.transform.SetParent(parent);
        gem.transform.position = pos;
        gem.transform.localScale = Vector3.one;
        gem.AddComponent<GemCollectible>();
    }

    void CreateJumpOverGap(Transform parent, int jumpRow, int landingRow, int col, float height = 0f)
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
