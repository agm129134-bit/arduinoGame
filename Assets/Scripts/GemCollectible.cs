using UnityEngine;

public class GemCollectible : MonoBehaviour
{
    [Header("Visual Effects")]
    public float rotateSpeed = 90.0f;
    public float floatAmplitude = 0.25f;
    public float floatFrequency = 2.0f;

    private Vector3 initialPos;
    private bool isCollected = false;

    void Start()
    {
        initialPos = transform.position;
    }

    void Update()
    {
        // 旋轉與上下漂浮
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float newY = initialPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(initialPos.x, newY, initialPos.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        RollingSkyBallController ball = other.GetComponent<RollingSkyBallController>();
        if (ball != null)
        {
            isCollected = true;
            AudioManager.Instance?.PlayGem();
            RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
            if (gm != null)
            {
                gm.AddGem();
            }

            // 隱藏與銷毀
            gameObject.SetActive(false);
            Destroy(gameObject, 0.5f);
        }
    }
}
