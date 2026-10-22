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

    void HandleSkyTowerCamera()
    {
        // 遊戲暫停 (ESC 選單打開) 時不接受旋轉輸入
        if (Time.timeScale > 0.001f)
        {
            // 1. 滑鼠轉動視角 (Mouse X 控制水平環繞 Yaw，Mouse Y 控制垂直俯仰 Pitch)
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

            // 2. Arduino 雙軸實體搖桿視角控制 (中位值約 500)
            if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();
            if (arduino != null)
            {
                float joyX = (arduino.joystick.x - 500f) / 500f;
                float joyY = (arduino.joystick.y - 500f) / 500f;
                const float joyDeadzone = 0.15f;

                if (Mathf.Abs(joyX) > joyDeadzone)
                {
                    float sign = Mathf.Sign(joyX);
                    yaw += sign * (Mathf.Abs(joyX) - joyDeadzone) * cameraSensitivity * 90f * Time.deltaTime;
                }
                if (Mathf.Abs(joyY) > joyDeadzone)
                {
                    float sign = Mathf.Sign(joyY);
                    pitch -= sign * (Mathf.Abs(joyY) - joyDeadzone) * cameraSensitivity * 70f * Time.deltaTime;
                }
            }

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
