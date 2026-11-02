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
        CreatePlatform(new Vector3(0, 0, 0), new Vector3(16f, 2.0f, 16f), matGround, hub.transform, "StartPlatform");

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

    // ==================== 1. Zone 1: 0m - 50m 晨曦起點 ====================
    void BuildZone1_DawnAscent(Transform parent)
    {
        GameObject zone = new GameObject("Zone1_DawnAscent (0-50m)");
        zone.transform.SetParent(parent);

        int steps = 25;
        for (int i = 0; i < steps; i++)
        {
            float y = 2.0f + i * 1.95f; // 升至約 49m
            float x = Mathf.Sin(i * 0.35f) * 6.5f;
            float z = 8.0f + i * 4.6f; // 步進 4.6m，徹底消除相鄰階梯前後重疊

            Vector3 pos = new Vector3(x, y, z);
            // 平臺全面加寬至 4.8m~6.5m 大方磚，跳躍更寬容
            Vector3 size = (i == 12 || i == steps - 1) ? new Vector3(6.5f, 0.45f, 6.5f) : new Vector3(4.8f, 0.45f, 4.8f);

            CreatePlatform(pos, size, matZone1_Dawn, zone.transform, $"Z1_Step_{i}");

            if (i % 3 == 0) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 2. Zone 2: 50m - 120m 霓虹管陣 ====================
    void BuildZone2_NeonPipes(Transform parent)
    {
        GameObject zone = new GameObject("Zone2_NeonPipes (50-120m)");
        zone.transform.SetParent(parent);

        int steps = 28;
        float startZ = 8.0f + 25 * 4.6f; // 銜接 Zone 1 終點 (~123m)

        for (int i = 0; i < steps; i++)
        {
            float y = 51.5f + i * 2.45f; // 升至約 118m
            // 沿全新 X 側翼延伸展開，徹底遠離 Zone 1 上空，杜絕垂直重疊夾角
            float x = 12.0f + Mathf.Cos(i * 0.32f) * 7.5f;
            float z = startZ + i * 4.8f; // 恆定向前推進，無任何回頭重疊

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = (i % 5 == 0) ? new Vector3(6.5f, 0.45f, 6.5f) : new Vector3(4.8f, 0.45f, 4.8f);

            CreatePlatform(pos, size, matZone2_Neon, zone.transform, $"Z2_Pipe_{i}");

            // 關鍵跳台 (前進跳躍躍升)
            if (i == 10 || i == 22)
            {
                CreateJumpPad(pos + Vector3.up * 0.3f, 13.0f, zone.transform);
            }

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

        int steps = 38;
        float radius = 16.0f; // 大幅擴展螺旋半徑至 16m，階梯水平周長充足

        for (int i = 0; i < steps; i++)
        {
            float y = 120.0f + i * 2.65f; // 升至約 218m，垂直淨空大幅提高
            float angle = i * 20.0f * Mathf.Deg2Rad; // 每圈需 18 階，跨圈垂直落差達 47m，徹底杜絕跨層上下重疊卡死
            float x = coreCenter.x + Mathf.Cos(angle) * radius;
            float z = coreCenter.z + Mathf.Sin(angle) * radius;

            Vector3 pos = new Vector3(x, y, z);
            Vector3 size = new Vector3(4.8f, 0.45f, 4.8f); // 平臺擴大至 4.8m 寬敞大板塊

            GameObject plat = CreatePlatform(pos, size, matZone3_Spiral, zone.transform, $"Z3_SpiralStep_{i}");

            // 平緩水平漂移浮島
            if (i == 14 || i == 26)
            {
                SkyTowerMovingPlatform mover = plat.AddComponent<SkyTowerMovingPlatform>();
                mover.moveDirection = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                mover.moveDistance = 2.0f;
                mover.moveSpeed = 1.0f;
            }

            // 垂直跳躍台
            if (i == 32)
            {
                CreateJumpPad(pos + Vector3.up * 0.3f, 13.5f, zone.transform);
            }

            if (i % 4 == 0) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 4. Zone 4: 220m - 320m 寬崖攀爬 ====================
    void BuildZone4_NarrowRidges(Transform parent)
    {
        GameObject zone = new GameObject("Zone4_NarrowRidges (220-320m)");
        zone.transform.SetParent(parent);

        int steps = 36;
        float squareSize = 36.0f; // 迴廊外框大幅加寬至 36m
        Vector3 zoneCenter = new Vector3(12f, 0f, 260f);

        for (int i = 0; i < steps; i++)
        {
            float y = 221.5f + i * 2.75f; // 升至約 318m

            // 沿著高空寬闊外廓方框行進
            float t = (i % 16) / 16.0f;
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
            // 由原先 1.6m 極窄懸崖全面加寬至 4.8m 寬敞安全平臺！
            Vector3 size = (i == 18) ? new Vector3(7.0f, 0.5f, 7.0f) : new Vector3(4.8f, 0.45f, 4.8f);

            CreatePlatform(pos, size, matZone4_Hazard, zone.transform, $"Z4_Ridge_{i}");

            // 尖刺機關障礙 (放置於角落邊緣，留出足夠安全通道供 Arduino 平滑通過)
            if (i % 6 == 3 && i != 18)
            {
                CreateHazard(pos + new Vector3(1.6f, 0.35f, 0), new Vector3(1.2f, 0.4f, 1.2f), zone.transform);
            }

            if (i % 4 == 2) CreateGem(pos + Vector3.up * 1.4f, zone.transform);
        }
    }

    // ==================== 5. Zone 5: 320m - 430m 雲端迷蹤 ====================
    void BuildZone5_CloudMaze(Transform parent)
    {
        GameObject zone = new GameObject("Zone5_CloudMaze (320-430m)");
        zone.transform.SetParent(parent);

        int steps = 36;
        Vector3 baseCenter = new Vector3(12f, 0f, 260f);

        for (int i = 0; i < steps; i++)
        {
            float y = 322.0f + i * 2.95f; // 升至約 425m

            // 寬敞雙分岔攀登路線：全面擴大平臺至 5.0m
            float xLeft = baseCenter.x - 8.5f + Mathf.Sin(i * 0.45f) * 5.0f;
            float zLeft = baseCenter.z + Mathf.Cos(i * 0.35f) * 9.0f;

            Vector3 posL = new Vector3(xLeft, y, zLeft);
            CreatePlatform(posL, new Vector3(5.0f, 0.45f, 5.0f), matZone5_Cosmic, zone.transform, $"Z5_MainPath_{i}");

            // 右側捷徑
            if (i % 4 == 0)
            {
                float xRight = baseCenter.x + 9.5f + Mathf.Cos(i * 0.45f) * 4.5f;
                float zRight = baseCenter.z + Mathf.Sin(i * 0.35f) * 8.0f;
                Vector3 posR = new Vector3(xRight, y, zRight);
                CreatePlatform(posR, new Vector3(4.8f, 0.45f, 4.8f), matZone5_Cosmic, zone.transform, $"Z5_ShortPath_{i}");
                CreateJumpPad(posR + Vector3.up * 0.3f, 13.5f, zone.transform);
            }

            if (i % 4 == 0) CreateGem(posL + Vector3.up * 1.4f, zone.transform);
        }

        // 雲端中繼大閣樓 (Y = 428m)
        CreatePlatform(new Vector3(12f, 428.5f, 260f), new Vector3(10.0f, 1.0f, 10.0f), matZone5_Cosmic, zone.transform, "CloudPavilion");
    }

    // ==================== 6. Zone 6: 430m - 500m 凌霄天頂與終焉方碑 ====================
    void BuildZone6_ApexShrine(Transform parent)
    {
        GameObject zone = new GameObject("Zone6_ApexShrine (430-500m)");
        zone.transform.SetParent(parent);

        // 黃金寬闊階梯直通天界
        int steps = 24;
        Vector3 apexTowerCenter = new Vector3(12f, 0f, 260f);

        for (int i = 0; i < steps; i++)
        {
            float y = 431.0f + i * 2.82f; // 升至約 496m
            float ratio = (float)i / steps;
            float radius = Mathf.Lerp(16f, 3.0f, ratio);
            float angle = i * 22f * Mathf.Deg2Rad;

            float x = apexTowerCenter.x + Mathf.Sin(angle) * radius;
            float z = apexTowerCenter.z - Mathf.Cos(angle) * radius;

            Vector3 pos = new Vector3(x, y, z);
            CreatePlatform(pos, new Vector3(5.2f, 0.5f, 5.2f), matZone6_Apex, zone.transform, $"Z6_GoldenStair_{i}");

            if (i % 3 == 0) CreateGem(pos + Vector3.up * 1.5f, zone.transform);
        }

        // ==================== 500m 凌霄神殿大平臺 ====================
        Vector3 apexCenter = new Vector3(12f, 499.5f, 260f);
        CreatePlatform(apexCenter, new Vector3(20f, 2.0f, 20f), matZone6_Apex, zone.transform, "ApexSanctuaryPlatform");

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
