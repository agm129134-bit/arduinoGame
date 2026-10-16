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
    public int cursorRow = 0; // 0 ~ 59

    private const int MAX_ROWS = 60;
    private const float COL_WIDTH = 2.0f;
    private const float ROW_LEN = 2.0f;

    // 0: 空, 1: 地磚, 2: 彈跳墊, 3: 尖刺, 4: 寶石
    private int[,] grid = new int[5, MAX_ROWS];
    private GameObject[,] visualObjects = new GameObject[5, MAX_ROWS];

    private GameObject cursorVisual;
    private Camera mainCamera;
    private RollingSkyBallController testBall;

    [Header("UI References")]
    public GameObject builderUIPanel;
    public Text infoText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;

        // 預設填入前 5 排地磚作為起跑台
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                grid[c, r] = 1;
            }
        }

        LoadSavedTrack();
        CreateVisualGrid();
        CreateCursor();
        BuildUI();
        UpdateCameraView();
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
        UpdateCameraView();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            cursorCol = Mathf.Clamp(cursorCol - 1, -2, 2);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            cursorCol = Mathf.Clamp(cursorCol + 1, -2, 2);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            cursorRow = Mathf.Clamp(cursorRow + 1, 0, MAX_ROWS - 1);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            cursorRow = Mathf.Clamp(cursorRow - 1, 0, MAX_ROWS - 1);
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

    void UpdateCameraView()
    {
        if (mainCamera == null || !isEditMode) return;
        Vector3 targetCamPos = new Vector3(cursorCol * COL_WIDTH * 0.4f, 12.0f, (cursorRow * ROW_LEN) - 10.0f);
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCamPos, Time.deltaTime * 6f);
        mainCamera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
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
        }

        int type = grid[cIdx, row];
        if (type == 0) return;

        GameObject obj = null;
        Vector3 pos = new Vector3(col * COL_WIDTH, 0, row * ROW_LEN);

        switch (type)
        {
            case 1: // 普通磚
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.position = pos;
                obj.transform.localScale = new Vector3(COL_WIDTH * 0.95f, 0.4f, ROW_LEN * 0.95f);
                break;
            case 2: // 彈跳墊
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.position = new Vector3(pos.x, 0.1f, pos.z);
                obj.transform.localScale = new Vector3(COL_WIDTH * 0.9f, 0.3f, ROW_LEN * 0.9f);
                BoxCollider jbc = obj.GetComponent<BoxCollider>();
                if (jbc != null) jbc.isTrigger = true;
                obj.AddComponent<JumpPad>();
                break;
            case 3: // 尖刺
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                obj.transform.localScale = new Vector3(1.3f, 1.0f, 1.3f);
                obj.AddComponent<Hazard>();
                break;
            case 4: // 寶石
                obj = new GameObject("Gem");
                obj.transform.position = new Vector3(pos.x, 0.8f, pos.z);
                obj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                SphereCollider gsc = obj.AddComponent<SphereCollider>();
                gsc.isTrigger = true;
                gsc.radius = 0.8f;
                obj.AddComponent<GemCollectible>();
                break;
        }

        if (obj != null)
        {
            obj.transform.SetParent(transform);
            visualObjects[cIdx, row] = obj;
        }
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
        SaveTrack();
        isEditMode = false;
        if (cursorVisual != null) cursorVisual.SetActive(false);
        if (builderUIPanel != null) builderUIPanel.SetActive(false);

        // 生成測試小球
        GameObject bObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bObj.name = "TestBall";
        bObj.transform.position = new Vector3(0, 1.0f, 2.0f);
        bObj.AddComponent<Rigidbody>();
        testBall = bObj.AddComponent<RollingSkyBallController>();

        // 綁定攝影機跟隨
        RollingSkyCamera camCtrl = mainCamera.GetComponent<RollingSkyCamera>();
        if (camCtrl == null) camCtrl = mainCamera.gameObject.AddComponent<RollingSkyCamera>();
        camCtrl.target = bObj.transform;
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

        RollingSkyCamera camCtrl = mainCamera.GetComponent<RollingSkyCamera>();
        if (camCtrl != null) Destroy(camCtrl);

        UpdateCameraView();
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
        infoText.fontSize = 24;
        infoText.color = Color.cyan;
        infoText.alignment = TextAnchor.UpperCenter;
        RectTransform rt = tObj.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -30);
        rt.sizeDelta = new Vector2(1200, 80);

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
                grid[c, r] = (r < 5) ? 1 : 0;
            }
        }
        CreateVisualGrid();
        SaveTrack();
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

        infoText.text = $"【自訂場地編輯器】 游標座標: (欄 {cursorCol}, 排 {cursorRow}) | 當前物件: <color=yellow>{typeName}</color>\n" +
                        "按鍵提示: [WASD/方向鍵] 移動游標 | [1] 地磚 | [2] 彈跳墊 | [3] 尖刺 | [4] 寶石 | [0] 消除";
    }
}
