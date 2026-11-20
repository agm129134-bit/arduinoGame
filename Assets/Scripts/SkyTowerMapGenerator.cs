using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通天天梯 (Sky Tower) 專屬全自動高空攀爬地圖生成器
/// 涵蓋 0m 至 500m 六大高度分區、登頂聖域神碑、動態浮島與體感推進跳躍機制
/// </summary>
public class SkyTowerMapGenerator : MonoBehaviour
{
    public static SkyTowerMapGenerator Instance { get; private set; }

    [Header("Sky Tower Material Palettes")]
    public Material matZone1_Dawn;      // 0-50m 晨曦金橙
    public Material matZone2_Neon;      // 50-120m 霓虹電藍
    public Material matZone3_Spiral;    // 120-220m 流光魔紫
    public Material matZone4_Hazard;    // 220-320m 熔岩赤紅
    public Material matZone5_Cosmic;    // 320-430m 深邃星空
    public Material matZone6_Apex;      // 430-500m 凌霄純金

    public Material matGround;          // 地面基座
    public Material matJumpPad;         // 推進彈跳墊
    public Material matGem;             // 浮空水晶
    public Material matSpike;           // 尖刺障礙
    public Material matMonolith;        // 通天神碑

    private PhysicsMaterial platformPhysMat;
    private GameObject towerRoot;

    // 主路徑銜接：記錄上一塊主路徑平臺，跨區時自動補上墊腳石，保證每一步都跳得到
    // 物理基準 (最低鍵盤速度 5 m/s、jumpForce 8.5、g 9.81)：最大躍升約 3.6m；墊腳石限制在 ≤2.4m 躍升 / ≤2.5m 邊緣間距
    private Vector3 lastPathPos;
    private Vector3 lastPathSize;
    private bool hasLastPath = false;
    private const float BridgeStoneSize = 4.6f;
    private const float BridgeStoneThickness = 0.45f;
    private const float BridgeMaxGap = 2.0f;
    private const float BridgeMaxRise = 2.0f;

    void Awake()
    {
        Instance = this;
        InitMaterials();
    }

    void InitMaterials()
    {
        platformPhysMat = new PhysicsMaterial("SkyTowerZeroBounce")
        {
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum,
            dynamicFriction = 0.08f,
            staticFriction = 0.08f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };

        matGround = CreateMat("Mat_Tower_Ground", new Color(0.08f, 0.12f, 0.20f), new Color(0.05f, 0.15f, 0.3f));
        matZone1_Dawn = CreateMat("Mat_Zone1_Dawn", new Color(0.12f, 0.22f, 0.35f), new Color(0.2f, 0.7f, 0.9f));
        matZone2_Neon = CreateMat("Mat_Zone2_Neon", new Color(0.05f, 0.18f, 0.28f), new Color(0f, 0.9f, 1.2f));
        matZone3_Spiral = CreateMat("Mat_Zone3_Spiral", new Color(0.18f, 0.08f, 0.28f), new Color(0.85f, 0.15f, 1.3f));
        matZone4_Hazard = CreateMat("Mat_Zone4_Hazard", new Color(0.28f, 0.10f, 0.05f), new Color(1.2f, 0.35f, 0.05f));
        matZone5_Cosmic = CreateMat("Mat_Zone5_Cosmic", new Color(0.10f, 0.08f, 0.24f), new Color(0.5f, 0.2f, 1.1f));
        matZone6_Apex = CreateMat("Mat_Zone6_Apex", new Color(0.35f, 0.28f, 0.08f), new Color(1.8f, 1.4f, 0.3f));

        matJumpPad = CreateMat("Mat_Tower_JumpPad", new Color(1.0f, 0.52f, 0.05f), new Color(4.8f, 2.5f, 0.2f));
        matSpike = CreateMat("Mat_Tower_Spike", new Color(0.8f, 0.1f, 0.1f), new Color(2.0f, 0.2f, 0.1f));
        matMonolith = CreateMat("Mat_Tower_Monolith", new Color(1.0f, 0.9f, 0.4f), new Color(2.5f, 2.0f, 0.6f));

        matGem = Resources.Load<Material>("Materials/Mat_Gem");
        if (matGem == null) matGem = CreateMat("Mat_Tower_Gem", new Color(1.0f, 0.85f, 0.1f), new Color(2.8f, 2.2f, 0.4f));
    }

