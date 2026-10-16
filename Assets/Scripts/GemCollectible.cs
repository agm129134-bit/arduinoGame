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
    private Transform haloTransform;
    private Light glowLight;

    void Awake()
    {
        // 升級視覺模型：以程式碼動態生成立體多面體璀璨水晶 (Faceted Crystal / Octahedron Diamond)
        EnsureCrystalMesh();
        EnsureRendererAndMaterial();
        EnsureSparkleLight();
        EnsureHaloRing();
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

        // 光環反向旋轉，營造超凡能量場
        if (haloTransform != null)
        {
            haloTransform.Rotate(Vector3.up * -160f * Time.deltaTime, Space.Self);
        }

        // 呼吸式動態高亮脈衝光暈 (使寶石在遠處亦格外醒目，高亮穿透地磚)
        if (glowLight != null)
        {
            glowLight.intensity = 2.8f + Mathf.PingPong(Time.time * 4.0f, 1.5f);
        }
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

        // 6個原始頂點：上尖端、下尖端、赤道四頂點 (加大尺寸至 0.85m 高，強化視覺辨識)
        Vector3 top = new Vector3(0, 0.85f, 0);
        Vector3 bottom = new Vector3(0, -0.85f, 0);
        Vector3 vNorth = new Vector3(0, 0, 0.62f);
        Vector3 vEast = new Vector3(0.62f, 0, 0);
        Vector3 vSouth = new Vector3(0, 0, -0.62f);
        Vector3 vWest = new Vector3(-0.62f, 0, 0);

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
            sc.radius = 0.7f;
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
    /// 自動為寶石物件掛載渲染器與極高對比燦爛金黃 (Radiant Golden Topaz) HDR 超強發光材質，杜絕與青藍地磚混淆
    /// </summary>
    void EnsureRendererAndMaterial()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

        Material mat = mr.material;
        if (mat == null || mat.name.Contains("Default") || !mat.name.Contains("RadiantGold"))
        {
            mat = new Material(Shader.Find("Standard"));
            mat.name = "Mat_RadiantGold";
        }

        // 燦爛金燦核心，在深藍與青藍賽道上形成最強色彩反差
        mat.color = new Color(1.0f, 0.88f, 0.12f, 1.0f);
        mat.SetColor("_EmissionColor", new Color(3.2f, 2.5f, 0.4f)); // 超高亮 HDR 閃耀
        mat.EnableKeyword("_EMISSION");
        mat.SetFloat("_Metallic", 0.7f);
        mat.SetFloat("_Glossiness", 0.98f);
        mr.material = mat;
    }

    /// <summary>
    /// 自動添加寶石周圍的水晶發光微粒 / 點光源 (高亮金黃光暈)
    /// </summary>
    void EnsureSparkleLight()
    {
        glowLight = GetComponentInChildren<Light>();
        if (glowLight == null)
        {
            GameObject lObj = new GameObject("GemGlowLight");
            lObj.transform.SetParent(transform, false);
            glowLight = lObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
        }
        glowLight.color = new Color(1.0f, 0.85f, 0.25f); // 燦爛高亮金黃
        glowLight.range = 6.0f;
        glowLight.intensity = 3.5f;
    }

    /// <summary>
    /// 自動生成環繞寶石旋轉的星環光帶 (Energy Halo Ring)，進一步強化遠距識別度
    /// </summary>
    void EnsureHaloRing()
    {
        Transform existing = transform.Find("GemHaloRing");
        if (existing != null)
        {
            haloTransform = existing;
            return;
        }

        GameObject ringObj = new GameObject("GemHaloRing");
        ringObj.transform.SetParent(transform, false);
        ringObj.transform.localPosition = Vector3.zero;
        ringObj.transform.localRotation = Quaternion.Euler(35f, 0f, 25f);
        ringObj.transform.localScale = Vector3.one * 1.1f;

        MeshFilter mf = ringObj.AddComponent<MeshFilter>();
        MeshRenderer mr = ringObj.AddComponent<MeshRenderer>();

        // 動態建構 16 段平滑環狀多邊形網格
        Mesh ringMesh = new Mesh();
        int segments = 16;
        Vector3[] verts = new Vector3[segments * 2];
        int[] tris = new int[segments * 6];
        float innerR = 0.85f;
        float outerR = 1.15f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            verts[i * 2] = new Vector3(cos * innerR, 0, sin * innerR);
            verts[i * 2 + 1] = new Vector3(cos * outerR, 0, sin * outerR);

            int next = (i + 1) % segments;
            int tIdx = i * 6;

            tris[tIdx] = i * 2;
            tris[tIdx + 1] = i * 2 + 1;
            tris[tIdx + 2] = next * 2;

            tris[tIdx + 3] = next * 2;
            tris[tIdx + 4] = i * 2 + 1;
            tris[tIdx + 5] = next * 2 + 1;
        }

        ringMesh.vertices = verts;
        ringMesh.triangles = tris;
        ringMesh.RecalculateNormals();
        mf.sharedMesh = ringMesh;

        Material haloMat = new Material(Shader.Find("Standard"));
        haloMat.color = new Color(1.0f, 0.95f, 0.35f, 0.9f);
        haloMat.SetColor("_EmissionColor", new Color(3.5f, 2.8f, 0.5f));
        haloMat.EnableKeyword("_EMISSION");
        mr.material = haloMat;

        haloTransform = ringObj.transform;
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
