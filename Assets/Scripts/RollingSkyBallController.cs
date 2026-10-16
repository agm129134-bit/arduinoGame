using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RollingSkyBallController : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("自動向前衝刺速度")]
    public float forwardSpeed = 12.0f;
    [Tooltip("左右橫移速度")]
    public float horizontalSpeed = 9.0f;
    [Tooltip("標準跳躍向上推力")]
    public float jumpForce = 8.5f;

    [Header("Hardware / Tilt Settings")]
    public ArduinoReceiver arduino;
    [Tooltip("MPU-6050 加速度計靈敏度")]
    public float tiltSensitivity = 1.8f;
    [Tooltip("手部微抖死區")]
    public float tiltDeadzone = 0.08f;

    [Header("Ground Check")]
    public LayerMask groundLayer = ~0;
    public float groundCheckDistance = 0.55f;

    private Rigidbody rb;
    private bool isGrounded = true;
    private bool isDead = false;
    private float currentHorizontalInput = 0f;
    private bool isEndlessMode = false;
    private float baseForwardSpeed = 12.0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 透過代碼平滑旋轉視覺，防止物理碰撞翻滾卡軌
    }

    private KeyCode customKeyLeft = KeyCode.A;
    private KeyCode customKeyRight = KeyCode.D;

    void Start()
    {
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();

        // 載入玩家自訂鍵盤按鍵與靈敏度
        customKeyLeft = (KeyCode)PlayerPrefs.GetInt("Key_Left", (int)KeyCode.A);
        customKeyRight = (KeyCode)PlayerPrefs.GetInt("Key_Right", (int)KeyCode.D);
        horizontalSpeed = PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f);

        // 載入控制器靈敏度與死區
        tiltSensitivity = PlayerPrefs.GetFloat("Tilt_Sensitivity", 1.8f);
        tiltDeadzone = PlayerPrefs.GetFloat("Tilt_Deadzone", 0.08f);

        isEndlessMode = (PlayerPrefs.GetInt("IsEndlessMode", 0) == 1);
        baseForwardSpeed = forwardSpeed;

        // 裝備已購買解鎖之球體皮膚與拖尾光效
        ApplyEquippedSkin();
    }

    /// <summary>
    /// 即時套用玩家在商店中選擇的球體皮膚材質與霓虹光效
    /// </summary>
    public void ApplyEquippedSkin()
    {
        int skinIndex = PlayerPrefs.GetInt("EquippedBallSkin", 0);
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) return;

        Material mat = new Material(Shader.Find("Standard"));
        Color themeColor = new Color(0f, 0.75f, 1f);

        switch (skinIndex)
        {
            case 0: // 經典電藍 (Classic Blue)
                themeColor = new Color(0f, 0.75f, 1f);
                mat.color = themeColor;
                mat.SetColor("_EmissionColor", new Color(0f, 0.35f, 0.8f));
                mat.SetFloat("_Metallic", 0.3f);
                mat.SetFloat("_Glossiness", 0.9f);
                break;
            case 1: // 霓虹烈焰 (Neon Flame)
                themeColor = new Color(1f, 0.22f, 0.05f);
                mat.color = themeColor;
                mat.SetColor("_EmissionColor", new Color(1.2f, 0.25f, 0f));
                mat.SetFloat("_Metallic", 0.5f);
                mat.SetFloat("_Glossiness", 0.95f);
                break;
            case 2: // 賽博矩陣 (Cyber Matrix)
                themeColor = new Color(0.05f, 1f, 0.3f);
                mat.color = themeColor;
                mat.SetColor("_EmissionColor", new Color(0.1f, 1.2f, 0.35f));
                mat.SetFloat("_Metallic", 0.6f);
                mat.SetFloat("_Glossiness", 0.85f);
                break;
            case 3: // 黃金至尊 (Golden Spark)
                themeColor = new Color(1f, 0.85f, 0.15f);
                mat.color = themeColor;
                mat.SetColor("_EmissionColor", new Color(1.4f, 1.0f, 0.2f));
                mat.SetFloat("_Metallic", 0.95f);
                mat.SetFloat("_Glossiness", 0.98f);
                break;
            case 4: // 銀河虛空 (Void Galaxy)
                themeColor = new Color(0.65f, 0.1f, 1f);
                mat.color = themeColor;
                mat.SetColor("_EmissionColor", new Color(0.85f, 0.15f, 1.4f));
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Glossiness", 0.95f);
                break;
        }

        mat.EnableKeyword("_EMISSION");
        mr.material = mat;

        // 綁定動態霓虹軌跡拖尾 (TrailRenderer)
        TrailRenderer tr = GetComponent<TrailRenderer>();
        if (tr == null) tr = gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.35f;
        tr.startWidth = 0.6f;
        tr.endWidth = 0.05f;
        tr.material = mat;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(themeColor, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        tr.colorGradient = grad;
    }

    void Update()
    {
        if (isDead) return;

        CheckGrounded();
        ReadInputs();

        // 無盡模式下隨著前進里程逐步提升速度
        if (isEndlessMode)
        {
            forwardSpeed = baseForwardSpeed + Mathf.Min(10.0f, (transform.position.z / 100f) * 1.2f);
        }

        // 掉落深淵判定
        if (transform.position.y < -5.0f)
        {
            Die("墜落虛空深淵！");
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // 計算水平速度 (結合鍵盤與 MPU-6050 陀螺儀)
        float targetVx = currentHorizontalInput * horizontalSpeed;

        // 保持穩定的向前速度與自由的垂直重力 (Unity 6 linearVelocity)
        Vector3 vel = rb.linearVelocity;
        vel.x = targetVx;
        vel.z = forwardSpeed;
        rb.linearVelocity = vel;
    }

    void ReadInputs()
    {
        float keyboardInput = 0f;
        if (Input.GetKey(customKeyLeft) || Input.GetKey(KeyCode.LeftArrow)) keyboardInput -= 1f;
        if (Input.GetKey(customKeyRight) || Input.GetKey(KeyCode.RightArrow)) keyboardInput += 1f;

        float tiltInput = 0f;
        if (arduino != null)
        {
            // MPU-6050 X軸加速度代表左右傾角
            float rawTilt = arduino.acceleration.x;
            if (Mathf.Abs(rawTilt) > tiltDeadzone)
            {
                tiltInput = Mathf.Clamp(rawTilt * tiltSensitivity, -1f, 1f);
            }
        }

        // 雙軌融合：硬體有數值時疊加，或鍵盤優先
        if (Mathf.Abs(keyboardInput) > 0.01f)
        {
            currentHorizontalInput = keyboardInput;
        }
        else
        {
            currentHorizontalInput = tiltInput;
        }
    }

    void CheckGrounded()
    {
        // 向下射線檢測是否接觸賽道地面
        RaycastHit hit;
        Vector3 rayStart = transform.position;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    public void Jump()
    {
        if (isDead) return;
        if (!isGrounded) return; // 空中不可二段跳

        Vector3 vel = rb.linearVelocity;
        vel.y = jumpForce;
        rb.linearVelocity = vel;
        isGrounded = false;

        AudioManager.Instance?.PlayJump();
    }

    public void Launch(float launchForce, float forwardBoost)
    {
        if (isDead) return;

        Vector3 vel = rb.linearVelocity;
        vel.y = launchForce;
        vel.z = forwardSpeed * forwardBoost;
        rb.linearVelocity = vel;
        isGrounded = false;
        AudioManager.Instance?.PlayJump();
    }

    public void Die(string reason)
    {
        if (isDead) return;
        isDead = true;

        // 凍結物理並觸發結算
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        AudioManager.Instance?.PlayFall();

        RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
        if (gm != null)
        {
            gm.TriggerGameOver(reason);
        }
    }

    public bool CanJump() => !isDead && isGrounded;
    public bool IsGrounded => isGrounded;
    public bool IsDead => isDead;
}