    Material CreateMat(string name, Color albedo, Color emission)
    {
        Material mat = new Material(Shader.Find("Standard"))
        {
            name = name,
            color = albedo
        };
        mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        mat.SetFloat("_Metallic", 0.4f);
        mat.SetFloat("_Glossiness", 0.9f);
        return mat;
    }

    /// <summary>
    /// 全面建造 0m 至 500m 通天天梯高塔場景
    /// </summary>
    public void BuildTower()
    {
        if (towerRoot != null) Destroy(towerRoot);
        towerRoot = new GameObject("--- Sky Tower Hierarchy (0m-500m) ---");
        hasLastPath = false;

        Debug.Log("[SkyTower] 開始生成通天天梯 500 米極限高塔地圖...");

        // 0. 起點基座與底層安全接球網
        BuildGroundHub(towerRoot.transform);

        // 1. Zone 1 (0m - 50m) 【晨曦起點 / 雲階基石 (Dawn Ascent)】
        BuildZone1_DawnAscent(towerRoot.transform);

        // 2. Zone 2 (50m - 120m) 【霓虹管陣 / 懸浮跳台 (Neon Platforms)】
        BuildZone2_NeonPipes(towerRoot.transform);

        // 3. Zone 3 (120m - 220m) 【旋轉階梯 / 律動浮島 (Spiral Steps)】
        BuildZone3_SpiralTower(towerRoot.transform);

        // 4. Zone 4 (220m - 320m) 【窄崖攀爬 / 尖刺夾縫 (Narrow Ridges)】
        BuildZone4_NarrowRidges(towerRoot.transform);

        // 5. Zone 5 (320m - 430m) 【雲端迷蹤 / 斷層風暴 (Cloud Maze)】
        BuildZone5_CloudMaze(towerRoot.transform);

        // 6. Zone 6 (430m - 500m) 【凌霄天頂 / 終焉方碑 (Apex Monolith & Shrine)】
        BuildZone6_ApexShrine(towerRoot.transform);

        Debug.Log("[SkyTower] 500 米高塔地圖建構完成！");
        RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
        if (gm != null)
        {
            gm.RefreshTotalGems();
        }
    }

    // ==================== 0. 起點基座與底層接地地板 ====================
    void BuildGroundHub(Transform parent)
    {
        GameObject hub = new GameObject("Ground_Hub");
        hub.transform.SetParent(parent);

        // 起點主平臺 (16x16m, Y = 0)
        CreatePathPlatform(new Vector3(0, 0, 0), new Vector3(16f, 2.0f, 16f), matGround, hub.transform, "StartPlatform");

        // 全域前向延伸墜落保護大地板 (涵蓋全塔 X:-120~+140m, Z:-80~+380m, Y = -0.8m)
        // 徹底解決小球前行攀爬至 Zone 2~Zone 6 時下方無地板、直接墜出地圖外的問題！
        Vector3 groundCenter = new Vector3(10f, -0.8f, 150f);
        Vector3 groundSize = new Vector3(260f, 1.0f, 460f);
        CreatePlatform(groundCenter, groundSize, matGround, hub.transform, "GroundCatcherFloor");

        // 四方高聳防墜邊界光柱與邊界防護標記 (Corner & Perimeter Beacon Pillars)
        float halfW = groundSize.x * 0.5f - 4f; // 126m
        float halfL = groundSize.z * 0.5f - 4f; // 226m
        Vector3[] beaconPositions = new Vector3[]
        {
            groundCenter + new Vector3(-halfW, 10f, -halfL),
            groundCenter + new Vector3(halfW, 10f, -halfL),
            groundCenter + new Vector3(-halfW, 10f, halfL),
            groundCenter + new Vector3(halfW, 10f, halfL),
            groundCenter + new Vector3(-halfW, 10f, 0f),
            groundCenter + new Vector3(halfW, 10f, 0f),
            groundCenter + new Vector3(0f, 10f, halfL),
            groundCenter + new Vector3(0f, 10f, -halfL)
        };
        for (int b = 0; b < beaconPositions.Length; b++)
        {
            CreatePillar(beaconPositions[b], 1.2f, 20f, matZone1_Dawn, hub.transform, $"GroundBeacon_{b}");
        }

        // 起點裝飾光柱
        CreatePillar(new Vector3(-7f, 3.5f, -7f), 0.5f, 7f, matZone1_Dawn, hub.transform);
        CreatePillar(new Vector3(7f, 3.5f, -7f), 0.5f, 7f, matZone1_Dawn, hub.transform);
        CreatePillar(new Vector3(-7f, 3.5f, 7f), 0.5f, 7f, matZone1_Dawn, hub.transform);
        CreatePillar(new Vector3(7f, 3.5f, 7f), 0.5f, 7f, matZone1_Dawn, hub.transform);
    }

