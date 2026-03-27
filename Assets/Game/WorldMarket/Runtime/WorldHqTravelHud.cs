using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 탭 — 본영 이주 대기 중 하단에 경로·이동 게이지(필요 pt 대비 충전 진행) 표시.</summary>
[DisallowMultipleComponent]
public class WorldHqTravelHud : MonoBehaviour
{
    public static WorldHqTravelHud InstanceOrNull { get; private set; }

    const float PanelHeight = 72f;

    [SerializeField] RectTransform hudRoot;
    [SerializeField] Image gaugeBackground;
    [SerializeField] Image gaugeFill;
    [SerializeField] TextMeshProUGUI gaugeValueText;
    [SerializeField] TextMeshProUGUI routeStatusText;
    [SerializeField] Button debugAddStepsButton;

    LayoutElement _hostLayoutElement;
    DataManager _dm;
    float _displayedGaugePoints;
    Coroutine _travelRoutine;

    /// <summary>본영 이주 패널·코루틴이 돌아가는 동안. 리스트·팝업 UI 분기용(대기 이주는 <see cref="DataManager.HasPendingHqMove"/>).</summary>
    public bool IsHqTravelAnimating => _travelRoutine != null;

    void Awake()
    {
        InstanceOrNull = this;
        _hostLayoutElement = GetComponent<LayoutElement>();
        BuildUiIfNeeded();
        ResolveHudRoot();
        SetTravelPanelVisible(false);
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
        ForceAbortTravelInProgress();
        UnhookDataManager();
    }

    void OnEnable()
    {
        HookDataManager();
        TryHookDebugButton();
        TryResumePendingTravelPanel();
    }

    void OnDisable()
    {
        if (_travelRoutine != null)
        {
            StopCoroutine(_travelRoutine);
            _travelRoutine = null;
        }

        SetTravelPanelVisible(false);
        if (routeStatusText != null)
            routeStatusText.text = "";

        UnhookDataManager();
    }

