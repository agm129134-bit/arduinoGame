using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GemCollectible : MonoBehaviour
{
    [Header("Visual Effects")]
    public float rotateSpeed = 120.0f;
    public float floatAmplitude = 0.22f;
    public float floatFrequency = 2.4f;

    private Vector3 initialPos;
    private bool isCollected = false;

    void Awake()
    {
        // 升級視覺模型：以程式碼動態生成立體多面體璀璨水晶 (Faceted Crystal / Octahedron Diamond)
        EnsureCrystalMesh();
        EnsureSparkleLight();
    }

    void Start()
    {
        initialPos = transform.position;
    }

    void Update()
    {
        // 多軸立體旋轉與上下懸浮動效
        transform.Rotate(new Vector3(25f, rotateSpeed, 15f) * Time.deltaTime, Space.World);
        float newY = initialPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(initialPos.x, newY, initialPos.z);
    }

    /// <summary>
    /// 自動為寶石物件替換為多面立體鑽石網格 (Octahedron Diamond Mesh)
    /// </summary>
    void EnsureCrystalMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();

        // 建立雙尖八面體璀璨鑽石幾何
        Mesh mesh = new Mesh();
        mesh.name = "CrystalDiamondMesh";

        // 6個原始頂點：上尖端、下尖端、赤道四頂點
        Vector3 top = new Vector3(0, 0.65f, 0);
        Vector3 bottom = new Vector3(0, -0.65f, 0);
        Vector3 vNorth = new Vector3(0, 0, 0.48f);
        Vector3 vEast = new Vector3(0.48f, 0, 0);
        Vector3 vSouth = new Vector3(0, 0, -0.48f);
        Vector3 vWest = new Vector3(-0.48f, 0, 0);

        // 為了呈現寶石折射立體切面感 (Flat Shading Facets)，每個三角形獨立頂點
        Vector3[] vertices = new Vector3[24];
        int[] triangles = new int[24];

        // 上半部 4 個切面
        AddFace(vertices, triangles, 0, top, vNorth, vEast);
        AddFace(vertices, triangles, 1, top, vEast, vSouth);
        AddFace(vertices, triangles, 2, top, vSouth, vWest);
        AddFace(vertices, triangles, 3, top, vWest, vNorth);

        // 下半部 4 個切面
        AddFace(vertices, triangles, 4, bottom, vEast, vNorth);
        AddFace(vertices, triangles, 5, bottom, vSouth, vEast);
        AddFace(vertices, triangles, 6, bottom, vWest, vSouth);
        AddFace(vertices, triangles, 7, bottom, vNorth, vWest);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;

        // 調整碰撞盒為適配鑽石大小
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null)
        {
            sc.radius = 0.55f;
            sc.isTrigger = true;
        }
    }

    void AddFace(Vector3[] verts, int[] tris, int faceIndex, Vector3 a, Vector3 b, Vector3 c)
    {
        int vIdx = faceIndex * 3;
        verts[vIdx] = a;
        verts[vIdx + 1] = b;
        verts[vIdx + 2] = c;
        tris[vIdx] = vIdx;
        tris[vIdx + 1] = vIdx + 1;
        tris[vIdx + 2] = vIdx + 2;
    }

    /// <summary>
    /// 自動添加寶石周圍的水晶發光微粒 / 點光源
    /// </summary>
    void EnsureSparkleLight()
    {
        Light pLight = GetComponentInChildren<Light>();
        if (pLight == null)
        {
            GameObject lObj = new GameObject("GemGlowLight");
            lObj.transform.SetParent(transform, false);
            pLight = lObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(0.2f, 0.95f, 1f); // 璀璨青空藍
            pLight.range = 3.5f;
            pLight.intensity = 1.8f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        RollingSkyBallController ball = other.GetComponent<RollingSkyBallController>();
        if (ball != null)
        {
            isCollected = true;
            AudioManager.Instance?.PlayGem();

            // 關卡內寶石計數增加
            RollingSkyGameManager gm = FindAnyObjectByType<RollingSkyGameManager>();
            if (gm != null)
            {
                gm.AddGem();
            }

            // 全域玩家存檔累計寶石（供皮膚商店購買解鎖）
            int totalGems = PlayerPrefs.GetInt("TotalGems", 0) + 1;
            PlayerPrefs.SetInt("TotalGems", totalGems);
            PlayerPrefs.Save();

            // 隱藏並銷毀物件
            gameObject.SetActive(false);
            Destroy(gameObject, 0.5f);
        }
    }
}