    // ==================== 難度調整 (easier pass) ====================
    // 主路徑統一：躍升 ≤ 2.1m (原 2.65m)、平臺邊距 ≤ 1.5m (原最大 4.2m)、平臺邊長 ≥ 5.0m；
    // 墊腳石 (BridgeMax*) 同步收緊為 邊距 ≤ 2.0m / 躍升 ≤ 2.0m；移動平臺只剩 1 座且極慢；
    // 尖刺大幅減少；環形路段外緣加低矮護欄 (僅在外緣與行進方向垂直時才放，絕不擋路)。

    // 在平臺外緣加低矮護欄：外向方向貼近 X 或 Z 軸 (±25°) 時才放，確保不會擋住前後平臺的進出路線
    void AddOuterRail(Vector3 platPos, Vector3 platSize, Vector3 ringCenter, Material mat, Transform parent)
    {
        Vector3 outward = new Vector3(platPos.x - ringCenter.x, 0f, platPos.z - ringCenter.z);
        float len = outward.magnitude;
        if (len < 0.01f) return;
        outward /= len;

        bool alongX = Mathf.Abs(outward.x) >= Mathf.Abs(outward.z);
        float dominant = Mathf.Max(Mathf.Abs(outward.x), Mathf.Abs(outward.z));
        if (dominant < 0.9f) return;

        const float railH = 0.9f;
        const float railT = 0.25f;
        Vector3 railPos;
        Vector3 railSize;
        if (alongX)
        {
            float sx = Mathf.Sign(outward.x);
            railPos = new Vector3(platPos.x + sx * (platSize.x * 0.5f - railT * 0.5f), platPos.y + platSize.y * 0.5f + railH * 0.5f, platPos.z);
            railSize = new Vector3(railT, railH, platSize.z);
        }
        else
        {
            float sz = Mathf.Sign(outward.z);
            railPos = new Vector3(platPos.x, platPos.y + platSize.y * 0.5f + railH * 0.5f, platPos.z + sz * (platSize.z * 0.5f - railT * 0.5f));
            railSize = new Vector3(platSize.x, railH, railT);
        }
        CreatePlatform(railPos, railSize, mat, parent, "GuardRail");
    }

