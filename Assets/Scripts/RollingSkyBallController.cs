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
    }

    void Update()
    {
        if (isDead) return;

        CheckGrounded();
        ReadInputs();

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

        // 保持穩定的向前速度與自由的垂直重力
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
        // 向下球形射線檢測
        isGrounded = Physics.SphereCast(transform.position, 0.45f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer);
    }

    public bool CanJump()
    {
        return !isDead && isGrounded;
    }

    public void Jump()
    {
        if (!CanJump()) return;

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
        Debug.Log("[Ball] 彈射跳板發動！凌空飛躍！");
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

    public bool IsDead => isDead;
}
