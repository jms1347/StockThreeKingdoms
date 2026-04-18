using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 성문 앞 금화 더미 시각화, 흔들기 수거, DOTween 비행 후 지연 입금.
/// 시장·농장 누적은 모두 금화로 합산되어 표시됩니다.
/// </summary>
public class CollectionManager : MonoBehaviour
{
    const float ShakeSqrThreshold = 2.5f;
    const float ShakeCooldownSec = 0.65f;

    [Header("참조")]
    public HomeController homeController;
    [Tooltip("비행 아이콘을 올릴 캔버스 루트 (보통 Screen Space Canvas)")]
    public RectTransform flyIconsRoot;
    [Tooltip("금화 텍스트/아이콘의 RectTransform (도착 지점)")]
    public RectTransform goldFlyTarget;
    [Tooltip("더미가 배치된 영역 (비행 시작 위치 월드→스크린 변환 기준)")]
    public RectTransform pileArea;

    [Header("더미 아이콘 (각 8개, 인덱스 0부터 순서대로 켜짐)")]
    public GameObject[] goldPiles = new GameObject[8];

    [Header("비행 아이콘 풀")]
    [Tooltip("풀에서 Instantiate할 금화 비행 아이콘 원본 (씬 오브젝트 또는 프리팹)")]
    public GameObject flyingGoldPrefab;
    [Range(10, 32)]
    public int poolSize = 12;

    public readonly Queue<GameObject> goldIconPool = new Queue<GameObject>();

    [Header("비행 연출")]
    public float flyDuration = 0.85f;
    public float bezierArc = 180f;
    [Range(0.02f, 0.3f)] public float flyStagger = 0.05f;
    [Tooltip("풀 인스턴스 기본 크기 (프리팹에 RectTransform이 있으면 생략 가능)")]
    public Vector2 flyIconSize = new Vector2(48f, 48f);

    float _shakeCooldownUntil;
    int _pendingFlyTweens;
    bool _poolsWarmed;
    bool _hidePilesWhileFlying;

    GameObject _runtimeGoldProto;

    void Awake()
    {
        if (homeController == null)
            homeController = GetComponent<HomeController>() ?? GetComponentInParent<HomeController>();
    }

    void Start()
    {
        WarmFlyPoolsIfPossible();
        TryResolveFlyTargetsFromGlobalUI();
    }

    void OnDestroy()
    {
        DOTween.Kill(this, true);
        PurgePoolTweens(goldIconPool);
    }

    static void PurgePoolTweens(Queue<GameObject> pool)
    {
        foreach (var go in pool)
        {
            if (go == null) continue;
            DOTween.Kill(go.transform, true);
        }
    }

    public void WarmFlyPoolsIfPossible()
    {
        if (_poolsWarmed || flyIconsRoot == null) return;

        EnsureRuntimePrototypeIfNeeded();
        PrewarmPool();
        _poolsWarmed = true;
    }

    void PrewarmPool()
    {
        var proto = GetPrototype();
        if (proto == null) return;

        int need = Mathf.Max(0, poolSize - goldIconPool.Count);
        for (int i = 0; i < need; i++)
        {
            var inst = Instantiate(proto, flyIconsRoot, false);
            inst.name = $"FlyGold_Pool_{i}";
            inst.SetActive(false);
            goldIconPool.Enqueue(inst);
        }
    }

    GameObject GetPrototype()
    {
        if (flyingGoldPrefab != null) return flyingGoldPrefab;
        return _runtimeGoldProto;
    }

    void EnsureRuntimePrototypeIfNeeded()
    {
        if (flyingGoldPrefab != null || _runtimeGoldProto != null) return;
        _runtimeGoldProto = BuildDefaultFlyPrototype();
    }

