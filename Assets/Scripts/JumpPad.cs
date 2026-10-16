using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Settings")]
    public float launchForce = 16.0f;
    public float forwardBoost = 2.0f;
    public Color activeColor = new Color(0f, 1f, 0.9f);

    void Awake()
    {
        // 確保 Collider 為觸發器，若無 Collider 則自動補全
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerLaunch(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TriggerLaunch(collision.gameObject);
    }

    private void TriggerLaunch(GameObject target)
    {
        RollingSkyBallController ball = target.GetComponent<RollingSkyBallController>();
        if (ball != null)
        {
            ball.Launch(launchForce, forwardBoost);
        }
    }
}
