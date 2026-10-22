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

    [Header("Ground & Jump Polish")]
    public LayerMask groundLayer = ~0;
    public float groundCheckDistance = 0.65f;
    public float sphereCastRadius = 0.42f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;

    private Rigidbody rb;
    private bool isGrounded = true;
    private bool isDead = false;
    private float currentHorizontalInput = 0f;
    private bool isEndlessMode = false;
    private bool isSkyTowerMode = false;
    private float baseForwardSpeed = 12.0f;
    private float currentForwardInput = 0f;
    private float lastUltrasonicDist = 50f;
    private float ultrasonicJumpCooldown = 0f;
    private Vector3 skyTowerSpawnPos = new Vector3(0, 1.5f, -4f);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 透過代碼平滑旋轉視覺，防止物理碰撞翻滾卡軌

        // 動態賦予零反彈無摩擦物理材質，徹底杜絕著陸碰撞反彈與跳躍殘留
        PhysicsMaterial zeroBounceMat = new PhysicsMaterial("BallZeroBounce");
        zeroBounceMat.bounciness = 0f;
        zeroBounceMat.bounceCombine = PhysicsMaterialCombine.Minimum;
        zeroBounceMat.dynamicFriction = 0.05f;
        zeroBounceMat.staticFriction = 0.05f;
        zeroBounceMat.frictionCombine = PhysicsMaterialCombine.Minimum;

        Collider col = GetComponent<Collider>();
        if (col != null) col.sharedMaterial = zeroBounceMat;
    }

    private KeyCode customKeyLeft = KeyCode.A;
    private KeyCode customKeyRight = KeyCode.D;
    private KeyCode customKeyJump = KeyCode.Space;

    void Start()
    {
        if (arduino == null) arduino = FindAnyObjectByType<ArduinoReceiver>();

        // 載入玩家自訂鍵盤按鍵與靈敏度
        ReloadSettings();

        isEndlessMode = (PlayerPrefs.GetInt("IsEndlessMode", 0) == 1);
        isSkyTowerMode = (PlayerPrefs.GetInt("IsSkyTowerMode", 0) == 1);
        baseForwardSpeed = forwardSpeed;

        if (isSkyTowerMode)
        {
            transform.position = skyTowerSpawnPos;
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }

        // 裝備已購買解鎖之球體皮膚與拖尾光效
        ApplyEquippedSkin();
    }

    /// <summary>
    /// 即時重新載入最新鍵位與靈敏度 (供暫停設定面板修改後立即生效)
    /// </summary>
    public void ReloadSettings()
    {
        customKeyLeft = (KeyCode)PlayerPrefs.GetInt("Key_Left", (int)KeyCode.A);
        customKeyRight = (KeyCode)PlayerPrefs.GetInt("Key_Right", (int)KeyCode.D);
        customKeyJump = (KeyCode)PlayerPrefs.GetInt("Key_Jump", (int)KeyCode.Space);
        horizontalSpeed = PlayerPrefs.GetFloat("Keyboard_Speed", 9.0f);
        tiltSensitivity = PlayerPrefs.GetFloat("Tilt_Sensitivity", 1.8f);
        tiltDeadzone = PlayerPrefs.GetFloat("Tilt_Deadzone", 0.08f);
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

        // 倒數土狼時間與跳躍緩衝
        if (coyoteTimer > 0f) coyoteTimer -= Time.deltaTime;
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;

        // 若玩家按下 Space 或自訂跳躍鍵，記錄進跳躍緩衝 (150ms 窗口)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(customKeyJump))
        {
            jumpBufferTimer = jumpBufferTime;
        }

        // 超音波手勢跳躍：手持實體裝置朝電腦螢幕往前推進跳躍
        if (arduino != null)
        {
            if (ultrasonicJumpCooldown > 0f) ultrasonicJumpCooldown -= Time.deltaTime;

            float dist = arduino.distance;
            float jumpThresh = PlayerPrefs.GetFloat("Jump_Distance", 20.0f);
            float delta = dist - lastUltrasonicDist;

            // 推進手勢檢測：距離快速縮小 (delta < -8) 或達到門檻 (dist < jumpThresh)
            if ((delta < -8.0f || (dist <= jumpThresh && lastUltrasonicDist > jumpThresh + 1.5f)) && ultrasonicJumpCooldown <= 0f)
            {
                jumpBufferTimer = jumpBufferTime;
                ultrasonicJumpCooldown = 0.4f;
            }
            lastUltrasonicDist = Mathf.Lerp(lastUltrasonicDist, dist, Time.deltaTime * 10f);
        }

        // 當處於跳躍緩衝期且具備起跳條件時，立即起跳！
        if (jumpBufferTimer > 0f && CanJump())
        {
            Jump();
        }

        // 無盡模式下隨著前進里程逐步提升速度
        if (isEndlessMode)
        {
            forwardSpeed = baseForwardSpeed + Mathf.Min(10.0f, (transform.position.z / 100f) * 1.2f);
        }

        // 掉落深淵判定 (天梯模式採硬派墜落重爬，常規模式死亡)
        if (isSkyTowerMode)
        {
            if (transform.position.y < -15.0f)
            {
                // 硬派 Only Up：跌入最底層虛空，傳送回 0 米起點重新攀登！
                transform.position = skyTowerSpawnPos;
                if (rb != null) rb.linearVelocity = Vector3.zero;
                AudioManager.Instance?.PlayFall();
            }
        }
        else
        {
            if (transform.position.y < -5.0f)
            {
                Die("墜落虛空深淵！");
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (isSkyTowerMode)
        {
            // 通天天梯：依相機視角水平朝向前後左右移動 (玩家視角轉向哪裡，前推就往哪裡跑)
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (Camera.main != null)
            {
                camForward = Camera.main.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();

                camRight = Camera.main.transform.right;
                camRight.y = 0f;
                if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();
            }

            Vector3 moveDir = (camForward * currentForwardInput + camRight * currentHorizontalInput);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            Vector3 targetVel = moveDir * horizontalSpeed;

            Vector3 vel = rb.linearVelocity;
            vel.x = Mathf.MoveTowards(vel.x, targetVel.x, 35f * Time.fixedDeltaTime);
            vel.z = Mathf.MoveTowards(vel.z, targetVel.z, 35f * Time.fixedDeltaTime);
            rb.linearVelocity = vel;
        }
        else
        {
            // 常規跑酷：計算水平速度與固定向前速度
            float targetVx = currentHorizontalInput * horizontalSpeed;

            Vector3 vel = rb.linearVelocity;
            vel.x = targetVx;
            vel.z = forwardSpeed;
            rb.linearVelocity = vel;
        }
    }

    void ReadInputs()
    {
        float keyboardInput = 0f;
        if (Input.GetKey(customKeyLeft) || Input.GetKey(KeyCode.LeftArrow)) keyboardInput -= 1f;
        if (Input.GetKey(customKeyRight) || Input.GetKey(KeyCode.RightArrow)) keyboardInput += 1f;

        float forwardKb = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forwardKb += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forwardKb -= 1f;

        float tiltInput = 0f;
        float pitchInput = 0f;
        if (arduino != null)
        {
            // MPU-6050 X軸加速度代表左右傾角
            float rawTilt = arduino.acceleration.x;
            if (Mathf.Abs(rawTilt) > tiltDeadzone)
            {
                tiltInput = Mathf.Clamp(rawTilt * tiltSensitivity, -1f, 1f);
            }

            // MPU-6050 Y軸加速度代表前後傾角 (前傾往前跑，後傾往後跑)
            float rawPitch = arduino.acceleration.y;
            if (Mathf.Abs(rawPitch) > tiltDeadzone)
            {
                pitchInput = Mathf.Clamp(rawPitch * tiltSensitivity, -1f, 1f);
            }
        }

        // 雙軌融合：鍵盤或硬體體感
        currentHorizontalInput = (Mathf.Abs(keyboardInput) > 0.01f) ? keyboardInput : tiltInput;
        currentForwardInput = (Mathf.Abs(forwardKb) > 0.01f) ? forwardKb : pitchInput;
    }

    void CheckGrounded()
    {
        Vector3 origin = transform.position;
        RaycastHit hit;

        // 1. 球體向下投射 (SphereCast) 檢測：半徑 0.42m，向下偵測 0.25m
        // 徹底免疫 8cm 地磚拼縫漏檢
        bool grounded = Physics.SphereCast(origin, sphereCastRadius, Vector3.down, out hit, 0.25f, groundLayer, QueryTriggerInteraction.Ignore);

        // 2. 備援向下三點射線檢測 (中心、左偏、右偏)
        if (!grounded)
        {
            grounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore) ||
                       Physics.Raycast(origin + Vector3.left * 0.35f, Vector3.down, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore) ||
                       Physics.Raycast(origin + Vector3.right * 0.35f, Vector3.down, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
        }

        isGrounded = grounded;
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // 碰撞接面法線向上檢測 (物理層面雙重保證)
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.4f)
            {
                isGrounded = true;
                coyoteTimer = coyoteTime;
                break;
            }
        }
    }

    public void Jump()
    {
        if (isDead) return;
        if (!CanJump()) return;

        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        isGrounded = false;

        Vector3 vel = rb.linearVelocity;
        vel.y = jumpForce;
        rb.linearVelocity = vel;

        AudioManager.Instance?.PlayJump();
    }

    public void Launch(float launchForce, float forwardBoost)
    {
        if (isDead) return;

        Vector3 vel = rb.linearVelocity;
        vel.y = launchForce;
        if (!isSkyTowerMode)
        {
            vel.z = forwardSpeed * forwardBoost;
        }
        else
        {
            if (Mathf.Abs(currentForwardInput) > 0.1f) vel.z = currentForwardInput * forwardSpeed * 1.2f;
        }
        rb.linearVelocity = vel;
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
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

    public bool CanJump() => !isDead && (isGrounded || coyoteTimer > 0f);
    public bool IsGrounded => isGrounded || coyoteTimer > 0f;
    public bool IsDead => isDead;
}