    GameObject BuildDefaultFlyPrototype()
    {
        var go = new GameObject("FlyGold_RuntimeProto",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(flyIconsRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = flyIconSize;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 0.85f, 0.2f);
        img.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    GameObject RentFlyIcon()
    {
        GameObject go = goldIconPool.Count > 0 ? goldIconPool.Dequeue() : null;
        if (go == null)
        {
            var proto = GetPrototype();
            if (proto == null)
            {
                EnsureRuntimePrototypeIfNeeded();
                proto = GetPrototype();
            }

            if (proto == null) return null;
            go = Instantiate(proto, flyIconsRoot, false);
            go.name = "FlyGold_Expanded";
        }

        DOTween.Kill(go.transform, true);
        go.transform.SetParent(flyIconsRoot, false);
        return go;
    }

    void ReturnFlyIcon(GameObject go)
    {
        if (go == null) return;
        DOTween.Kill(go.transform, true);
        DOTween.Kill(go, true);
        go.SetActive(false);
        go.transform.SetParent(flyIconsRoot, false);
        goldIconPool.Enqueue(go);
    }

    static void ResetPooledRectTransform(RectTransform rt, Vector2 anchoredStart, Vector2 iconSize)
    {
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = iconSize;
        rt.anchoredPosition = anchoredStart;
    }

    void Update()
    {
        UpdatePileVisuals();
        TryResolveFlyTargetsFromGlobalUI();

        if (Time.unscaledTime < _shakeCooldownUntil) return;
        if (homeController == null) return;

        Vector3 acc = Input.acceleration;
        if (acc.sqrMagnitude > ShakeSqrThreshold)
        {
            if (homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: false))
                _shakeCooldownUntil = Time.unscaledTime + ShakeCooldownSec;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (Time.unscaledTime < _shakeCooldownUntil) return;
            if (homeController != null &&
                homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: false))
            {
                _shakeCooldownUntil = Time.unscaledTime + ShakeCooldownSec;
                Debug.Log("[Editor 흔들기] F8 → 창고 비행 수거 시도 (누적 자원이 있어야 발동)");
            }
            else
                Debug.Log("[Editor 흔들기] F8 — 수거 안 됨: 비행 중이거나 창고 누적 0, 또는 HomeController 없음");
        }
#endif
    }

