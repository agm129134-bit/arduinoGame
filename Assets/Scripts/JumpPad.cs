using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Settings")]
    public float launchForce = 16.0f;
    public float forwardBoost = 2.0f;
    public Color activeColor = new Color(0f, 1f, 0.9f);

    private float lastLaunchTime = -999f;

    void Awake()
    {
        // 確保 Collider 為觸發器，並擴展為 3.6m 高立體垂直感應柱 (0.12 * 30 = 3.6m)
        // 徹底解決小球因為地磚微幅浮空或小跳而從跳台上方掠過漏觸發的問題
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = true;
            bc.size = new Vector3(1.0f, 30.0f, 1.0f);
            bc.center = new Vector3(0, 14.5f, 0);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerLaunch(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerLaunch(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TriggerLaunch(collision.gameObject);
    }

    private void TriggerLaunch(GameObject target)
    {
        if (Time.time - lastLaunchTime < 0.2f) return;

        RollingSkyBallController ball = target.GetComponent<RollingSkyBallController>() ?? target.GetComponentInParent<RollingSkyBallController>();
        if (ball != null)
        {
            lastLaunchTime = Time.time;
            ball.Launch(launchForce, forwardBoost);
        }
    }
}
