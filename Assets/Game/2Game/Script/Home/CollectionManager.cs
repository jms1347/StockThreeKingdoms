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
    [Range(0.02f, 0.3f)] public float flyStagger = 0.05f;
    [Tooltip("풀 인스턴스 기본 크기 (프리팹에 RectTransform이 있으면 생략 가능)")]
    public Vector2 flyIconSize = new Vector2(48f, 48f);

    float _shakeCooldownUntil;
    int _pendingFlyTweens;
    bool _poolsWarmed;
    bool _hidePilesWhileFlying;

    GameObject _runtimeGoldProto;
    GameObject _runtimeGrainProto;

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
        DOTween.Kill(go, true);
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

    void TryResolveFlyTargetsFromGlobalUI()
    {
        // HomeScene에서 ResourceBar를 없애고 GlobalUI 탑바를 도착지로 쓰는 경우,
        // GlobalUIManager가 늦게 로드되면 Start 시점엔 target이 null일 수 있음 → 매 프레임 가볍게 보강.
        if (goldFlyTarget != null && grainFlyTarget != null) return;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gui == null) return;
        if (goldFlyTarget == null) goldFlyTarget = gui.AssetsTarget;
        if (grainFlyTarget == null) grainFlyTarget = gui.FoodTarget;
    }

    public void UpdatePileVisuals()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        // 비행 중엔 출발 순간부터 더미를 숨긴 상태 유지
        if (HidePilesWhileFlying)
        {
            SetPileArray(goldPiles, 0);
            SetPileArray(grainPiles, 0);
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
            // HomeController 없으면 생산/기준시각 해석 불가 → 더미 표시 안 함 (잘못된 아이콘 방지)
            mElapsed = 0;
            fElapsed = 0;
        }

        int goldOn = PileCountFromElapsedTiered(mElapsed);
        int grainOn = PileCountFromElapsedTiered(fElapsed);

        SetPileArray(goldPiles, goldOn);
        SetPileArray(grainPiles, grainOn);
    }

    /// <summary>
    /// T &lt; 1분: 0 / 1분~1시간 미만: 1 / 이후 1시간마다 +1 (최대 8).
    /// 7시간 이상은 8개 고정.
    /// </summary>
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

    public bool HasActivePileVisual()
    {
        return CountActive(goldPiles) > 0 || CountActive(grainPiles) > 0;
    }

    public int CountActiveGoldPiles() => CountActive(goldPiles);

    public int CountActiveGrainPiles() => CountActive(grainPiles);

    static int CountActive(GameObject[] piles)
    {
        if (piles == null) return 0;
        int c = 0;
        for (int i = 0; i < piles.Length; i++)
            if (piles[i] != null && piles[i].activeSelf) c++;
        return c;
    }

    public bool IsFlyBusy => _pendingFlyTweens > 0;

    /// <summary>비행 시작과 동시에 더미를 숨겼는지(출발 순간 아이콘 즉시 제거용).</summary>
    public bool HidePilesWhileFlying => _hidePilesWhileFlying && IsFlyBusy;

    public void TryCollectFromGate()
    {
        if (homeController == null) return;
        homeController.TryFlyCollectFromWarehouse(this, requireActivePiles: true);
    }

    /// <summary>
    /// 활성 주머니 개수만큼 자원을 분할해 순차(스태거) 비행 후, 도착 시마다 입금+펀치.
    /// 모든 비행이 끝나면 주머니 비활성화 및 onAllComplete 호출(lastCollectTime 갱신 등).
    /// </summary>
    public void PlayFlyEffect(long totalGold, long totalGrain, Action onAllComplete = null)
    {
        EnsureFlyRootIfNeeded();
        TryResolveFlyTargetsFromGlobalUI();

        WarmFlyPoolsIfPossible();

        int gVis = CountActive(goldPiles);
        int grVis = CountActive(grainPiles);

        int goldFlies = totalGold > 0 && gVis > 0 ? gVis : 0;
        int grainFlies = totalGrain > 0 && grVis > 0 ? grVis : 0;
        int totalFlies = goldFlies + grainFlies;

        if (totalFlies <= 0)
        {
            onAllComplete?.Invoke();
            return;
        }

        // 출발 위치는 "숨기기 이전" 활성 더미 위치를 캐시 (숨김 후엔 activeSelf=false라 start가 한 곳으로 모임)
        var canvas = flyIconsRoot != null ? flyIconsRoot.GetComponentInParent<Canvas>() : null;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2[] goldStarts = goldFlies > 0 ? CaptureStartAnchoredPositions(goldPiles, goldFlies, cam) : null;
        Vector2[] grainStarts = grainFlies > 0 ? CaptureStartAnchoredPositions(grainPiles, grainFlies, cam) : null;

        // 출발 순간: 창고에 남아있는 더미는 즉시 숨기고, 비행 아이콘만 보이게.
        _hidePilesWhileFlying = true;
        SetPileArray(goldPiles, 0);
        SetPileArray(grainPiles, 0);

        int completed = 0;
        void OnOneFlyDone()
        {
            completed++;
            if (completed < totalFlies) return;
            _hidePilesWhileFlying = false;
            onAllComplete?.Invoke();
        }

        int globalIndex = 0;
        if (goldFlies > 0)
        {
            SpawnFliesForResource(totalGold, goldFlies, goldStarts, goldFlyTarget, true, ref globalIndex, OnOneFlyDone);
        }
        if (grainFlies > 0)
        {
            SpawnFliesForResource(totalGrain, grainFlies, grainStarts, grainFlyTarget, false, ref globalIndex, OnOneFlyDone);
        }
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

        // GlobalUIManager가 별도 Canvas(sortingOrder=1000)를 쓰므로,
        // 비행 아이콘은 그 위에 보이도록 오버레이 캔버스를 따로 둡니다.
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
        // 어떤 UI보다 위에 보이도록 충분히 높게
        c.sortingOrder = 32767;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.gameObject.AddComponent<GraphicRaycaster>();
    }

    void SpawnFliesForResource(long total, int flyCount, Vector2[] startAnchoredPositions, RectTransform target, bool isGold, ref int globalFlyIndex, Action onSingleFlyComplete)
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
            RunPooledFly(startAnchored, target, cam, chunk, isGold, flyDuration, delay, onSingleFlyComplete);
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

    void RunPooledFly(Vector2 startAnchored, RectTransform target, Camera cam, long chunk, bool isGold, float duration, float delay, Action onFlyComplete)
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
            onFlyComplete?.Invoke();
            return;
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
        {
            ReturnFlyIcon(go, isGold);
            onFlyComplete?.Invoke();
            return;
        }

        // 창고 더미(18x18)와 비행 아이콘 크기를 동일하게 맞춤
        Vector2 size = new Vector2(18f, 18f);

        ResetPooledRectTransform(rt, startAnchored, size);
        // 풀 재사용 시 이전 상태(알파/스케일/회전)가 남는 잔상 방지
        var img = go.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            var col = img.color;
            col.a = 1f;
            img.color = col;
            img.enabled = true;
        }
        go.SetActive(true);

        // 출발 전에 1 스케일로 고정 (더미와 동일 스케일로 출발)
        rt.localScale = Vector3.one;

        Vector2 endAnchored = target != null
            // Text/레이아웃이 가로로 크게 늘어나는 경우 rect.center가 화면 밖으로 튈 수 있어 pivot(=position) 기준으로 계산
            ? WorldToAnchoredOnRoot(target.position, flyIconsRoot, cam)
            : startAnchored + Vector2.up * 400f;

        // 혹시라도 도착점이 레이아웃/스케일 이슈로 화면 밖으로 계산되면,
        // flyIconsRoot 안으로 살짝 클램프해서 “오른쪽 화면 밖으로 이탈”을 방지
        if (flyIconsRoot != null)
        {
            var r = flyIconsRoot.rect;
            float pad = 30f;
            endAnchored.x = Mathf.Clamp(endAnchored.x, r.xMin + pad, r.xMax - pad);
            endAnchored.y = Mathf.Clamp(endAnchored.y, r.yMin + pad, r.yMax - pad);
        }

        Vector2 mid = (startAnchored + endAnchored) * 0.5f + new Vector2(0f, bezierArc);

        // 거리 기반으로 duration을 약간 보정(타겟이 멀리 튀면 “엄청 빠르게” 보이는 문제 완화)
        float dist = Vector2.Distance(startAnchored, endAnchored);
        float scaledDuration = Mathf.Clamp(duration * (dist / 700f), duration * 0.85f, duration * 1.8f);

        _pendingFlyTweens++;
        Sequence seq = DOTween.Sequence();
        // 아이콘 GameObject를 타겟으로 잡아, 반환/킬이 확실히 되게
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
                if (isGold)
                {
                    gm.AddGold(chunk);
                    GlobalUIManager.InstanceOrNull?.PunchAssetsText();
                }
                else
                {
                    gm.AddGrain(chunk);
                    GlobalUIManager.InstanceOrNull?.PunchFoodText();
                }
            }
            ReturnFlyIcon(go, isGold);
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
