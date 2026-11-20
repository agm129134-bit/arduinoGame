using UnityEngine;

public class RollingSkyCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Regular Mode Settings")]
    public Vector3 offset = new Vector3(0, 4.5f, -6.5f);
    public float smoothSpeedX = 10.0f;
    public float lookAheadDistance = 5.0f;

    [Header("Sky Tower 3D Orbit Settings")]
    public float distance = 6.2f;
    public float heightOffset = 1.2f;
    public float pitchMin = -15.0f;
    public float pitchMax = 75.0f;
    public float cameraSensitivity = 2.0f;

    [Header("Runtime Angles")]
    public float yaw = 0f;
    public float pitch = 20f;

    private float currentX;
    private ArduinoReceiver arduino;

    // ---- Arduino 搖桿視角 (自動校準) ----
    // 原本寫死中位 500 + 單純 /500 正規化：實體搖桿靜止值常落在 480~540 (甚至更偏)，造成相機持續漂移或單向轉不動；
    // 且靈敏度 220°/s x cameraSensitivity(2.0) = 440°/s 太躁、曲線近乎線性 → 微調不易；Camera_Sensitivity 只在 Awake 讀一次。
    private Vector2 joyCenter = new Vector2(512f, 512f);
    private bool joyCalibrated = false;
    private Vector2 joyLastRaw;
    private float joyStableTime = 0f;
    private Vector2 joySmoothed = Vector2.zero;
    private float sensRefreshTimer = 0f;
    private const float JoyDeadzone = 0.07f;      // 正規化死區 (約 ±35 單位)
    private const float JoyCurveExponent = 1.4f;  // 指數曲線：小幅微調細膩、推到底仍有速度
    private const float JoyYawSpeed = 200f;       // 全推水平角速度 (度/秒，cameraSensitivity=2.0 時)
    private const float JoyPitchSpeed = 110f;     // 全推垂直角速度
    private const float JoySmoothRate = 28f;      // 一階低通 (時間常數約 36ms)，平滑但幾乎無延遲

    public Vector3 CameraForwardXZ
    {
        get
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
        }
    }

    public Vector3 CameraRightXZ
    {
        get
        {
            Vector3 r = transform.right;
            r.y = 0f;
            return r.sqrMagnitude > 0.001f ? r.normalized : Vector3.right;
        }
    }

    void Awake()
    {
        ReloadSettings();
    }

    void Start()
    {
        if (target != null)
        {
            currentX = target.position.x;
            transform.position = target.position + offset;
        }
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
    }

    public void ReloadSettings()
    {
        cameraSensitivity = PlayerPrefs.GetFloat("Camera_Sensitivity", 2.0f);
    }

    void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        bool isSkyTower = (PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1);
        if (isSkyTower)
        {
            HandleSkyTowerCamera();
            return;
        }

        // 常規模式：平滑追蹤 X 軸 (避免左右晃動過劇)
        currentX = Mathf.Lerp(currentX, target.position.x, smoothSpeedX * Time.deltaTime);

        // 嚴格跟隨 Z 軸向前衝刺
        Vector3 origDesiredPos = new Vector3(currentX, target.position.y + offset.y, target.position.z + offset.z);
        transform.position = origDesiredPos;

        // 俯角朝向小球前方賽道
        Vector3 lookTarget = target.position + Vector3.forward * lookAheadDistance;
        transform.LookAt(lookTarget);
    }

    void UpdateJoystickCamera()
    {
        // 定期重讀設定 (設定選單調整 Camera_Sensitivity 後即時生效)
        sensRefreshTimer -= Time.unscaledDeltaTime;
        if (sensRefreshTimer <= 0f)
        {
            sensRefreshTimer = 0.5f;
            ReloadSettings();
        }

        if (arduino == null || !arduino.IsConnected)
        {
            joyCalibrated = false;
            joySmoothed = Vector2.zero;
            return;
        }

        Vector2 raw = arduino.joystick;
        if (raw.x < 0f || raw.x > 1023f || raw.y < 0f || raw.y > 1023f) return; // 雜訊/異常封包

        // 首次取樣：若接近中央 (350~674) 就當作靜止中位，否則用理論中位 512
        if (!joyCalibrated)
        {
            bool nearCenter = raw.x > 350f && raw.x < 674f && raw.y > 350f && raw.y < 674f;
            joyCenter = nearCenter ? raw : new Vector2(512f, 512f);
            joyCalibrated = true;
            joyLastRaw = raw;
            joyStableTime = 0f;
        }

        // 閒置自動校準：搖桿靜止 (抖動 < 6) 超過 0.8 秒且離中位 < 150，緩慢把中位貼近目前讀值
        float dt = Time.unscaledDeltaTime;
        bool still = Mathf.Abs(raw.x - joyLastRaw.x) < 6f && Mathf.Abs(raw.y - joyLastRaw.y) < 6f;
        joyLastRaw = raw;
        joyStableTime = still ? joyStableTime + dt : 0f;
        if (joyStableTime > 0.8f && Mathf.Abs(raw.x - joyCenter.x) < 150f && Mathf.Abs(raw.y - joyCenter.y) < 150f)
        {
            joyCenter = Vector2.Lerp(joyCenter, raw, 1f - Mathf.Exp(-3f * dt));
        }

        Vector2 target = new Vector2(NormalizeJoyAxis(raw.x, joyCenter.x), NormalizeJoyAxis(raw.y, joyCenter.y));
        float smooth = 1f - Mathf.Exp(-JoySmoothRate * dt);
        joySmoothed = Vector2.Lerp(joySmoothed, target, smooth);

        // cameraSensitivity 預設 2.0 為基準 (scale = 1)
        float scale = Mathf.Max(0.1f, cameraSensitivity) / 2.0f;
        float gameDt = Time.deltaTime;
        yaw += joySmoothed.x * JoyYawSpeed * scale * gameDt;
        pitch -= joySmoothed.y * JoyPitchSpeed * scale * gameDt;
    }

    // 以校準中位正規化到 -1~1：全推判定在可用行程的 85% (實體搖桿常推不到 0/1023)，死區後套指數曲線
    static float NormalizeJoyAxis(float value, float center)
    {
        float d = value - center;
        float span = (d >= 0f ? (1023f - center) : center) * 0.85f;
        float n = Mathf.Clamp(d / Mathf.Max(span, 1f), -1f, 1f);
        float a = Mathf.Abs(n);
        if (a <= JoyDeadzone) return 0f;
        a = (a - JoyDeadzone) / (1f - JoyDeadzone);
        return Mathf.Sign(n) * Mathf.Pow(a, JoyCurveExponent);
    }

    void HandleSkyTowerCamera()
    {
        // 遊戲暫停 (ESC 選單打開) 時不接受旋轉輸入
        if (Time.timeScale > 0.001f)
        {
            // 1. 滑鼠轉動視角 (Mouse X 控制水平環繞 Yaw，Mouse Y 控制垂直俯仰 Pitch)
            // 僅在游標鎖定狀態（正常遊戲中）響應滑鼠旋轉，避免在選單/結算彈窗誤觸相機旋轉
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                if (Mathf.Abs(mouseX) > 0.0001f)
                {
                    yaw += mouseX * cameraSensitivity * 2.2f;
                }
                if (Mathf.Abs(mouseY) > 0.0001f)
                {
                    pitch -= mouseY * cameraSensitivity * 1.8f;
                }
            }

            // 2. Arduino 雙軸實體搖桿視角控制：自動校準中位 + 小死區 + 指數曲線 + 低延遲平滑 + 套用 Camera_Sensitivity
            if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
            UpdateJoystickCamera();

            // 3. 鍵盤 Q / E 視角快速旋轉輔助
            float keyTurn = 0f;
            if (Input.GetKey(KeyCode.Q)) keyTurn -= 1f;
            if (Input.GetKey(KeyCode.E)) keyTurn += 1f;
            if (Mathf.Abs(keyTurn) > 0.01f)
            {
                yaw += keyTurn * cameraSensitivity * 75f * Time.deltaTime;
            }
        }

        // 俯仰角限制：-15° (微仰視高空塔樓) 至 75° (俯視落點平台)
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 圍繞小球計算 360 度環繞位置
        Vector3 targetFocus = target.position + Vector3.up * heightOffset;
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredCamPos = targetFocus + (camRotation * new Vector3(0f, 0f, -distance));

        // 柔和插值跟隨與朝向
        transform.position = Vector3.Lerp(transform.position, desiredCamPos, 18f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, camRotation, 22f * Time.deltaTime);
    }
}