    void TryResumePendingTravelPanel()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || !dm.HasPendingHqMove || _travelRoutine != null)
            return;
        if (!gameObject.activeInHierarchy)
            return;
        _travelRoutine = StartCoroutine(CoTravel(dm, dm.PendingHqMoveTargetId, dm.PendingHqMoveCostPoints));
    }

    void Update()
    {
        if (hudRoot == null || !hudRoot.gameObject.activeSelf) return;
        if (_dm == null || !_dm.IsStateReady) return;
        if (_travelRoutine != null) return;
        _displayedGaugePoints = _dm.TravelGaugePoints;
        UpdateGaugeVisual();
    }

    void HookDataManager()
    {
        _dm = DataManager.InstanceOrNull;
        if (_dm == null) return;
        _dm.OnHomeCastleChanged += OnHomeOrGaugeDirty;
        _dm.OnTravelGaugeChanged += OnHomeOrGaugeDirty;
    }

    void UnhookDataManager()
    {
        if (_dm == null) return;
        _dm.OnHomeCastleChanged -= OnHomeOrGaugeDirty;
        _dm.OnTravelGaugeChanged -= OnHomeOrGaugeDirty;
        _dm = null;
    }

    void OnHomeOrGaugeDirty()
    {
        if (_travelRoutine != null)
        {
            if (hudRoot != null && hudRoot.gameObject.activeSelf)
                RefreshPendingTravelVisuals();
            return;
        }

        if (hudRoot == null || !hudRoot.gameObject.activeSelf) return;
        RefreshGaugeWhenVisible();
    }

    void RefreshPendingTravelVisuals()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || !dm.HasPendingHqMove) return;
        _displayedGaugePoints = dm.TravelGaugePoints;
        UpdateGaugeVisualForPending(dm.PendingHqMoveCostPoints);
    }

    void TryHookDebugButton()
    {
        if (debugAddStepsButton == null) return;
        debugAddStepsButton.onClick.RemoveListener(OnDebugAddSteps);
        debugAddStepsButton.onClick.AddListener(OnDebugAddSteps);
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        debugAddStepsButton.gameObject.SetActive(false);
#endif
    }

    void OnDebugAddSteps()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        gm.currentUser.stepsToday += 500;
        gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
        gm.OnStepsChanged?.Invoke(gm.currentUser.stepsToday);
    }

    /// <summary>성 카드의 본영 이동 버튼에서 호출.</summary>
    public void TryBeginTravelTo(string targetCastleId)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return;
        if (_travelRoutine != null) return;

        if (string.IsNullOrWhiteSpace(targetCastleId))
            return;
        targetCastleId = targetCastleId.Trim();

        if (dm.HasPendingHqMove)
        {
            if (string.Equals(dm.PendingHqMoveTargetId, targetCastleId, StringComparison.Ordinal))
                return;
            Debug.LogWarning("[WorldHqTravelHud] 이미 다른 목적지로 본영 이동 중입니다.");
            return;
        }

        if (!dm.TryValidateHqMove(targetCastleId, out float cost, out string err))
        {
            Debug.LogWarning("[WorldHqTravelHud] " + err);
            return;
        }

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        _travelRoutine = StartCoroutine(CoTravel(dm, targetCastleId, cost));
        if (_travelRoutine == null)
        {
            Debug.LogWarning(
                "[WorldHqTravelHud] 이동 연출 코루틴을 시작하지 못했습니다. WorldHqTravelHud 오브젝트·부모가 활성화되어 있는지 확인하세요.");
        }
    }

    void ForceAbortTravelInProgress()
    {
        if (_travelRoutine != null)
        {
            StopCoroutine(_travelRoutine);
            _travelRoutine = null;
        }

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.ClearPendingHqMove();

        SetTravelPanelVisible(false);
        if (routeStatusText != null)
            routeStatusText.text = "";
    }

    IEnumerator CoTravel(DataManager dm, string targetCastleId, float cost)
    {
        ResolveHudRoot();
        dm.SetPendingHqMove(targetCastleId, cost);
        dm.TryCompletePendingHqMoveIfReady();
        if (!dm.HasPendingHqMove)
        {
            SetTravelPanelVisible(false);
            _travelRoutine = null;
            yield break;
        }

        SetTravelPanelVisible(true);
        yield return null;

        dm.RequestWorldUiRefresh();

        string fromId = dm.HomeCastleId?.Trim() ?? "";
        string fromName = string.IsNullOrWhiteSpace(fromId) ? "—" : dm.GetCastleDisplayName(fromId);
        if (string.IsNullOrWhiteSpace(fromName))
            fromName = fromId;

        string toName = dm.GetCastleDisplayName(targetCastleId);
        if (string.IsNullOrWhiteSpace(toName))
            toName = targetCastleId;

        if (routeStatusText != null)
            routeStatusText.text = $"{fromName}(본영) → {toName} 이동 중…";

        while (dm.HasPendingHqMove)
        {
            _displayedGaugePoints = dm.TravelGaugePoints;
            UpdateGaugeVisualForPending(cost);
            yield return null;
        }

        _displayedGaugePoints = dm.TravelGaugePoints;
        UpdateGaugeVisual();
        SetTravelPanelVisible(false);
        if (routeStatusText != null)
            routeStatusText.text = "";

        _travelRoutine = null;
    }

    void RefreshGaugeWhenVisible()
    {
        _dm = DataManager.InstanceOrNull;
        if (_dm == null || !_dm.IsStateReady) return;
        _displayedGaugePoints = _dm.TravelGaugePoints;
        UpdateGaugeVisual();
    }

    void UpdateGaugeVisualForPending(float requiredTotal)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        float p = dm.TravelGaugePoints;
        float fill = requiredTotal > 1e-4f ? Mathf.Clamp01(p / requiredTotal) : 1f;
        if (gaugeFill != null)
            gaugeFill.fillAmount = fill;
        if (gaugeValueText != null)
        {
            float remain = Mathf.Max(0f, requiredTotal - p);
            int stepEq = dm.GetTravelCostStepEquivalent(remain);
            gaugeValueText.text =
                $"이동 충전 {p:N0} / {requiredTotal:N0}pt (만보기 약 {stepEq:N0}보 남음)";
        }
    }

    void UpdateGaugeVisual()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        float cap = dm.TravelGaugeVisualCap;
        float fill = cap > 1e-4f ? Mathf.Clamp01(_displayedGaugePoints / cap) : 0f;
        if (gaugeFill != null)
            gaugeFill.fillAmount = fill;
        if (gaugeValueText != null)
            gaugeValueText.text = $"{_displayedGaugePoints:N0} / ~{cap:N0}";
    }

    void SetTravelPanelVisible(bool visible)
    {
        ResolveHudRoot();
        if (hudRoot != null)
            hudRoot.gameObject.SetActive(visible);

        if (_hostLayoutElement == null)
            _hostLayoutElement = GetComponent<LayoutElement>();
        if (_hostLayoutElement != null)
        {
            if (visible)
            {
                _hostLayoutElement.minHeight = PanelHeight;
                _hostLayoutElement.preferredHeight = PanelHeight;
            }
            else
            {
                _hostLayoutElement.minHeight = 0f;
                _hostLayoutElement.preferredHeight = 0f;
            }
        }
    }

    void ResolveHudRoot()
    {
        if (hudRoot != null) return;
        if (gaugeFill != null)
        {
            Transform t = gaugeFill.transform;
            while (t != null && t.parent != transform)
                t = t.parent;
            hudRoot = t as RectTransform;
        }

        if (hudRoot == null && transform.childCount > 0)
            hudRoot = transform.GetChild(0) as RectTransform;
    }

    void BuildUiIfNeeded()
    {
        if (gaugeFill != null) return;

        var root = transform as RectTransform;
        if (root == null) return;

        var strip = new GameObject("HqTravelHudStrip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        var stripRt = strip.GetComponent<RectTransform>();
        strip.transform.SetParent(root, false);
        StretchBottomFullWidth(stripRt, PanelHeight);

        strip.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.96f);
        strip.GetComponent<Image>().raycastTarget = true;
        var stripLe = strip.GetComponent<LayoutElement>();
        stripLe.minHeight = PanelHeight;
        stripLe.preferredHeight = PanelHeight;
        stripLe.flexibleWidth = 1f;
        stripLe.flexibleHeight = 0f;

        var vl = strip.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(12, 12, 6, 6);
        vl.spacing = 6;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        var rowTop = new GameObject("RouteRow", typeof(RectTransform), typeof(LayoutElement));
        rowTop.transform.SetParent(strip.transform, false);
        rowTop.GetComponent<LayoutElement>().minHeight = 22f;
        rowTop.GetComponent<LayoutElement>().preferredHeight = 24f;
        rowTop.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var routeTmp = rowTop.AddComponent<TextMeshProUGUI>();
        routeTmp.fontSize = 15;
        routeTmp.fontStyle = FontStyles.Bold;
        routeTmp.color = new Color(0.92f, 0.88f, 0.72f, 1f);
        routeTmp.text = "";
        routeTmp.enableWordWrapping = false;
        var rrt = rowTop.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;
        routeStatusText = routeTmp;

        var rowBot = new GameObject("GaugeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowBot.transform.SetParent(strip.transform, false);
        var rowBotLe = rowBot.GetComponent<LayoutElement>();
        rowBotLe.minHeight = 30f;
        rowBotLe.preferredHeight = 32f;
        rowBotLe.flexibleWidth = 1f;
        var rowH = rowBot.GetComponent<HorizontalLayoutGroup>();
        rowH.padding = new RectOffset(0, 0, 0, 0);
        rowH.spacing = 10;
        rowH.childAlignment = TextAnchor.MiddleLeft;
        rowH.childControlWidth = true;
        rowH.childControlHeight = true;
        rowH.childForceExpandHeight = true;
        rowH.childForceExpandWidth = false;

        var barHost = new GameObject("TravelGaugeBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barHost.transform.SetParent(rowBot.transform, false);
        barHost.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        barHost.GetComponent<Image>().raycastTarget = false;
        var barLe = barHost.GetComponent<LayoutElement>();
        barLe.flexibleWidth = 1f;
        barLe.minWidth = 80f;
        barLe.minHeight = 22f;
        barLe.preferredHeight = 22f;
        gaugeBackground = barHost.GetComponent<Image>();

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barHost.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fImg = fillGo.GetComponent<Image>();
        fImg.type = Image.Type.Filled;
        fImg.fillMethod = Image.FillMethod.Horizontal;
        fImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fImg.fillAmount = 0f;
        fImg.color = new Color(0.35f, 0.62f, 0.95f, 0.85f);
        fImg.raycastTarget = false;
        gaugeFill = fImg;

        var valGo = new GameObject("GaugeValue", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        valGo.transform.SetParent(rowBot.transform, false);
        var valTmp = valGo.GetComponent<TextMeshProUGUI>();
        valTmp.fontSize = 13;
        valTmp.color = new Color(0.78f, 0.8f, 0.84f, 1f);
        valTmp.alignment = TextAlignmentOptions.MidlineRight;
        valTmp.text = "0 / 0";
        var valLe = valGo.GetComponent<LayoutElement>();
        valLe.minWidth = 120f;
        valLe.preferredWidth = 220f;
        valLe.flexibleWidth = 0f;
        gaugeValueText = valTmp;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var dbg = new GameObject("DebugStepsPlus500", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        dbg.transform.SetParent(rowBot.transform, false);
        dbg.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.22f, 0.95f);
        var dle = dbg.GetComponent<LayoutElement>();
        dle.minWidth = 72f;
        dle.preferredWidth = 88f;
        debugAddStepsButton = dbg.GetComponent<Button>();
        var dlab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        dlab.transform.SetParent(dbg.transform, false);
        var dtmp = dlab.GetComponent<TextMeshProUGUI>();
        dtmp.fontSize = 12;
        dtmp.fontStyle = FontStyles.Bold;
        dtmp.color = Color.white;
        dtmp.text = "+500보";
        dtmp.alignment = TextAlignmentOptions.Center;
        var drt = dlab.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
#endif

        hudRoot = stripRt;
    }

    static void StretchBottomFullWidth(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>씬에 HUD가 없을 때 <see cref="WorldMarketRoot"/> 등 아래에 한 번만 붙입니다.</summary>
    public static void EnsureUnderWorldMarketRoot(Transform worldMarketRoot)
    {
        if (worldMarketRoot == null) return;
        if (worldMarketRoot.GetComponentInChildren<WorldHqTravelHud>(true) != null)
            return;

        var go = new GameObject("WorldHqTravelHud", typeof(RectTransform), typeof(LayoutElement), typeof(WorldHqTravelHud));
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(worldMarketRoot, false);
        go.transform.SetAsLastSibling();
        StretchBottomFullWidth(rt, 0f);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 0f;
        le.preferredHeight = 0f;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;
    }
}
