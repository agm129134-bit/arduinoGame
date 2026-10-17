using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TrackBuilderManager : MonoBehaviour
{
    public static TrackBuilderManager Instance { get; private set; }

    [Header("Builder Status")]
    public bool isEditMode = true;
    public int cursorCol = 0; // -2 ~ 2
    public int cursorRow = 0; // 0 ~ 199

    public const int MAX_ROWS = 200; // 擴充上限至 200 排 (400 米史詩賽道)，徹底突破 59 排限制
    private const float COL_WIDTH = 2.0f;
    private const float ROW_LEN = 2.0f;

    // 0: 空, 1: 地磚, 2: 彈跳墊, 3: 尖刺, 4: 寶石
    private int[,] grid = new int[5, MAX_ROWS];
    private GameObject[,] visualObjects = new GameObject[5, MAX_ROWS];

    private GameObject cursorVisual;
    private Camera mainCamera;
    private RollingSkyBallController testBall;

    [Header("Visual Materials")]
    private Material matTileDark;
    private Material matTileCyan;
    private Material matHazard;
    private Material matJumpPad;
    private Material matGem;

    [Header("UI References")]
    public GameObject builderUIPanel;
    public Text infoText;

    void Awake()
    {
        Instance = this;
    }

    void EnsureMaterials()
    {
        if (RollingSkyTrackGenerator.Instance != null)
        {
            matTileDark = RollingSkyTrackGenerator.Instance.matTileDark;
            matTileCyan = RollingSkyTrackGenerator.Instance.matTileCyan;
            matHazard = RollingSkyTrackGenerator.Instance.matHazard;
            matJumpPad = RollingSkyTrackGenerator.Instance.matJumpPad;
            matGem = RollingSkyTrackGenerator.Instance.matGem;
        }

        if (matTileDark == null)
        {
            matTileDark = new Material(Shader.Find("Standard"));
            matTileDark.color = new Color(0.08f, 0.12f, 0.22f);
            matTileDark.SetFloat("_Glossiness", 0.7f);
        }
        if (matTileCyan == null)
        {
            matTileCyan = new Material(Shader.Find("Standard"));
            matTileCyan.color = new Color(0f, 0.75f, 1f);
            matTileCyan.SetFloat("_Glossiness", 0.7f);
        }
        if (matHazard == null)
        {
            matHazard = new Material(Shader.Find("Standard"));
            matHazard.color = new Color(1f, 0.15f, 0.15f);
            matHazard.SetColor("_EmissionColor", new Color(1.2f, 0.1f, 0.1f));
            matHazard.EnableKeyword("_EMISSION");
            matHazard.SetFloat("_Glossiness", 0.8f);
        }
        if (matJumpPad == null)
        {
            matJumpPad = new Material(Shader.Find("Standard"));
            matJumpPad.color = new Color(0.1f, 1f, 0.4f);
            matJumpPad.SetColor("_EmissionColor", new Color(0.2f, 1.3f, 0.4f));
            matJumpPad.EnableKeyword("_EMISSION");
            matJumpPad.SetFloat("_Glossiness", 0.8f);
        }
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
    }

    void Start()
    {
        EnsureMaterials();
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            RollingSkyCamera camCtrl = mainCamera.GetComponent<RollingSkyCamera>();
            if (camCtrl != null) camCtrl.enabled = false;
        }

        // 清理場景中任何舊賽道階層與殘留物件，確保給玩家 100% 空白無雜物的畫布
        GameObject legacyTrack = GameObject.Find("--- Track Hierarchy ---");
        if (legacyTrack != null) Destroy(legacyTrack);

        GameObject tr = GameObject.Find("TrackRoot");
        if (tr != null)
        {
            for (int i = tr.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(tr.transform.GetChild(i).gameObject);
            }
        }

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
        GameObject starGate = GameObject.Find("FinishStarGate");
        if (starGate != null) Destroy(starGate);

        // 預設全空畫布，讓玩家從零自由建造，不強制預填任何方塊
        LoadSavedTrack();
        CreateVisualGrid();
        CreateCursor();
        BuildUI();
        SnapCameraToCursor();
    }

    void Update()
    {
        if (!isEditMode)
        {
            // 測試遊玩中按下 ESC 可隨時重返建造模式；若小球墜落或死亡亦自動返回
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopTestPlay();
            }
            else if (testBall != null && (testBall.IsDead || testBall.transform.position.y < -4f))
            {
                OnTestBallDied(testBall.IsDead ? "撞擊障礙失誤" : "小球墜落虛空");
            }
            return;
        }

        HandleInput();
        UpdateCursorTransform();
    }

    void LateUpdate()
    {
        if (!isEditMode || mainCamera == null) return;
        UpdateCameraView();
    }

    void HandleInput()
    {
        // 左右橫移
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            cursorCol = Mathf.Clamp(cursorCol - 1, -2, 2);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            cursorCol = Mathf.Clamp(cursorCol + 1, -2, 2);
        }

        // 前後位移：支援 Shift 加速前進 5 排，大幅提升 200 排賽道導航效率
        int rowStep = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 5 : 1;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            cursorRow = Mathf.Clamp(cursorRow + rowStep, 0, MAX_ROWS - 1);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            cursorRow = Mathf.Clamp(cursorRow - 1, 0, MAX_ROWS - 1);
        }

        // 翻頁快速導航 (PageUp / PageDown 跳躍 15 排，Home / End 首末直達)
        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            cursorRow = Mathf.Clamp(cursorRow + 15, 0, MAX_ROWS - 1);
        }
        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            cursorRow = Mathf.Clamp(cursorRow - 15, 0, MAX_ROWS - 1);
        }
        if (Input.GetKeyDown(KeyCode.Home))
        {
            cursorRow = 0;
        }
        if (Input.GetKeyDown(KeyCode.End))
        {
            cursorRow = MAX_ROWS - 1;
        }

        // 快捷鍵放置物件
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlaceTile(1); // 普通地磚
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlaceTile(2); // 彈跳墊
        if (Input.GetKeyDown(KeyCode.Alpha3)) PlaceTile(3); // 尖刺陷阱
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlaceTile(4); // 水晶寶石
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Backspace)) PlaceTile(0); // 消除

        UpdateInfoText();
    }

    void PlaceTile(int type)
    {
        int colIdx = cursorCol + 2;
        grid[colIdx, cursorRow] = type;
        RefreshSingleVisual(cursorCol, cursorRow);
    }

    void CreateCursor()
    {
        cursorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cursorVisual.name = "BuilderCursor";
        cursorVisual.transform.localScale = new Vector3(COL_WIDTH * 1.05f, 0.6f, ROW_LEN * 1.05f);

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.9f, 0.1f, 0.5f);
        mat.SetColor("_EmissionColor", new Color(1.5f, 1.2f, 0.2f));
        mat.EnableKeyword("_EMISSION");
        cursorVisual.GetComponent<MeshRenderer>().material = mat;
        Destroy(cursorVisual.GetComponent<Collider>());
    }

    void UpdateCursorTransform()
    {
        if (cursorVisual != null)
        {
            cursorVisual.transform.position = new Vector3(cursorCol * COL_WIDTH, 0.3f, cursorRow * ROW_LEN);
        }
    }

    public void SnapCameraToCursor()
    {
        if (mainCamera == null) return;
        Vector3 targetCamPos = new Vector3(cursorCol * COL_WIDTH * 0.35f, 13.5f, (cursorRow * ROW_LEN) - 8.5f);
        mainCamera.transform.position = targetCamPos;
        mainCamera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
    }

    void UpdateCameraView()
    {
        if (mainCamera == null || !isEditMode) return;
        Vector3 targetCamPos = new Vector3(cursorCol * COL_WIDTH * 0.35f, 13.5f, (cursorRow * ROW_LEN) - 8.5f);
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCamPos, Time.unscaledDeltaTime * 10f);
        mainCamera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
    }

    void CreateVisualGrid()
    {
        for (int r = 0; r < MAX_ROWS; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                RefreshSingleVisual(c, r);
            }
        }
    }

    void RefreshSingleVisual(int col, int row)
    {
        int cIdx = col + 2;
        if (visualObjects[cIdx, row] != null)
        {
            Destroy(visualObjects[cIdx, row]);
            visualObjects[cIdx, row] = null;
        }

        int type = grid[cIdx, row];
        if (type == 0) return;

        // 建立單元複合根物件
        GameObject cellObj = new GameObject($"Cell_{col}_{row}");
        cellObj.transform.SetParent(transform);

        // 1. 基礎跑道地磚 (所有機關皆座落於標準地磚上，呈現交替色賽博霓虹質感)
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = "Tile";
        tile.transform.SetParent(cellObj.transform);
        tile.transform.position = new Vector3(col * COL_WIDTH, 0, row * ROW_LEN);
        tile.transform.localScale = new Vector3(COL_WIDTH * 0.96f, 0.4f, ROW_LEN * 0.96f);

        bool isAlt = (Mathf.Abs(col) + row) % 2 == 0;
        tile.GetComponent<MeshRenderer>().material = isAlt ? matTileDark : matTileCyan;

        Vector3 pos = new Vector3(col * COL_WIDTH, 0, row * ROW_LEN);

        // 2. 依類型附加對應特徵物件與鮮明材質
        switch (type)
        {
            case 1: // 普通磚 (已具備基礎地磚)
                break;

            case 2: // 彈跳墊 (翠綠發光跳台)
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "JumpPad";
                pad.transform.SetParent(cellObj.transform);
                pad.transform.position = new Vector3(pos.x, 0.22f, pos.z);
                pad.transform.localScale = new Vector3(COL_WIDTH * 0.88f, 0.16f, ROW_LEN * 0.88f);
                pad.GetComponent<MeshRenderer>().material = matJumpPad;
                BoxCollider jbc = pad.GetComponent<BoxCollider>();
                if (jbc != null) jbc.isTrigger = true;
                pad.AddComponent<JumpPad>();
                break;

            case 3: // 尖刺障礙 (赤紅發光致命尖刺)
                GameObject haz = GameObject.CreatePrimitive(PrimitiveType.Cube);
                haz.name = "Hazard";
                haz.transform.SetParent(cellObj.transform);
                haz.transform.position = new Vector3(pos.x, 0.6f, pos.z);
                haz.transform.localScale = new Vector3(1.35f, 1.0f, 1.35f);
                haz.GetComponent<MeshRenderer>().material = matHazard;
                haz.AddComponent<Hazard>();
                break;

            case 4: // 水晶寶石 (璀璨高亮旋轉金鑽模型)
                GameObject gem = new GameObject("Gem");
                gem.transform.SetParent(cellObj.transform);
                gem.transform.position = new Vector3(pos.x, 0.95f, pos.z);
                gem.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                SphereCollider gsc = gem.AddComponent<SphereCollider>();
                gsc.isTrigger = true;
                gsc.radius = 0.6f;
                MeshRenderer gmr = gem.AddComponent<MeshRenderer>();
                gmr.material = matGem;
                gem.AddComponent<GemCollectible>();
                break;
        }

        visualObjects[cIdx, row] = cellObj;
    }

    public void SaveTrack()
    {
        CustomTrackData data = new CustomTrackData();
        data.totalRows = MAX_ROWS;

        for (int r = 0; r < MAX_ROWS; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                int type = grid[c + 2, r];
                if (type > 0)
                {
                    data.tiles.Add(new CustomTrackTile { col = c, row = r, y = 0, type = type });
                }
            }
        }

        string json = JsonUtility.ToJson(data, true);
        string path = Application.persistentDataPath + "/custom_track.json";
        File.WriteAllText(path, json);
        Debug.Log("🎉 自訂場地已成功儲存至: " + path);
    }

    void LoadSavedTrack()
    {
        string path = Application.persistentDataPath + "/custom_track.json";
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        CustomTrackData data = JsonUtility.FromJson<CustomTrackData>(json);
        if (data == null || data.tiles == null) return;

        for (int r = 0; r < MAX_ROWS; r++)
        {
            for (int c = 0; c < 5; c++) grid[c, r] = 0;
        }

        foreach (var t in data.tiles)
        {
            if (t.col >= -2 && t.col <= 2 && t.row >= 0 && t.row < MAX_ROWS)
            {
                grid[t.col + 2, t.row] = t.type;
            }
        }
    }

    public void StartTestPlay()
    {
        // 檢查賽道是否具備至少一塊地磚
        int firstRow = -1;
        int firstCol = 0;
        for (int r = 0; r < MAX_ROWS; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                if (grid[c, r] > 0)
                {
                    firstRow = r;
                    firstCol = c - 2;
                    break;
                }
            }
            if (firstRow >= 0) break;
        }

        if (firstRow == -1)
        {
            if (infoText != null)
            {
                infoText.text = "<color=yellow>【無法試玩】賽道目前沒有任何方塊！請先使用按鍵 [1~4] 放置起跑方塊後再進行測試！</color>";
            }
            return;
        }

        SaveTrack();
        isEditMode = false;
        if (cursorVisual != null) cursorVisual.SetActive(false);
        if (builderUIPanel != null) builderUIPanel.SetActive(false);

        // 生成測試小球，精準降落在第一塊方塊上方
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bObj.name = "TestBall";
        bObj.transform.position = new Vector3(firstCol * COL_WIDTH, 1.2f, firstRow * ROW_LEN);
        bObj.AddComponent<Rigidbody>();
        testBall = bObj.AddComponent<RollingSkyBallController>();

        // 啟用攝影機跟隨小球
        if (mainCamera != null)
        {
            RollingSkyCamera camCtrl = mainCamera.GetComponent<RollingSkyCamera>();
            if (camCtrl == null) camCtrl = mainCamera.gameObject.AddComponent<RollingSkyCamera>();
            camCtrl.enabled = true;
            camCtrl.target = bObj.transform;
        }
    }

    public void OnTestBallDied(string reason)
    {
        Debug.Log($"[TrackBuilder] 試玩小球判定結束: {reason}，重設返回編輯視角");
        StopTestPlay();
        if (infoText != null)
        {
            infoText.text = $"<color=orange>【試玩結束】{reason}</color> | 點擊下方 [▶ 測試遊玩] 可再次實測 | [WASD] 繼續編輯地圖";
        }
    }

    public void StopTestPlay()
    {
        isEditMode = true;
        Time.timeScale = 1.0f; // 確保時間尺度正常

        if (cursorVisual != null) cursorVisual.SetActive(true);
        if (builderUIPanel != null) builderUIPanel.SetActive(true);

        if (testBall != null)
        {
            Destroy(testBall.gameObject);
            testBall = null;
        }

        if (mainCamera != null)
        {
            RollingSkyCamera camCtrl = mainCamera.GetComponent<RollingSkyCamera>();
            if (camCtrl != null)
            {
                camCtrl.enabled = false;
                camCtrl.target = null;
            }
        }

        SnapCameraToCursor();
    }

    void BuildUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cObj = new GameObject("BuilderCanvas");
            canvas = cObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cObj.AddComponent<CanvasScaler>();
            cObj.AddComponent<GraphicRaycaster>();
        }

        builderUIPanel = new GameObject("BuilderHUD");
        builderUIPanel.transform.SetParent(canvas.transform, false);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 頂部資訊列
        GameObject tObj = new GameObject("InfoText");
        tObj.transform.SetParent(builderUIPanel.transform, false);
        infoText = tObj.AddComponent<Text>();
        infoText.font = font;
        infoText.fontSize = 20;
        infoText.color = Color.cyan;
        infoText.alignment = TextAnchor.UpperCenter;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;
        infoText.lineSpacing = 1.15f;
        RectTransform rt = tObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -25);
        rt.sizeDelta = new Vector2(1750, 130);

        // 底部工具列按鈕
        CreateButton(builderUIPanel.transform, "Btn_Play", "▶ 測試遊玩 (PLAY)", new Vector2(-360, 60), new Vector2(220, 55), StartTestPlay, font);
        CreateButton(builderUIPanel.transform, "Btn_Save", "💾 儲存場地 (SAVE)", new Vector2(-120, 60), new Vector2(220, 55), SaveTrack, font);
        CreateButton(builderUIPanel.transform, "Btn_Clear", "🗑 清空賽道 (CLEAR)", new Vector2(120, 60), new Vector2(220, 55), ClearGrid, font);
        CreateButton(builderUIPanel.transform, "Btn_Exit", "🏠 返回選單 (BACK)", new Vector2(360, 60), new Vector2(220, 55), ReturnToMenu, font);

        UpdateInfoText();
    }

    void CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.12f, 0.2f, 0.35f, 0.95f);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text t = txtObj.AddComponent<Text>();
        t.font = font;
        t.fontSize = 20;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = label;
        RectTransform trt = txtObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
    }

    void ClearGrid()
    {
        for (int r = 0; r < MAX_ROWS; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                grid[c, r] = 0;
            }
        }
        CreateVisualGrid();
        SaveTrack();
        if (infoText != null)
        {
            infoText.text = "<color=yellow>【賽道已完全清空】地圖已歸零重置，請自由從零開始建造！</color>";
        }
    }

    void ReturnToMenu()
    {
        SaveTrack();
        PlayerPrefs.SetInt("IsBuilderMode", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenuScene");
    }

    void UpdateInfoText()
    {
        if (infoText == null) return;
        int currentType = grid[cursorCol + 2, cursorRow];
        string typeName = "空隙 (None)";
        if (currentType == 1) typeName = "普通賽道磚 (Tile)";
        if (currentType == 2) typeName = "彈跳墊 (JumpPad)";
        if (currentType == 3) typeName = "尖刺陷阱 (Hazard)";
        if (currentType == 4) typeName = "水晶寶石 (Gem)";

        infoText.text = $"【自訂場地編輯器】 游標座標: (欄 {cursorCol}, 排 {cursorRow} / {MAX_ROWS - 1}) | 當前物件: <color=yellow>{typeName}</color>\n" +
                        "移動操作: [WASD / 方向鍵] 移動游標 (Shift: 加速5排 | PgUp/PgDn: 加速15排 | Home/End: 直達首尾)\n" +
                        "放置物件: [1] 普通地磚  |  [2] 彈跳墊  |  [3] 尖刺陷阱  |  [4] 水晶寶石  |  [0/Del/Backspace] 消除方塊";
    }
}
