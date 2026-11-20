using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全遊戲統一的面板轉場 (全部使用 unscaledDeltaTime，暫停 timeScale=0 時照樣播放)：
///   UIPanelAnimator.Show(panel)  → 啟用面板並淡入 + 由大縮到正常 + 子元件依序 (stagger) 浮現
///   UIPanelAnimator.Hide(panel)  → 立即 SetActive(false)，但在原位留下一個「殘影」複本淡出縮小後自動銷毀
/// Hide 採殘影設計，因此遊戲邏輯 (activeSelf / activeInHierarchy 判斷、導航分派) 與原本完全相同，
/// 不會因為「關閉動畫還沒播完」而讓輸入跑到舊面板。
/// 動畫只改 CanvasGroup.alpha 與面板根節點 localScale，不動子按鈕的位置/縮放，與選單的 Accordion 動效不衝突。
/// </summary>
public class UIPanelAnimator : MonoBehaviour
{
    public const float OpenTime = 0.30f;
    public const float CloseTime = 0.18f;
    const float StaggerStep = 0.025f;
    const float StaggerMax = 0.20f;
    const float ChildFade = 0.20f;

    CanvasGroup cg;
    RectTransform rt;
    Coroutine routine;
    readonly List<CanvasGroup> kids = new List<CanvasGroup>();

    // ---------------- 對外 API ----------------

    /// <summary>啟用並以轉場顯示面板。scaleFrom &gt;1 表示「由近處縮入」(前進)，&lt;1 表示「由遠處放大」(返回)。</summary>
    public static void Show(GameObject go, float scaleFrom = 1.04f, bool stagger = true, float duration = OpenTime)
    {
        if (go == null) return;
        go.SetActive(true);
        UIPanelAnimator a = Obtain(go);
        a.PlayOpen(scaleFrom, stagger, duration);
    }

    /// <summary>關閉面板：原物件立即停用，並留下淡出殘影。</summary>
    public static void Hide(GameObject go, float scaleTo = 0.96f, bool animate = true)
    {
        if (go == null) return;
        if (animate && go.activeInHierarchy && go.transform.parent != null)
        {
            GameObject ghost = Instantiate(go, go.transform.parent);
            ghost.name = "UIGhost";
            ghost.transform.SetSiblingIndex(go.transform.GetSiblingIndex());
            ghost.SetActive(true);
            foreach (Selectable s in ghost.GetComponentsInChildren<Selectable>(true)) s.interactable = false;
            UIPanelAnimator a = Obtain(ghost);
            a.Init();
            a.cg.interactable = false;
            a.cg.blocksRaycasts = false;
            a.routine = a.StartCoroutine(a.GhostRoutine(scaleTo));
        }
        go.SetActive(false);
    }

    /// <summary>設定面板 / 分頁等「同層切換」：只做快速淡入，不縮放、不 stagger。</summary>
    public static void SwapIn(GameObject go)
    {
        if (go == null || go.activeSelf) return;
        Show(go, 1f, false, 0.16f);
    }

    static UIPanelAnimator Obtain(GameObject go)
    {
        UIPanelAnimator a = go.GetComponent<UIPanelAnimator>();
        if (a == null) a = go.AddComponent<UIPanelAnimator>();
        return a;
    }

    // ---------------- 內部實作 ----------------

    void Init()
    {
        if (rt == null) rt = transform as RectTransform;
        if (cg == null)
        {
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        }
    }

    static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }

    void PlayOpen(float scaleFrom, bool stagger, float duration)
    {
        Init();
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(OpenRoutine(scaleFrom, stagger, duration));
    }

    IEnumerator OpenRoutine(float scaleFrom, bool stagger, float duration)
    {
        cg.alpha = 0f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        if (rt != null) rt.localScale = new Vector3(scaleFrom, scaleFrom, 1f);

        kids.Clear();
        if (stagger)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "SelectionGlow") continue;
                CanvasGroup k = c.GetComponent<CanvasGroup>();
                if (k == null) k = c.gameObject.AddComponent<CanvasGroup>();
                k.alpha = 0f;
                kids.Add(k);
            }
        }

        float total = duration;
        if (kids.Count > 0) total = Mathf.Max(duration, Mathf.Min(StaggerStep * (kids.Count - 1), StaggerMax) + ChildFade);

        float t = 0f;
        while (t < total)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float e = EaseOut(t / duration);
            cg.alpha = e;
            if (rt != null)
            {
                float s = Mathf.Lerp(scaleFrom, 1f, e);
                rt.localScale = new Vector3(s, s, 1f);
            }
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i] == null) continue;
                float delay = Mathf.Min(StaggerStep * i, StaggerMax);
                kids[i].alpha = EaseOut((t - delay) / ChildFade);
            }
            yield return null;
        }
        Finish();
    }

    IEnumerator GhostRoutine(float scaleTo)
    {
        float a0 = cg.alpha;
        Vector3 s0 = rt != null ? rt.localScale : Vector3.one;
        float t = 0f;
        while (t < CloseTime)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float e = EaseOut(t / CloseTime);
            cg.alpha = a0 * (1f - e);
            if (rt != null) rt.localScale = Vector3.Lerp(s0, new Vector3(scaleTo, scaleTo, 1f), e);
            yield return null;
        }
        Destroy(gameObject);
    }

    void Finish()
    {
        if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
        if (rt != null) rt.localScale = Vector3.one;
        for (int i = 0; i < kids.Count; i++) if (kids[i] != null) kids[i].alpha = 1f;
        routine = null;
    }

    void OnDisable()
    {
        // 轉場途中被其他程式直接 SetActive(false)：還原成乾淨狀態，下次啟用不會殘留半透明
        if (gameObject.name == "UIGhost") return;
        if (routine != null) { StopCoroutine(routine); Finish(); }
    }
}