    // ==================== 1. Zone 1: 0m - 50m 晨曦起點 ====================
    void BuildZone1_DawnAscent(Transform parent)
    {
        GameObject zone = new GameObject("Zone1_DawnAscent (0-50m)");
        zone.transform.SetParent(parent);

        int steps = 25;
        for (int i = 0; i < steps; i++)
        {
            float y = 2.0f + i * 1.95f; // 升至約 49m (躍升 1.95m)
            float x = Mathf.Sin(i * 0.35f) * 5.0f; // 左右擺幅 6.5 → 5.0m，換邊更緩
            float z = 8.0f + i * 4.6f;

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = (i % 6 == 0 || i == steps - 1) ? new Vector3(7.0f, 0.45f, 7.0f) : new Vector3(5.6f, 0.45f, 5.6f);

            CreatePathPlatform(pos, size, matZone1_Dawn, zone.transform, $"Z1_Step_{i}");

            if (i % 3 == 0) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 2. Zone 2: 50m - 120m 霓虹管陣 ====================
    void BuildZone2_NeonPipes(Transform parent)
    {
        GameObject zone = new GameObject("Zone2_NeonPipes (50-120m)");
        zone.transform.SetParent(parent);

        int steps = 36;                       // 28 → 36 階，躍升 2.45 → 1.9m
        float startZ = 8.0f + 25 * 4.6f;      // 銜接 Zone 1 終點 (~123m)，Zone 1 的 z 步進未變

        for (int i = 0; i < steps; i++)
        {
            float y = 51.5f + i * 1.9f;       // 升至約 118m
            float x = 12.0f + Mathf.Cos(i * 0.25f) * 6.0f; // 頻率隨階數縮放、擺幅 7.5 → 6.0m
            float z = startZ + i * 3.73f;     // 總前進距離與原本相同

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = (i % 6 == 0) ? new Vector3(7.0f, 0.45f, 7.0f) : new Vector3(5.6f, 0.45f, 5.6f);

            // i == 0：與 Zone 1 終點相距遠，自動補墊腳石銜接
            CreatePathPlatform(pos, size, matZone2_Neon, zone.transform, $"Z2_Pipe_{i}", i == 0);

            // 原本 2 座垂直彈跳墊會把球彈離路徑，易化版移除

            if (i % 4 == 1) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 3. Zone 3: 120m - 220m 旋轉階梯 ====================
    void BuildZone3_SpiralTower(Transform parent)
    {
        GameObject zone = new GameObject("Zone3_SpiralTower (120-220m)");
        zone.transform.SetParent(parent);

        // 中央高聳能量核心巨柱 (X=12, Z=260)
        Vector3 coreCenter = new Vector3(12f, 170f, 260f);
        CreatePillar(coreCenter, 3.5f, 110f, matZone3_Spiral, zone.transform, "CentralEnergyPillar");

        int steps = 47;                  // 38 → 47 階，躍升 2.65 → 2.1m
        float radius = 16.0f;
        const float startAngleDeg = 235.0f;
        const float stepAngleDeg = 16.0f; // 20° → 16°：相鄰平臺緊貼無缺口 (終點角度與原設計相近)
        Vector3 ringCenter = new Vector3(coreCenter.x, 0f, coreCenter.z);

        for (int i = 0; i < steps; i++)
        {
            float y = 120.0f + i * 2.1f;  // 升至約 217m
            float angle = (startAngleDeg + i * stepAngleDeg) * Mathf.Deg2Rad;
            float x = coreCenter.x + Mathf.Cos(angle) * radius;
            float z = coreCenter.z + Mathf.Sin(angle) * radius;

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = new Vector3(5.2f, 0.45f, 5.2f);

            GameObject plat = CreatePathPlatform(pos, size, matZone3_Spiral, zone.transform, $"Z3_SpiralStep_{i}", i == 0);
            AddOuterRail(pos, size, ringCenter, matZone3_Spiral, zone.transform);

            // 只保留 1 座極慢小幅漂移浮島 (最大速度約 0.8 m/s、幅度 1.2m)，原為 2 座
            if (i == 24)
            {
                SkyTowerMovingPlatform mover = plat.AddComponent<SkyTowerMovingPlatform>();
                mover.moveDirection = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                mover.moveDistance = 1.2f;
                mover.moveSpeed = 0.7f;
            }

            if (i % 4 == 0) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 4. Zone 4: 220m - 320m 寬崖攀爬 ====================
    void BuildZone4_NarrowRidges(Transform parent)
    {
        GameObject zone = new GameObject("Zone4_NarrowRidges (220-320m)");
        zone.transform.SetParent(parent);

        // 外框 36 → 28m：每階水平間距 9 → 7m、平臺 5.6m，邊距僅 1.4m (原 4.2m)；躍升 2.45 → 1.75m，階數 41 → 57
        int steps = 57;
        float squareSize = 28.0f;
        Vector3 zoneCenter = new Vector3(12f, 0f, 260f);
        // 相位 3：起點靠近 Zone 3 終點；第 56 階落在北側 (5, 274) 正對 Zone 5 起點 (連線不穿過中央巨柱)
        const int phaseOffset = 3;

        for (int i = 0; i < steps; i++)
        {
            float y = 221.5f + i * 1.75f; // 升至約 319.5m

            float t = ((i + phaseOffset) % 16) / 16.0f;
            float x = 0f, z = 0f;

            if (t < 0.25f)
            {
                x = Mathf.Lerp(-squareSize / 2, squareSize / 2, t / 0.25f);
                z = -squareSize / 2;
            }
            else if (t < 0.5f)
            {
                x = squareSize / 2;
                z = Mathf.Lerp(-squareSize / 2, squareSize / 2, (t - 0.25f) / 0.25f);
            }
            else if (t < 0.75f)
            {
                x = Mathf.Lerp(squareSize / 2, -squareSize / 2, (t - 0.5f) / 0.25f);
                z = squareSize / 2;
            }
            else
            {
                x = -squareSize / 2;
                z = Mathf.Lerp(squareSize / 2, -squareSize / 2, (t - 0.75f) / 0.25f);
            }

            Vector3 pos = zoneCenter + new Vector3(x, y, z);
            Vector3 size = (i % 12 == 6) ? new Vector3(7.0f, 0.5f, 7.0f) : new Vector3(5.6f, 0.45f, 5.6f);

            CreatePathPlatform(pos, size, matZone4_Hazard, zone.transform, $"Z4_Ridge_{i}", i == 0);
            AddOuterRail(pos, size, zoneCenter, matZone4_Hazard, zone.transform);

            // 尖刺：原每 6 階 1 個 → 每 12 階 1 個，縮小並貼近外緣 (內側仍有 ≥ 3.5m 通道)
            if (i % 12 == 3)
            {
                Vector3 outward = new Vector3(x, 0f, z).normalized;
                CreateHazard(pos + outward * 1.7f + new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.4f, 1.0f), zone.transform);
            }

            if (i % 4 == 2) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 5. Zone 5: 320m - 430m 雲端迷蹤 ====================
    void BuildZone5_CloudMaze(Transform parent)
    {
        GameObject zone = new GameObject("Zone5_CloudMaze (320-430m)");
        zone.transform.SetParent(parent);

        // 40 → 52 階，躍升 2.65 → 2.0m；橢圓放大 (X 5→6.5、Z 9→11) 且頻率隨階數縮放，使相鄰平臺水平間距維持約 3m、不垂直疊壓
        int steps = 52;
        Vector3 baseCenter = new Vector3(12f, 0f, 260f);
        Vector3 ringCenter = new Vector3(baseCenter.x - 8.5f, 0f, baseCenter.z);

        for (int i = 0; i < steps; i++)
        {
            float y = 322.0f + i * 2.0f; // 升至約 424m

            float xLeft = baseCenter.x - 8.5f + Mathf.Sin(i * 0.346f) * 6.5f;
            float zLeft = baseCenter.z + Mathf.Cos(i * 0.346f) * 11.0f;

            Vector3 posL = new Vector3(xLeft, y, zLeft);
            Vector3 sizeL = new Vector3(5.4f, 0.45f, 5.4f);
            CreatePathPlatform(posL, sizeL, matZone5_Cosmic, zone.transform, $"Z5_MainPath_{i}", i == 0);
            AddOuterRail(posL, sizeL, ringCenter, matZone5_Cosmic, zone.transform);

            // 右側選用捷徑 (原每 4 階 → 每 8 階)，非主路徑，僅供加速
            if (i % 8 == 0)
            {
                float xRight = baseCenter.x + 9.5f + Mathf.Cos(i * 0.45f) * 4.5f;
                float zRight = baseCenter.z + Mathf.Sin(i * 0.35f) * 8.0f;
                Vector3 posR = new Vector3(xRight, y, zRight);
                CreatePlatform(posR, new Vector3(5.2f, 0.45f, 5.2f), matZone5_Cosmic, zone.transform, $"Z5_ShortPath_{i}");
                CreateJumpPad(posR + Vector3.up * 0.3f, 13.5f, zone.transform);
            }

            if (i % 4 == 0) CreateGem(posL + Vector3.up * 1.4f, zone.transform);
        }

        // 雲端中繼大閣樓 (Y = 428m)
        CreatePathPlatform(new Vector3(12f, 428.5f, 260f), new Vector3(10.0f, 1.0f, 10.0f), matZone5_Cosmic, zone.transform, "CloudPavilion", true);
    }

    // ==================== 6. Zone 6: 430m - 500m 凌霄天頂與終焉方碑 ====================
    void BuildZone6_ApexShrine(Transform parent)
    {
        GameObject zone = new GameObject("Zone6_ApexShrine (430-500m)");
        zone.transform.SetParent(parent);

        // 半徑 16m 環繞神殿外圍；27 → 35 階、躍升 2.65 → 2.02m、每階 16.9°，最後一階 (y≈499.7) 緊鄰神殿北緣外 (約 0.5m)
        int steps = 35;
        Vector3 apexTowerCenter = new Vector3(12f, 0f, 260f);

        for (int i = 0; i < steps; i++)
        {
            float y = 431.0f + i * 2.02f;
            float radius = 16f;
            float angle = i * 16.9f * Mathf.Deg2Rad;

            float x = apexTowerCenter.x + Mathf.Sin(angle) * radius;
            float z = apexTowerCenter.z - Mathf.Cos(angle) * radius;

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = new Vector3(5.6f, 0.5f, 5.6f);
            CreatePathPlatform(pos, size, matZone6_Apex, zone.transform, $"Z6_GoldenStair_{i}", i == 0);
            AddOuterRail(pos, size, apexTowerCenter, matZone6_Apex, zone.transform);

            if (i % 3 == 0) CreateGem(pos + Vector3.up * 1.5f, zone.transform);
        }

        // ==================== 500m 凌霄神殿大平臺 ====================
        Vector3 apexCenter = new Vector3(12f, 499.5f, 260f);
        CreatePathPlatform(apexCenter, new Vector3(20f, 2.0f, 20f), matZone6_Apex, zone.transform, "ApexSanctuaryPlatform", true);

        // 四方天界神柱
        CreatePillar(apexCenter + new Vector3(-8f, 4.5f, -8f), 0.7f, 9f, matZone6_Apex, zone.transform);
        CreatePillar(apexCenter + new Vector3(8f, 4.5f, -8f), 0.7f, 9f, matZone6_Apex, zone.transform);
        CreatePillar(apexCenter + new Vector3(-8f, 4.5f, 8f), 0.7f, 9f, matZone6_Apex, zone.transform);
        CreatePillar(apexCenter + new Vector3(8f, 4.5f, 8f), 0.7f, 9f, matZone6_Apex, zone.transform);

        // 中央通天神碑 (Sky Monolith) - 12米高聳方碑
        Vector3 monoPos = apexCenter + new Vector3(0, 6.5f, 6.0f);
        GameObject monolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
        monolith.name = "SkyMonolith";
        monolith.transform.SetParent(zone.transform);
        monolith.transform.position = monoPos;
        monolith.transform.localScale = new Vector3(2.5f, 12.0f, 2.5f);
        monolith.GetComponent<MeshRenderer>().material = matMonolith;

        // 通天登頂終點光門
        CreateApexFinishGate(apexCenter + new Vector3(0, 1.25f, 0), zone.transform);
    }

    void CreateApexFinishGate(Vector3 pos, Transform parent)
    {
        GameObject gate = new GameObject("ApexVictoryGate");
        gate.transform.SetParent(parent);
        gate.transform.position = pos;

        // 勝利光幕 Trigger
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "ApexFinishTrigger";
        trigger.transform.SetParent(gate.transform);
        trigger.transform.localPosition = new Vector3(0, 2.5f, 0);
        trigger.transform.localScale = new Vector3(10f, 5.0f, 1.0f);

        Material trigMat = new Material(Shader.Find("Standard"));
        trigMat.color = new Color(1.0f, 0.9f, 0.2f, 0.45f);
        trigMat.SetColor("_EmissionColor", new Color(3.5f, 2.8f, 0.8f));
        trigMat.EnableKeyword("_EMISSION");
        trigger.GetComponent<MeshRenderer>().material = trigMat;

        BoxCollider bc = trigger.GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;
        trigger.AddComponent<FinishTrigger>();
    }

    // ==================== 主路徑平臺與跨區墊腳石 ====================
    /// <summary>
    /// 建立主攀登路徑平臺；bridgeFromPrevious 為 true 時，先在上一塊主路徑平臺與本平臺之間
    /// 自動補上墊腳石 (每段邊緣間距 ≤ BridgeMaxGap、躍升 ≤ BridgeMaxRise)，用於各分區銜接處
    /// </summary>
    GameObject CreatePathPlatform(Vector3 pos, Vector3 size, Material mat, Transform parent, string name, bool bridgeFromPrevious = false)
    {
        if (bridgeFromPrevious && hasLastPath)
        {
            BuildBridge(lastPathPos, lastPathSize, pos, size, mat, parent, $"Bridge_To_{name}");
        }
        GameObject obj = CreatePlatform(pos, size, mat, parent, name);
        lastPathPos = pos;
        lastPathSize = size;
        hasLastPath = true;
        return obj;
    }

    void BuildBridge(Vector3 fromPos, Vector3 fromSize, Vector3 toPos, Vector3 toSize, Material mat, Transform parent, string name)
    {
        Vector3 d = toPos - fromPos;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.01f) return;
        Vector3 u = d / dist;

        // 沿連線方向計算兩端平臺邊緣與墊腳石自身的半長
        float edgeFrom = HalfExtentAlong(fromSize, u);
        float edgeTo = HalfExtentAlong(toSize, u);
        float edgeStone = HalfExtentAlong(new Vector3(BridgeStoneSize, 0f, BridgeStoneSize), u);
        float edgeLen = dist - edgeFrom - edgeTo;

        float fromTop = fromPos.y + fromSize.y * 0.5f;
        float toTop = toPos.y + toSize.y * 0.5f;
        float rise = toTop - fromTop;

        int byGap = Mathf.CeilToInt((edgeLen - BridgeMaxGap) / (2f * edgeStone + BridgeMaxGap));
        int byRise = Mathf.CeilToInt(Mathf.Abs(rise) / BridgeMaxRise) - 1;
        int n = Mathf.Max(byGap, byRise, 0);
        if (n == 0) return;

        float gap = (edgeLen - n * 2f * edgeStone) / (n + 1);
        for (int k = 1; k <= n; k++)
        {
            float along = edgeFrom + k * gap + (2 * k - 1) * edgeStone;
            Vector3 p = fromPos + u * along;
            p.y = fromTop + rise * k / (n + 1) - BridgeStoneThickness * 0.5f;
            CreatePlatform(p, new Vector3(BridgeStoneSize, BridgeStoneThickness, BridgeStoneSize), mat, parent, $"{name}_{k}");
        }
    }

    static float HalfExtentAlong(Vector3 size, Vector3 u)
    {
        return Mathf.Min(size.x * 0.5f / Mathf.Max(Mathf.Abs(u.x), 1e-4f), size.z * 0.5f / Mathf.Max(Mathf.Abs(u.z), 1e-4f));
    }

    // ==================== 基礎幾何生成工具 ====================
    public GameObject CreatePlatform(Vector3 pos, Vector3 size, Material mat, Transform parent, string name = "Platform")
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.position = pos;
        obj.transform.localScale = size;
        if (mat != null) obj.GetComponent<MeshRenderer>().material = mat;

        BoxCollider bc = obj.GetComponent<BoxCollider>();
        if (bc != null && platformPhysMat != null) bc.sharedMaterial = platformPhysMat;
        return obj;
    }

    void CreatePillar(Vector3 pos, float radius, float height, Material mat, Transform parent, string name = "Pillar")
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
        if (mat != null) obj.GetComponent<MeshRenderer>().material = mat;
    }

    void CreateJumpPad(Vector3 pos, float launchForce, Transform parent)
    {
        // 1. 深色金屬黑曜石防滑外框基座 (Rim/Base)，形成鮮明輪廓反差，絕不與青藍平臺融為一體
        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rim.name = "SkyTower_JumpPad_Rim";
        rim.transform.SetParent(parent);
        rim.transform.position = pos + Vector3.up * 0.02f;
        rim.transform.localScale = new Vector3(3.0f, 0.22f, 3.0f);
        rim.GetComponent<MeshRenderer>().material = matGround;
        Destroy(rim.GetComponent<Collider>());

        // 2. 主體烈焰金橙高亮發光彈跳墊 (加大至 2.6x2.6m)
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "SkyTower_JumpPad";
        pad.transform.SetParent(parent);
        pad.transform.position = pos + Vector3.up * 0.08f;
        pad.transform.localScale = new Vector3(2.6f, 0.26f, 2.6f);
        pad.GetComponent<MeshRenderer>().material = matJumpPad;

        // 3. 中央向上推進耀眼白金箭頭核心 (Core Beacon)
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        core.name = "JumpPad_BeaconCore";
        core.transform.SetParent(pad.transform);
        core.transform.localPosition = new Vector3(0, 0.55f, 0);
        core.transform.localRotation = Quaternion.Euler(0, 45, 0);
        core.transform.localScale = new Vector3(0.42f, 0.20f, 0.42f);
        Material coreMat = new Material(Shader.Find("Standard"))
        {
            color = new Color(1.0f, 0.95f, 0.6f)
        };
        coreMat.SetColor("_EmissionColor", new Color(5.5f, 4.8f, 1.8f));
        coreMat.EnableKeyword("_EMISSION");
        core.GetComponent<MeshRenderer>().material = coreMat;
        Destroy(core.GetComponent<Collider>());

        JumpPad jp = pad.AddComponent<JumpPad>();
        jp.launchForce = launchForce;
        jp.forwardBoost = 0f; // 垂直彈跳向上
    }

    void CreateHazard(Vector3 pos, Vector3 size, Transform parent)
    {
        GameObject haz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        haz.name = "SkyTower_Spike";
        haz.transform.SetParent(parent);
        haz.transform.position = pos;
        haz.transform.localScale = size;
        haz.GetComponent<MeshRenderer>().material = matSpike;
        haz.AddComponent<Hazard>();
    }

    void CreateGem(Vector3 pos, Transform parent)
    {
        GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gem.name = "SkyTower_Gem";
        gem.transform.SetParent(parent);
        gem.transform.position = pos;
        gem.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        gem.GetComponent<MeshRenderer>().material = matGem;

        SphereCollider sc = gem.GetComponent<SphereCollider>();
        if (sc != null) sc.isTrigger = true;
        gem.AddComponent<GemCollectible>();
    }
}
