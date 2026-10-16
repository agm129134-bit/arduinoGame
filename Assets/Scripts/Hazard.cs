using UnityEngine;

public class Hazard : MonoBehaviour
{
    [Header("Hazard Settings")]
    public string hazardName = "尖刺障礙";
    public bool isInstantDeath = true;

    private void OnTriggerEnter(Collider other)
    {
        CheckHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckHit(collision.gameObject);
    }

    void CheckHit(GameObject obj)
    {
        RollingSkyBallController ball = obj.GetComponent<RollingSkyBallController>();
        if (ball != null)
        {
            ball.Die($"撞擊到 {hazardName}！");
        }
    }
}