    void TryResolveFlyTargetsFromGlobalUI()
    {
        if (goldFlyTarget != null) return;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gui == null) return;
        goldFlyTarget = gui.AssetsTarget;
    }

    public void UpdatePileVisuals()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        if (HidePilesWhileFlying)
        {
            SetPileArray(goldPiles, 0);
            return;
        }

        long mElapsed;
        long fElapsed;
        if (homeController != null)
        {
            mElapsed = homeController.GetMarketElapsedSeconds();
            fElapsed = homeController.GetFarmElapsedSeconds();
        }
        else
        {
            mElapsed = 0;
            fElapsed = 0;
        }

        int mTier = PileCountFromElapsedTiered(mElapsed);
        int fTier = PileCountFromElapsedTiered(fElapsed);
        int combined = Mathf.Max(mTier, fTier);

        SetPileArray(goldPiles, combined);
    }

    public static int PileCountFromElapsedTiered(long elapsedSec)
    {
        if (elapsedSec < 60) return 0;
        if (elapsedSec < 3600) return 1;
        int extraHours = (int)(elapsedSec / 3600);
        return Mathf.Clamp(1 + extraHours, 1, 8);
    }

    static void SetPileArray(GameObject[] piles, int activeCount)
    {
        if (piles == null) return;
        for (int i = 0; i < piles.Length; i++)
        {
            if (piles[i] == null) continue;
            piles[i].SetActive(i < activeCount);
        }
    }

    public bool HasActivePileVisual() => CountActive(goldPiles) > 0;

    public int CountActiveGoldPiles() => CountActive(goldPiles);

    static int CountActive(GameObject[] piles)
    {
        if (piles == null) return 0;
        int c = 0;
        for (int i = 0; i < piles.Length; i++)
            if (piles[i] != null && piles[i].activeSelf) c++;
        return c;
    }

    public bool IsFlyBusy => _pendingFlyTweens > 0;

    public bool HidePilesWhileFlying => _hidePilesWhileFlying && IsFlyBusy;

    public void TryCollectFromGate()
    {
        if (homeController == null) return;
        homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: true);
    }

    /// <summary>시장·농장에서 나온 금화를 합산해 비행 입금합니다.</summary>
    public void PlayFlyEffect(long totalMarketGold, long totalFarmGold, Action onAllComplete = null)
    {
        EnsureFlyRootIfNeeded();
        TryResolveFlyTargetsFromGlobalUI();

        WarmFlyPoolsIfPossible();

        long totalGold = totalMarketGold + totalFarmGold;

        int gVis = CountActive(goldPiles);
        int goldFlies = totalGold > 0 && gVis > 0 ? gVis : 0;

        if (goldFlies <= 0 || totalGold <= 0)
        {
            onAllComplete?.Invoke();
            return;
        }

        var canvas = flyIconsRoot != null ? flyIconsRoot.GetComponentInParent<Canvas>() : null;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2[] goldStarts = goldFlies > 0 ? CaptureStartAnchoredPositions(goldPiles, goldFlies, cam) : null;

        _hidePilesWhileFlying = true;
        SetPileArray(goldPiles, 0);

        int completed = 0;
        void OnOneFlyDone()
        {
            completed++;
            if (completed < goldFlies) return;
            _hidePilesWhileFlying = false;
            onAllComplete?.Invoke();
        }

        int globalIndex = 0;
        SpawnFliesForResource(totalGold, goldFlies, goldStarts, goldFlyTarget, ref globalIndex, OnOneFlyDone);
    }

    void EnsureFlyRootIfNeeded()
    {
        if (flyIconsRoot != null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var t = canvas.transform.Find("FlyIconsRoot");
        if (t != null)
        {
            flyIconsRoot = t as RectTransform;
            EnsureFlyRootOverlayCanvas(flyIconsRoot);
            return;
        }

        var go = new GameObject("FlyIconsRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        flyIconsRoot = rt;

        EnsureFlyRootOverlayCanvas(flyIconsRoot);
    }

    static void EnsureFlyRootOverlayCanvas(RectTransform root)
    {
        if (root == null) return;
        var c = root.GetComponent<Canvas>();
        if (c == null) c = root.gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = 32767;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.gameObject.AddComponent<GraphicRaycaster>();
    }

    void SpawnFliesForResource(long total, int flyCount, Vector2[] startAnchoredPositions, RectTransform target,
        ref int globalFlyIndex, Action onSingleFlyComplete)
    {
        if (total <= 0 || flyCount <= 0) return;

        long baseChunk = total / flyCount;
        long remainder = total % flyCount;

        var canvas = flyIconsRoot != null ? flyIconsRoot.GetComponentInParent<Canvas>() : null;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        for (int i = 0; i < flyCount; i++)
        {
            Vector2 startAnchored = (startAnchoredPositions != null && i >= 0 && i < startAnchoredPositions.Length)
                ? startAnchoredPositions[i]
                : GetFlyStartAnchoredPosition(null, cam);

            long chunk = baseChunk + (i < remainder ? 1 : 0);
            if (chunk <= 0) continue;

            float delay = globalFlyIndex * flyStagger;
            globalFlyIndex++;
            RunPooledFly(startAnchored, target, cam, chunk, flyDuration, delay, onSingleFlyComplete);
        }
    }

    Vector2[] CaptureStartAnchoredPositions(GameObject[] piles, int flyCount, Camera cam)
    {
        if (flyCount <= 0) return Array.Empty<Vector2>();
        int active = CountActive(piles);
        if (active <= 0) return Array.Empty<Vector2>();

        var result = new Vector2[flyCount];
        for (int i = 0; i < flyCount; i++)
        {
            int pileIndex = Mathf.Min(i, active - 1);
            GameObject pileGo = GetIthActivePile(piles, pileIndex);
            RectTransform rt = pileGo != null ? pileGo.GetComponent<RectTransform>() : null;
            result[i] = GetFlyStartAnchoredPosition(rt, cam);
        }

        return result;
    }

    static GameObject GetIthActivePile(GameObject[] piles, int activeIndex)
    {
        int seen = -1;
        for (int i = 0; i < piles.Length; i++)
        {
            if (piles[i] == null || !piles[i].activeSelf) continue;
            seen++;
            if (seen == activeIndex) return piles[i];
        }

        return null;
    }

    Vector2 GetFlyStartAnchoredPosition(RectTransform pileRt, Camera cam)
    {
        if (pileRt != null && flyIconsRoot != null)
        {
            Vector3 world = pileRt.TransformPoint(pileRt.rect.center);
            return WorldToAnchoredOnRoot(world, flyIconsRoot, cam);
        }

        if (pileArea != null)
            return WorldToAnchoredOnRoot(pileArea.TransformPoint(pileArea.rect.center), flyIconsRoot, cam);

        return Vector2.zero;
    }

    static Vector2 WorldToAnchoredOnRoot(Vector3 worldPos, RectTransform root, Camera cam)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, cam, out Vector2 local);
        return local;
    }

    void RunPooledFly(Vector2 startAnchored, RectTransform target, Camera cam, long chunk, float duration,
        float delay, Action onFlyComplete)
    {
        GameObject go = RentFlyIcon();
        if (go == null)
        {
            Debug.LogError("[CollectionManager] 비행 아이콘 풀에서 인스턴스를 가져올 수 없습니다.");
            GameManager.InstanceOrNull?.AddGold(chunk);
            onFlyComplete?.Invoke();
            return;
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
        {
            ReturnFlyIcon(go);
            onFlyComplete?.Invoke();
            return;
        }

        Vector2 size = new Vector2(18f, 18f);

        ResetPooledRectTransform(rt, startAnchored, size);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            var col = img.color;
            col.a = 1f;
            img.color = col;
            img.enabled = true;
        }

        go.SetActive(true);

        rt.localScale = Vector3.one;

        Vector2 endAnchored = target != null
            ? WorldToAnchoredOnRoot(target.position, flyIconsRoot, cam)
            : startAnchored + Vector2.up * 400f;

        if (flyIconsRoot != null)
        {
            var r = flyIconsRoot.rect;
            float pad = 30f;
            endAnchored.x = Mathf.Clamp(endAnchored.x, r.xMin + pad, r.xMax - pad);
            endAnchored.y = Mathf.Clamp(endAnchored.y, r.yMin + pad, r.yMax - pad);
        }

        Vector2 mid = (startAnchored + endAnchored) * 0.5f + new Vector2(0f, bezierArc);

        float dist = Vector2.Distance(startAnchored, endAnchored);
        float scaledDuration = Mathf.Clamp(duration * (dist / 700f), duration * 0.85f, duration * 1.8f);

        _pendingFlyTweens++;
        Sequence seq = DOTween.Sequence();
        seq.SetTarget(go);
        if (delay > 0) seq.AppendInterval(delay);
        seq.Append(DOTween.To(() => 0f, t =>
        {
            float u = t;
            Vector2 p = QuadraticBezier(startAnchored, mid, endAnchored, u);
            rt.anchoredPosition = p;
        }, 1f, scaledDuration).SetEase(Ease.InOutQuad).SetTarget(go));
        seq.OnComplete(() =>
        {
            var gm = GameManager.InstanceOrNull;
            if (gm != null)
            {
                gm.AddGold(chunk);
                GlobalUIManager.InstanceOrNull?.PunchAssetsText();
            }

            ReturnFlyIcon(go);
            _pendingFlyTweens--;
            onFlyComplete?.Invoke();
        });
        seq.SetUpdate(true);
    }

    static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
