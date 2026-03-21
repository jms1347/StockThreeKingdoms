using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 성문 앞 금화/식량 더미 시각화, 흔들기 수거, DOTween 비행 후 지연 입금.
/// 비행 아이콘은 오브젝트 풀(Queue)로 재사용합니다.
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
    [Tooltip("식량 텍스트/아이콘의 RectTransform")]
    public RectTransform grainFlyTarget;
    [Tooltip("더미가 배치된 영역 (비행 시작 위치 월드→스크린 변환 기준)")]
    public RectTransform pileArea;

    [Header("더미 아이콘 (각 8개, 인덱스 0부터 순서대로 켜짐)")]
    public GameObject[] goldPiles = new GameObject[8];
    public GameObject[] grainPiles = new GameObject[8];

    [Header("비행 아이콘 풀")]
    [Tooltip("풀에서 Instantiate할 금화 비행 아이콘 원본 (씬 오브젝트 또는 프리팹)")]
    public GameObject flyingGoldPrefab;
    [Tooltip("풀에서 Instantiate할 식량 비행 아이콘 원본")]
    public GameObject flyingGrainPrefab;
    [Range(10, 32)]
    public int poolSize = 12;

    /// <summary>비활성 대기 중인 금화 비행 아이콘</summary>
    public readonly Queue<GameObject> goldIconPool = new Queue<GameObject>();
    /// <summary>비활성 대기 중인 식량 비행 아이콘</summary>
    public readonly Queue<GameObject> grainIconPool = new Queue<GameObject>();

    [Header("비행 연출")]
    public float flyDuration = 0.85f;
    public float bezierArc = 180f;
    [Range(0.02f, 0.3f)] public float flyStagger = 0.06f;
    [Tooltip("풀 인스턴스 기본 크기 (프리팹에 RectTransform이 있으면 생략 가능)")]
    public Vector2 flyIconSize = new Vector2(48f, 48f);

    float _shakeCooldownUntil;
    int _pendingFlyTweens;
    bool _poolsWarmed;

    GameObject _runtimeGoldProto;
    GameObject _runtimeGrainProto;

    void Awake()
    {
        if (homeController == null)
            homeController = GetComponentInParent<HomeController>();
    }

    void Start()
    {
        WarmFlyPoolsIfPossible();
    }

    void OnDestroy()
    {
        DOTween.Kill(this, true);
        PurgePoolTweens(goldIconPool);
        PurgePoolTweens(grainIconPool);
    }

    static void PurgePoolTweens(Queue<GameObject> pool)
    {
        foreach (var go in pool)
        {
            if (go == null) continue;
            DOTween.Kill(go.transform, true);
        }
    }

    /// <summary>flyIconsRoot가 있으면 풀 프리워밍 (중복 호출 안전)</summary>
    public void WarmFlyPoolsIfPossible()
    {
        if (_poolsWarmed || flyIconsRoot == null) return;

        EnsureRuntimePrototypeIfNeeded(true);
        EnsureRuntimePrototypeIfNeeded(false);

        PrewarmPool(true);
        PrewarmPool(false);
        _poolsWarmed = true;
    }

    void PrewarmPool(bool isGold)
    {
        var proto = GetPrototype(isGold);
        if (proto == null) return;

        var q = isGold ? goldIconPool : grainIconPool;
        int need = Mathf.Max(0, poolSize - q.Count);
        for (int i = 0; i < need; i++)
        {
            var inst = Instantiate(proto, flyIconsRoot, false);
            inst.name = isGold ? $"FlyGold_Pool_{i}" : $"FlyGrain_Pool_{i}";
            inst.SetActive(false);
            q.Enqueue(inst);
        }
    }

    GameObject GetPrototype(bool isGold)
    {
        if (isGold)
        {
            if (flyingGoldPrefab != null) return flyingGoldPrefab;
            return _runtimeGoldProto;
        }
        if (flyingGrainPrefab != null) return flyingGrainPrefab;
        return _runtimeGrainProto;
    }

    void EnsureRuntimePrototypeIfNeeded(bool isGold)
    {
        if (isGold)
        {
            if (flyingGoldPrefab != null || _runtimeGoldProto != null) return;
            _runtimeGoldProto = BuildDefaultFlyPrototype(true);
        }
        else
        {
            if (flyingGrainPrefab != null || _runtimeGrainProto != null) return;
            _runtimeGrainProto = BuildDefaultFlyPrototype(false);
        }
    }

    GameObject BuildDefaultFlyPrototype(bool isGold)
    {
        var go = new GameObject(isGold ? "FlyGold_RuntimeProto" : "FlyGrain_RuntimeProto",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(flyIconsRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = flyIconSize;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var img = go.GetComponent<Image>();
        img.color = isGold ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.85f, 0.35f);
        img.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    GameObject RentFlyIcon(bool isGold)
    {
        var q = isGold ? goldIconPool : grainIconPool;
        GameObject go = q.Count > 0 ? q.Dequeue() : null;
        if (go == null)
        {
            var proto = GetPrototype(isGold);
            if (proto == null)
            {
                EnsureRuntimePrototypeIfNeeded(isGold);
                proto = GetPrototype(isGold);
            }
            if (proto == null) return null;
            go = Instantiate(proto, flyIconsRoot, false);
            go.name = isGold ? "FlyGold_Expanded" : "FlyGrain_Expanded";
        }

        DOTween.Kill(go.transform, true);
        go.transform.SetParent(flyIconsRoot, false);
        return go;
    }

    void ReturnFlyIcon(GameObject go, bool isGold)
    {
        if (go == null) return;
        DOTween.Kill(go.transform, true);
        go.SetActive(false);
        go.transform.SetParent(flyIconsRoot, false);
        (isGold ? goldIconPool : grainIconPool).Enqueue(go);
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

        if (Time.unscaledTime < _shakeCooldownUntil) return;
        if (homeController == null) return;

        Vector3 acc = Input.acceleration;
        if (acc.sqrMagnitude > ShakeSqrThreshold)
        {
            if (homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: false))
                _shakeCooldownUntil = Time.unscaledTime + ShakeCooldownSec;
        }

#if UNITY_EDITOR
        // 에디터에선 가속도가 거의 0 → F8로 흔들기 수거와 동일 경로 테스트 (쿨다운 동일)
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

    public void UpdatePileVisuals()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        long now = TimeManager.GetUnixNow();
        long mLast = gm.currentUser.lastMarketCollectTime <= 0 ? now : gm.currentUser.lastMarketCollectTime;
        long fLast = gm.currentUser.lastFarmCollectTime <= 0 ? now : gm.currentUser.lastFarmCollectTime;

        int goldOn = PileCountFromElapsedHours(now - mLast);
        int grainOn = PileCountFromElapsedHours(now - fLast);

        SetPileArray(goldPiles, goldOn);
        SetPileArray(grainPiles, grainOn);
    }

    static int PileCountFromElapsedHours(long elapsedSeconds)
    {
        if (elapsedSeconds <= 0) return 0;
        double hours = elapsedSeconds / 3600.0d;
        double ratio = (hours / 8.0) * 8.0;
        int n = (int)Math.Floor(ratio);
        return Mathf.Clamp(n, 0, 8);
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

    public bool HasActivePileVisual()
    {
        return CountActive(goldPiles) > 0 || CountActive(grainPiles) > 0;
    }

    static int CountActive(GameObject[] piles)
    {
        if (piles == null) return 0;
        int c = 0;
        for (int i = 0; i < piles.Length; i++)
            if (piles[i] != null && piles[i].activeSelf) c++;
        return c;
    }

    public bool IsFlyBusy => _pendingFlyTweens > 0;

    public void TryCollectFromGate()
    {
        if (homeController == null) return;
        homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: true);
    }

    public void PlayFlyEffect(long totalGold, long totalGrain)
    {
        if (flyIconsRoot == null)
        {
            Debug.LogWarning("[CollectionManager] flyIconsRoot 미할당 — 즉시 입금으로 폴백");
            ApplyImmediateFallback(totalGold, totalGrain);
            return;
        }

        WarmFlyPoolsIfPossible();

        int gVis = Mathf.Clamp(CountActive(goldPiles), 1, 8);
        int grVis = Mathf.Clamp(CountActive(grainPiles), 1, 8);

        if (totalGold > 0)
            SpawnFliesForResource(totalGold, gVis, goldPiles, goldFlyTarget, true);
        if (totalGrain > 0)
            SpawnFliesForResource(totalGrain, grVis, grainPiles, grainFlyTarget, false);
    }

    void ApplyImmediateFallback(long totalGold, long totalGrain)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        if (totalGold > 0) gm.AddGold(totalGold);
        if (totalGrain > 0) gm.AddGrain(totalGrain);
    }

    void SpawnFliesForResource(long total, int flyCount, GameObject[] piles, RectTransform target, bool isGold)
    {
        if (total <= 0 || piles == null || flyCount <= 0) return;

        long baseChunk = total / flyCount;
        long remainder = total % flyCount;

        var canvas = flyIconsRoot.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        int activePiles = CountActive(piles);

        for (int i = 0; i < flyCount; i++)
        {
            int pileIndex = activePiles > 0 ? Mathf.Min(i, activePiles - 1) : 0;
            GameObject pileGo = (activePiles > 0) ? GetIthActivePile(piles, pileIndex) : null;
            RectTransform startRt = pileGo != null ? pileGo.GetComponent<RectTransform>() : null;
            Vector2 startAnchored = GetFlyStartAnchoredPosition(startRt, cam);

            long chunk = baseChunk + (i < remainder ? 1 : 0);
            if (chunk <= 0) continue;

            float delay = i * flyStagger;
            RunPooledFly(startAnchored, target, cam, chunk, isGold, flyDuration, delay);
        }
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

    void RunPooledFly(Vector2 startAnchored, RectTransform target, Camera cam, long chunk, bool isGold, float duration, float delay)
    {
        GameObject go = RentFlyIcon(isGold);
        if (go == null)
        {
            Debug.LogError("[CollectionManager] 비행 아이콘 풀에서 인스턴스를 가져올 수 없습니다.");
            var gm = GameManager.InstanceOrNull;
            if (gm != null)
            {
                if (isGold) gm.AddGold(chunk);
                else gm.AddGrain(chunk);
            }
            return;
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
        {
            ReturnFlyIcon(go, isGold);
            return;
        }

        Vector2 size = flyIconSize;
        var src = GetPrototype(isGold);
        if (src != null)
        {
            var pr = src.GetComponent<RectTransform>();
            if (pr != null) size = pr.sizeDelta;
        }

        ResetPooledRectTransform(rt, startAnchored, size);
        go.SetActive(true);

        Vector2 endAnchored = target != null
            ? WorldToAnchoredOnRoot(target.TransformPoint(target.rect.center), flyIconsRoot, cam)
            : startAnchored + Vector2.up * 400f;

        Vector2 mid = (startAnchored + endAnchored) * 0.5f + new Vector2(0f, bezierArc);

        _pendingFlyTweens++;
        Sequence seq = DOTween.Sequence();
        seq.SetTarget(rt);
        if (delay > 0) seq.AppendInterval(delay);
        seq.Append(DOTween.To(() => 0f, t =>
        {
            float u = t;
            Vector2 p = QuadraticBezier(startAnchored, mid, endAnchored, u);
            rt.anchoredPosition = p;
        }, 1f, duration).SetEase(Ease.InOutQuad).SetTarget(rt));
        seq.OnComplete(() =>
        {
            var gm = GameManager.InstanceOrNull;
            if (gm != null)
            {
                if (isGold) gm.AddGold(chunk);
                else gm.AddGrain(chunk);
            }
            DOTween.Kill(rt, true);
            ReturnFlyIcon(go, isGold);
            _pendingFlyTweens--;
        });
        seq.SetUpdate(true);
    }

    static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

}
