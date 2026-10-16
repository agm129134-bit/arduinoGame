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

        if (isBuilder)
        {
            // 在自訂建造模式下，由 TrackBuilderManager 全權掌管建造與視覺，本生成器不預先生成靜態賽道
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

    void Update()
    {
        if (isEndlessActive && ballTransform != null)
        {
            float ballZ = ballTransform.position.z;

            // 確保前方恆定維持 120 米以上的跑道預加載，永不中斷
            while (currentFarthestZ < ballZ + 120.0f)
            {
                SpawnEndlessChunk();
            }

            // 安全銷毀：只有當整個 Chunk 的結束點 (endZ) 徹底落後小球 35 米以上才銷毀
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                if (activeChunks[i] != null && (ballZ - activeChunks[i].endZ > 35.0f))
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
        if (matGem == null) matGem = CreateFallbackMat(new Color(0.2f, 0.95f, 1f));
        if (matGoal == null) matGoal = CreateFallbackMat(new Color(1f, 0.85f, 0.1f));
    }

    Material CreateFallbackMat(Color col)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = col;
        m.SetFloat("_Glossiness", 0.7f);
        return m;
    }

    GameObject GetOrCreateTrackRoot()
    {
        GameObject root = GameObject.Find("TrackRoot");
        if (root != null)
        {
            // 清理既有靜態路面，確保動態生成不重疊
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(root.transform.GetChild(i).gameObject);
            }
        }
        else
        {
            root = new GameObject("TrackRoot");
        }
        return root;
    }

    // ==================== 10 大關卡動態生成系統 ====================
    public void BuildLevel(int level)
    {
        GameObject root = GetOrCreateTrackRoot();
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

        // 更新遊戲管理器總長度與寶石數
        RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
        if (gm != null)
        {
            gm.trackLength = 220f + (level * 15f);
            gm.ResetLevelStats();
        }
    }

    // ---------- LEVEL 1 : 霓虹大道 (Neon Avenue) ----------
    void BuildLevel_1(Transform parent)
    {
        // 寬敞 5 線道，適合新手與熟悉感測操作，寶石密度激增 (22 顆)
        BuildSection(parent, 0, 30, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 30, 60, new int[] { -1, 0, 1 });
        BuildSection(parent, 60, 90, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 90, 115, new int[] { -1, 0, 1 });

        // 寶石導引陣列 (前三關大量寶石)
        for (int z = 10; z < 100; z += 4)
        {
            float x = Mathf.Sin(z * 0.1f) * COL_WIDTH;
            CreateGem(parent, new Vector3(x, 0.8f, z));
        }

        // 少量溫和障礙
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 35));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 48));
        CreateHazard(parent, new Vector3(0, 0.5f, 75));

        // 終點
        CreateFinishGate(parent, new Vector3(0, 1.5f, 115));
    }

    // ---------- LEVEL 2 : 虛空躍升 (Void Leap) ----------
    void BuildLevel_2(Transform parent)
    {
        // 包含 3 個跳躍墊與懸空寶石弧線 (24 顆寶石)
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });
        // 第一跳台
        CreateJumpPad(parent, new Vector3(0, 0.1f, 25));
        CreateGem(parent, new Vector3(0, 3.5f, 32));
        CreateGem(parent, new Vector3(0, 4.2f, 36));
        CreateGem(parent, new Vector3(0, 3.5f, 40));

        BuildSection(parent, 22, 45, new int[] { -1, 0, 1 }); // 44m ~ 90m
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 54));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 66));

        // 第二跳台
        CreateJumpPad(parent, new Vector3(0, 0.1f, 85));
        CreateGem(parent, new Vector3(0, 4f, 95));
        CreateGem(parent, new Vector3(0, 4.5f, 100));

        BuildSection(parent, 56, 80, new int[] { -2, -1, 0, 1, 2 }); // 112m ~ 160m
        for (int z = 115; z < 155; z += 5)
        {
            CreateGem(parent, new Vector3(-COL_WIDTH, 0.8f, z));
            CreateGem(parent, new Vector3(COL_WIDTH, 0.8f, z));
        }

        CreateFinishGate(parent, new Vector3(0, 1.5f, 160));
    }

    // ---------- LEVEL 3 : 疾速迴旋 (Speed Spiral) ----------
    void BuildLevel_3(Transform parent)
    {
        // S型蛇行賽道與左右避障 (22 顆寶石)
        BuildSection(parent, 0, 20, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 20, 40, new int[] { -2, -1, 0 });   // 偏左
        BuildSection(parent, 40, 60, new int[] { 0, 1, 2 });     // 偏右
        BuildSection(parent, 60, 80, new int[] { -1, 0, 1 });    // 居中
        BuildSection(parent, 80, 105, new int[] { -2, -1, 0, 1, 2 });

        for (int z = 12; z < 180; z += 8)
        {
            CreateGem(parent, new Vector3(Mathf.Sin(z * 0.15f) * COL_WIDTH * 1.5f, 0.8f, z));
        }

        CreateHazard(parent, new Vector3(0, 0.5f, 42));
        CreateHazard(parent, new Vector3(-COL_WIDTH, 0.5f, 85));
        CreateHazard(parent, new Vector3(COL_WIDTH, 0.5f, 105));
        CreateHazard(parent, new Vector3(0, 0.5f, 130));

        CreateFinishGate(parent, new Vector3(0, 1.5f, 210));
    }

    // ---------- LEVEL 4 : 斷崖連跳 (Chasm Bounce) ----------
    void BuildLevel_4(Transform parent)
    {
        BuildSection(parent, 0, 20, new int[] { -1, 0, 1 });
        CreateJumpPad(parent, new Vector3(0, 0.1f, 38));
        BuildSection(parent, 26, 45, new int[] { 0, 1 }); // 落地右側窄橋
        CreateJumpPad(parent, new Vector3(COL_WIDTH, 0.1f, 88));
        BuildSection(parent, 52, 75, new int[] { -1, 0 }); // 落地左側窄橋
        CreateJumpPad(parent, new Vector3(-COL_WIDTH, 0.1f, 148));
        BuildSection(parent, 82, 110, new int[] { -2, -1, 0, 1, 2 });

        for (int z = 15; z < 210; z += 12)
        {
            CreateGem(parent, new Vector3(0, 1.0f, z));
        }

        CreateFinishGate(parent, new Vector3(0, 1.5f, 220));
    }

    // ---------- LEVEL 5 : 迷宮尖刺 (Spike Labyrinth) ----------
    void BuildLevel_5(Transform parent)
    {
        BuildSection(parent, 0, 115, new int[] { -2, -1, 0, 1, 2 });
        // 交錯尖刺陣列，考驗左右傾角微調操作
        for (int z = 30; z < 220; z += 15)
        {
            int blockedCol = ((z / 15) % 2 == 0) ? 1 : -1;
            CreateHazard(parent, new Vector3(blockedCol * COL_WIDTH, 0.5f, z));
            CreateHazard(parent, new Vector3(0, 0.5f, z + 5));
            CreateGem(parent, new Vector3(-blockedCol * COL_WIDTH, 0.8f, z));
        }
        CreateFinishGate(parent, new Vector3(0, 1.5f, 230));
    }

    // ---------- LEVEL 6 : 升降律動 (Piston Pulse) ----------
    void BuildLevel_6(Transform parent)
    {
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 25, 45, new int[] { -1, 0, 1 }, 1.5f); // 升高階梯
        BuildSection(parent, 45, 65, new int[] { -1, 0, 1 }, 3.0f); // 第二層高空
        CreateJumpPad(parent, new Vector3(0, 3.1f, 128));
        BuildSection(parent, 72, 115, new int[] { -2, -1, 0, 1, 2 });

        for (int z = 20; z < 220; z += 10)
        {
            CreateGem(parent, new Vector3(0, 1.2f, z));
        }
        CreateFinishGate(parent, new Vector3(0, 1.5f, 230));
    }

    // ---------- LEVEL 7 : 浮島穿梭 (Floating Islands) ----------
    void BuildLevel_7(Transform parent)
    {
        // 浮空獨立島嶼拼接
        for (int i = 0; i < 6; i++)
        {
            int startRow = i * 20;
            int offsetCol = (i % 2 == 0) ? -1 : 1;
            BuildSection(parent, startRow, startRow + 10, new int[] { offsetCol - 1, offsetCol, offsetCol + 1 });
            if (i < 5)
            {
                CreateJumpPad(parent, new Vector3(offsetCol * COL_WIDTH, 0.1f, (startRow + 9) * ROW_LEN));
                CreateGem(parent, new Vector3(0, 4.0f, (startRow + 14) * ROW_LEN));
            }
        }
        BuildSection(parent, 120, 135, new int[] { -2, -1, 0, 1, 2 });
        CreateFinishGate(parent, new Vector3(0, 1.5f, 270));
    }

    // ---------- LEVEL 8 : 重力逆襲 (Gravity Shift) ----------
    void BuildLevel_8(Transform parent)
    {
        BuildSection(parent, 0, 30, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 30, 50, new int[] { -1, 0 });
        BuildSection(parent, 50, 70, new int[] { 0, 1 });
        BuildSection(parent, 70, 90, new int[] { -2, -1 });
        BuildSection(parent, 90, 110, new int[] { 1, 2 });
        BuildSection(parent, 110, 140, new int[] { -1, 0, 1 });

        for (int z = 20; z < 260; z += 12)
        {
            CreateHazard(parent, new Vector3(0, 0.5f, z));
            CreateGem(parent, new Vector3(COL_WIDTH, 0.8f, z + 4));
            CreateGem(parent, new Vector3(-COL_WIDTH, 0.8f, z + 8));
        }
        CreateFinishGate(parent, new Vector3(0, 1.5f, 280));
    }

    // ---------- LEVEL 9 : 混沌風暴 (Chaos Vortex) ----------
    void BuildLevel_9(Transform parent)
    {
        // 單格鋼索橋 + 連續密集尖刺
        BuildSection(parent, 0, 25, new int[] { -2, -1, 0, 1, 2 });
        BuildSection(parent, 25, 45, new int[] { 0 }); // 單線鋼絲橋
        BuildSection(parent, 45, 65, new int[] { -1, 0, 1 });
        BuildSection(parent, 65, 85, new int[] { 0 }); // 第二段鋼絲
        BuildSection(parent, 85, 120, new int[] { -2, -1, 0, 1, 2 });
        CreateJumpPad(parent, new Vector3(0, 0.1f, 238));
        BuildSection(parent, 128, 150, new int[] { -1, 0, 1 });

        for (int z = 30; z < 290; z += 10)
        {
            CreateGem(parent, new Vector3(0, 1.2f, z));
        }
        CreateFinishGate(parent, new Vector3(0, 1.5f, 300));
    }

    // ---------- LEVEL 10 : 終極躍動 (Apex Transcendence) ----------
    void BuildLevel_10(Transform parent)
    {
        // 350m 史詩高難度終章：全機關大集結
        BuildSection(parent, 0, 30, new int[] { -2, -1, 0, 1, 2 });
        CreateJumpPad(parent, new Vector3(0, 0.1f, 58));
        BuildSection(parent, 36, 60, new int[] { -1, 0, 1 });
        CreateJumpPad(parent, new Vector3(-COL_WIDTH, 0.1f, 118));
        BuildSection(parent, 67, 90, new int[] { 0, 1, 2 });
        CreateJumpPad(parent, new Vector3(COL_WIDTH, 0.1f, 178));
        BuildSection(parent, 98, 130, new int[] { -1, 0, 1 });
        BuildSection(parent, 130, 175, new int[] { -2, -1, 0, 1, 2 });

        for (int z = 20; z < 340; z += 9)
        {
            float x = (z % 2 == 0) ? -COL_WIDTH : COL_WIDTH;
            CreateHazard(parent, new Vector3(x, 0.5f, z));
            CreateGem(parent, new Vector3(-x, 0.8f, z));
        }
        CreateFinishGate(parent, new Vector3(0, 1.5f, 350));
    }

    // ==================== 無盡模式 (Endless Mode) ====================
    public void StartEndlessMode()
    {
        isEndlessActive = true;
        currentFarthestZ = 0f;
        activeChunks.Clear();

        GameObject root = GetOrCreateTrackRoot();

        // 初始前導跑道 (長度 60m)
        GameObject startChunk = new GameObject("Endless_StartChunk");
        startChunk.transform.SetParent(root.transform);
        BuildSection(startChunk.transform, 0, 30, new int[] { -2, -1, 0, 1, 2 });
        for (int z = 10; z < 50; z += 8)
        {
            CreateGem(startChunk.transform, new Vector3(0, 0.8f, z));
        }
        currentFarthestZ = 60.0f;
        activeChunks.Add(new EndlessChunkEntry { go = startChunk, startZ = 0f, endZ = 60.0f });

        // 預先生成前方多個區塊，使起跑時前方至少有 150 米賽道
        for (int i = 0; i < 5; i++)
        {
            SpawnEndlessChunk();
        }
    }

    void SpawnEndlessChunk()
    {
        GameObject root = GetOrCreateTrackRoot();
        float chunkStartZ = currentFarthestZ;
        float chunkEndZ = currentFarthestZ + chunkLength;

        GameObject chunk = new GameObject($"Chunk_{chunkStartZ:F0}_{chunkEndZ:F0}");
        chunk.transform.SetParent(root.transform);

        int chunkType = Random.Range(0, 6);
        int rowsInChunk = (int)(chunkLength / ROW_LEN);
        int startRow = (int)(chunkStartZ / ROW_LEN);

        switch (chunkType)
        {
            case 0: // 寬賽道 + 隨機尖刺
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateHazard(chunk.transform, new Vector3(Random.Range(-1, 2) * COL_WIDTH, 0.5f, chunkStartZ + 15));
                break;
            case 1: // 3線道蛇行變道
                int offset = Random.Range(-1, 2);
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { offset - 1, offset, offset + 1 });
                CreateGem(chunk.transform, new Vector3(offset * COL_WIDTH, 0.8f, chunkStartZ + 10));
                CreateGem(chunk.transform, new Vector3(offset * COL_WIDTH, 0.8f, chunkStartZ + 20));
                break;
            case 2: // 彈跳飛躍區（安全防掉落設計：全線道彈跳墊 + 寬著陸台）
                BuildSection(chunk.transform, startRow, startRow + 5, new int[] { -1, 0, 1 });
                CreateJumpPad(chunk.transform, new Vector3(-COL_WIDTH, 0.1f, (startRow + 4) * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(0, 0.1f, (startRow + 4) * ROW_LEN));
                CreateJumpPad(chunk.transform, new Vector3(COL_WIDTH, 0.1f, (startRow + 4) * ROW_LEN));
                CreateGem(chunk.transform, new Vector3(0, 3.5f, (startRow + 5) * ROW_LEN));
                // 著陸區：間隔縮小為 2 排 (4 米)，有彈跳墊推進飛躍極為流暢，且即使沒踩中彈墊，普通跳躍也能輕鬆越過！
                BuildSection(chunk.transform, startRow + 7, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                break;
            case 3: // 雙側尖刺封路，單路通行
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateHazard(chunk.transform, new Vector3(-COL_WIDTH, 0.5f, chunkStartZ + 12));
                CreateHazard(chunk.transform, new Vector3(COL_WIDTH, 0.5f, chunkStartZ + 12));
                CreateGem(chunk.transform, new Vector3(0, 0.8f, chunkStartZ + 12));
                break;
            case 4: // 水晶豐收賽道 (獎勵路段)
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -1, 0, 1 });
                for (float z = chunkStartZ + 4; z < chunkEndZ; z += 6)
                {
                    CreateGem(chunk.transform, new Vector3(0, 0.8f, z));
                }
                break;
            case 5: // 穿梭障礙區 (交錯障礙與兩側寶石)
                BuildSection(chunk.transform, startRow, startRow + rowsInChunk, new int[] { -2, -1, 0, 1, 2 });
                CreateHazard(chunk.transform, new Vector3(0, 0.5f, chunkStartZ + 8));
                CreateHazard(chunk.transform, new Vector3(-COL_WIDTH, 0.5f, chunkStartZ + 18));
                CreateHazard(chunk.transform, new Vector3(COL_WIDTH, 0.5f, chunkStartZ + 18));
                CreateGem(chunk.transform, new Vector3(-COL_WIDTH, 0.8f, chunkStartZ + 8));
                CreateGem(chunk.transform, new Vector3(COL_WIDTH, 0.8f, chunkStartZ + 8));
                CreateGem(chunk.transform, new Vector3(0, 0.8f, chunkStartZ + 18));
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
            BuildLevel_1(GetOrCreateTrackRoot().transform);
            return;
        }

        string json = File.ReadAllText(path);
        CustomTrackData trackData = JsonUtility.FromJson<CustomTrackData>(json);
        if (trackData == null || trackData.tiles == null) return;

        GameObject root = GetOrCreateTrackRoot();
        Transform parent = root.transform;

        foreach (var tile in trackData.tiles)
        {
            Vector3 pos = new Vector3(tile.col * COL_WIDTH, tile.y, tile.row * ROW_LEN);
            switch (tile.type)
            {
                case 1: // 普通磚
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    break;
                case 2: // 彈跳墊
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateJumpPad(parent, new Vector3(pos.x, pos.y + 0.1f, pos.z));
                    break;
                case 3: // 尖刺
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateHazard(parent, new Vector3(pos.x, pos.y + 0.5f, pos.z));
                    break;
                case 4: // 寶石
                    CreateSingleTile(parent, tile.col, tile.row, tile.y);
                    CreateGem(parent, new Vector3(pos.x, pos.y + 0.8f, pos.z));
                    break;
            }
        }

        // 於最後一行放置終點星門
        CreateFinishGate(parent, new Vector3(0, 1.5f, (trackData.totalRows + 2) * ROW_LEN));
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

    void CreateJumpPad(Transform parent, Vector3 pos)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "JumpPad";
        pad.transform.SetParent(parent);
        pad.transform.position = pos;
        pad.transform.localScale = new Vector3(1.6f, 0.2f, 1.6f);
        pad.GetComponent<MeshRenderer>().material = matJumpPad;
        BoxCollider bc = pad.GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;
        pad.AddComponent<JumpPad>();
    }

    void CreateGem(Transform parent, Vector3 pos)
    {
        GameObject gem = new GameObject("Gem");
        gem.transform.SetParent(parent);
        gem.transform.position = pos;
        gem.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        SphereCollider sc = gem.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.8f;
        MeshRenderer mr = gem.AddComponent<MeshRenderer>();
        mr.material = matGem;
        gem.AddComponent<GemCollectible>();
    }

    void CreateFinishGate(Transform parent, Vector3 pos)
    {
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "FinishGate";
        gate.transform.SetParent(parent);
        gate.transform.position = pos;
        gate.transform.localScale = new Vector3(COL_WIDTH * 5f, 3.0f, 1.0f);
        gate.GetComponent<MeshRenderer>().material = matGoal;
        BoxCollider bc = gate.GetComponent<BoxCollider>();
        bc.isTrigger = true;

        gate.AddComponent<FinishTrigger>();
    }
}

public class FinishTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<RollingSkyBallController>() != null)
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
    public int totalRows = 60;
    public List<CustomTrackTile> tiles = new List<CustomTrackTile>();
}
