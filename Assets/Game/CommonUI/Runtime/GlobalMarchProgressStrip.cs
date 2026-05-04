using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전역 상단 HUD(Top Bar) 바로 아래 — 본영 이주(행군) 진행률·MP 주입/환급.
/// <see cref="GlobalUIManager"/> 루트의 자식으로 둡니다.
/// </summary>
[DisallowMultipleComponent]
public class GlobalMarchProgressStrip : MonoBehaviour
{
    public static GlobalMarchProgressStrip InstanceOrNull { get; private set; }

    const float StripHeight = 92f;
    const int RefundChunk = 100;

    [SerializeField] RectTransform stripRoot;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image gaugeBackground;
    [SerializeField] Image gaugeFill;
    [SerializeField] TextMeshProUGUI statusText;
    [Tooltip("탭 시 보유 행군 MP를 잔여 이주 비용 한도 내에서 한꺼번에 이동 게이지에 반영")]
    [SerializeField] Button mpSpendMarchPointsButton;
    [SerializeField] Button mpRefundButton;
    [SerializeField] RectTransform arrivalPopupRoot;
    [SerializeField] TextMeshProUGUI arrivalBodyText;
    [SerializeField] Button arrivalCloseButton;

    float _topBarOffset = 180f;
    bool _pendingUi;
    Sequence _showSeq;

    void Awake()
    {
        InstanceOrNull = this;
        BuildUiIfNeeded();
        canvasGroup ??= gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        _pendingUi = false;
        if (arrivalPopupRoot != null)
            arrivalPopupRoot.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
        Unhook();
        _showSeq?.Kill();
    }

    void Start()
    {
        ResolveTopBarInset();
        PositionBelowTopBar();
    }

    void OnEnable()
    {
        StartCoroutine(CoBindWhenReady());
    }

    IEnumerator CoBindWhenReady()
    {
        int guard = 0;
        while (DataManager.InstanceOrNull == null && guard++ < 180)
            yield return null;
        Hook();
        RefreshImmediate();
    }

    void OnDisable()
    {
        Unhook();
        _showSeq?.Kill();
    }

    void Hook()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnTravelGaugeChanged -= OnDmTravel;
        dm.OnTravelGaugeChanged += OnDmTravel;
        dm.OnHqRelocationCompleted -= OnHqCompleted;
        dm.OnHqRelocationCompleted += OnHqCompleted;
    }

    void Unhook()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnTravelGaugeChanged -= OnDmTravel;
        dm.OnHqRelocationCompleted -= OnHqCompleted;
    }

    void OnDmTravel() => RefreshImmediate();

    void OnHqCompleted(string targetCastleId)
    {
        RefreshImmediate();
        ShowArrivalPopup(targetCastleId);
    }

    void ResolveTopBarInset()
    {
        var gui = GlobalUIManager.InstanceOrNull;
        if (gui == null) return;
        var top = gui.transform.Find("TopBar") as RectTransform;
        if (top != null && top.rect.height > 10f)
            _topBarOffset = top.rect.height;
    }

    void PositionBelowTopBar()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, StripHeight);
        rt.anchoredPosition = new Vector2(0f, -_topBarOffset);
    }

    /// <summary><see cref="GlobalUIManager"/>에서 호출해 행군 바 오브젝트를 붙입니다.</summary>
    public static GlobalMarchProgressStrip EnsureOn(Transform globalUiRoot)
    {
        if (globalUiRoot == null) return null;
        var existing = globalUiRoot.GetComponentInChildren<GlobalMarchProgressStrip>(true);
        if (existing != null)
            return existing;

        var go = new GameObject("MarchProgressStrip", typeof(RectTransform), typeof(GlobalMarchProgressStrip));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(globalUiRoot, false);
        var topBar = globalUiRoot.Find("TopBar");
        if (topBar != null)
            go.transform.SetSiblingIndex(topBar.GetSiblingIndex() + 1);
        else
            go.transform.SetAsFirstSibling();

        return go.GetComponent<GlobalMarchProgressStrip>();
    }

    void RefreshImmediate()
    {
        var dm = DataManager.InstanceOrNull;
        bool show = dm != null && dm.IsStateReady && dm.HasPendingHqMove;
        if (show)
            ApplyVisuals(dm);
        UpdateVisibility(show);
    }

    void UpdateVisibility(bool show)
    {
        if (show == _pendingUi)
            return;
        _pendingUi = show;
        _showSeq?.Kill();
        canvasGroup ??= gameObject.GetComponent<CanvasGroup>();

        if (show)
        {
            PositionBelowTopBar();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            var rt = stripRoot != null ? stripRoot : transform as RectTransform;
            if (rt != null)
            {
                const float slidePx = 28f;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + slidePx);
            }

            _showSeq = DOTween.Sequence();
            if (canvasGroup != null)
                _showSeq.Join(canvasGroup.DOFade(1f, 0.35f).SetEase(Ease.OutCubic));
            var slideRt = stripRoot != null ? stripRoot : transform as RectTransform;
            if (slideRt != null)
                _showSeq.Join(slideRt.DOAnchorPosY(slideRt.anchoredPosition.y - 28f, 0.38f).SetEase(Ease.OutCubic));
            _showSeq.OnComplete(() =>
            {
                if (_pendingUi && canvasGroup != null)
                    canvasGroup.alpha = 1f;
            });
        }
        else
        {
            _showSeq = DOTween.Sequence();
            if (canvasGroup != null)
                _showSeq.Join(canvasGroup.DOFade(0f, 0.28f).SetEase(Ease.InCubic));
            _showSeq.OnComplete(() =>
            {
                if (!_pendingUi && canvasGroup != null)
                {
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.alpha = 0f;
                }
                if (!_pendingUi)
                    PositionBelowTopBar();
            });
        }
    }

    void ApplyVisuals(DataManager dm)
    {
        if (dm == null) return;
        float cost = dm.PendingHqMoveCostPoints;
        float gauge = dm.TravelGaugePoints;
        float fill = cost > 1e-4f ? Mathf.Clamp01(gauge / cost) : 1f;
        if (gaugeFill != null)
            gaugeFill.fillAmount = fill;

        string fromId = dm.HomeCastleId?.Trim() ?? "";
        string toId = dm.PendingHqMoveTargetId?.Trim() ?? "";
        string fromName = string.IsNullOrEmpty(fromId) ? "—" : dm.GetCastleDisplayName(fromId);
        string toName = string.IsNullOrEmpty(toId) ? "—" : dm.GetCastleDisplayName(toId);
        if (string.IsNullOrWhiteSpace(fromName)) fromName = fromId;
        if (string.IsNullOrWhiteSpace(toName)) toName = toId;

        float remainPt = Mathf.Max(0f, cost - gauge);
        int remainSteps = dm.GetTravelCostStepEquivalent(remainPt);

        if (statusText != null)
        {
            statusText.richText = true;
            statusText.text =
                $"<b>이동 중:</b> {fromName} → {toName}   <color=#aab6c4>(잔여: {remainSteps:N0}보)</color>";
        }

        var gm = GameManager.InstanceOrNull;
        int haveMp = gm?.currentUser != null ? Mathf.Max(0, gm.currentUser.marchPoints) : 0;
        bool canSpendMp = remainPt > 1e-3f && haveMp > 0;
        if (mpSpendMarchPointsButton != null)
            mpSpendMarchPointsButton.interactable = canSpendMp;
    }

    void OnSpendMarchPointsClicked()
    {
        var dm = DataManager.InstanceOrNull;
        var gm = GameManager.InstanceOrNull;
        if (dm == null || gm?.currentUser == null || !dm.HasPendingHqMove)
            return;

        float remain = Mathf.Max(0f, dm.PendingHqMoveCostPoints - dm.TravelGaugePoints);
        if (remain <= 1e-3f) return;

        int need = Mathf.CeilToInt(remain);
        int have = Mathf.Max(0, gm.currentUser.marchPoints);
        int req = Mathf.Min(need, have);
        if (req <= 0) return;

        dm.TrySpendMarchPointsForPendingHqMove(req, out _);
    }

    void OnRefundClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.HasPendingHqMove)
            return;
        dm.TryRefundMarchInjectionChunk(RefundChunk, out _);
    }

    void ShowArrivalPopup(string targetCastleId)
    {
        EnsureArrivalUi();
        if (arrivalPopupRoot == null || arrivalBodyText == null) return;

        var dm = DataManager.InstanceOrNull;
        string name = dm != null ? dm.GetCastleDisplayName(targetCastleId) : targetCastleId;
        if (string.IsNullOrWhiteSpace(name)) name = targetCastleId;
        arrivalBodyText.text = $"본영이 <b>{name}</b>(으)로 이전했습니다.";
        arrivalPopupRoot.gameObject.SetActive(true);
        arrivalPopupRoot.SetAsLastSibling();
    }

    void EnsureArrivalUi()
    {
        if (arrivalPopupRoot != null) return;
        var canvas = GetComponentInParent<Canvas>();
        var parent = canvas != null ? canvas.transform : transform.parent;
        var overlay = new GameObject("MarchArrivalOverlay", typeof(RectTransform), typeof(Image));
        var ort = overlay.GetComponent<RectTransform>();
        ort.SetParent(parent, false);
        ort.SetAsLastSibling();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.5f);
        dim.raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.SetParent(ort, false);
        prt.sizeDelta = new Vector2(560f, 220f);
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        var tmpGo = new GameObject("Body", typeof(RectTransform));
        tmpGo.transform.SetParent(prt, false);
        var body = tmpGo.AddComponent<TextMeshProUGUI>();
        body.fontSize = 26f;
        body.alignment = TextAlignmentOptions.Center;
        body.color = Color.white;
        var bodyRt = tmpGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0.35f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(24f, 0f);
        bodyRt.offsetMax = new Vector2(-24f, -20f);

        var btnGo = new GameObject("Ok", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(prt, false);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 36f);
        brt.sizeDelta = new Vector2(200f, 56f);
        btnGo.GetComponent<Image>().color = new Color(0.25f, 0.48f, 0.82f, 1f);
        var okBtn = btnGo.GetComponent<Button>();

        arrivalPopupRoot = ort;
        arrivalBodyText = body;
        arrivalCloseButton = okBtn;
        okBtn.onClick.AddListener(() =>
        {
            if (arrivalPopupRoot != null)
                arrivalPopupRoot.gameObject.SetActive(false);
        });
        ort.gameObject.SetActive(false);
    }

    void BuildUiIfNeeded()
    {
        if (stripRoot != null) return;

        stripRoot = transform as RectTransform;
        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.09f, 0.94f);
        bg.raycastTarget = true;

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var rootV = gameObject.GetComponent<VerticalLayoutGroup>();
        if (rootV == null) rootV = gameObject.AddComponent<VerticalLayoutGroup>();
        rootV.padding = new RectOffset(14, 14, 8, 8);
        rootV.spacing = 6;
        rootV.childControlWidth = true;
        rootV.childForceExpandWidth = true;
        rootV.childAlignment = TextAnchor.UpperLeft;

        var row0 = new GameObject("StatusRow", typeof(RectTransform), typeof(LayoutElement));
        row0.transform.SetParent(transform, false);
        row0.GetComponent<LayoutElement>().minHeight = 22f;
        statusText = row0.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            statusText.font = TMP_Settings.defaultFontAsset;
        statusText.fontSize = 15;
        statusText.fontStyle = FontStyles.Bold;
        statusText.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        statusText.alignment = TextAlignmentOptions.Left;

        var row1 = new GameObject("GaugeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row1.transform.SetParent(transform, false);
        var h = row1.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childForceExpandWidth = true;
        row1.GetComponent<LayoutElement>().minHeight = 36f;

        var barHost = new GameObject("GaugeBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barHost.transform.SetParent(row1.transform, false);
        barHost.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        barHost.GetComponent<LayoutElement>().flexibleWidth = 1f;
        barHost.GetComponent<LayoutElement>().minHeight = 28f;
        var barRt = barHost.GetComponent<RectTransform>();

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barHost.transform, false);
        gaugeFill = fillGo.GetComponent<Image>();
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.color = new Color(0.95f, 0.96f, 1f, 0.95f);
        gaugeFill.fillAmount = 0f;
        var fillRt = fillGo.GetComponent<RectTransform>();
        StretchRect(fillRt);

        var glow = fillGo.AddComponent<Outline>();
        glow.effectColor = new Color(1f, 1f, 1f, 0.55f);
        glow.effectDistance = new Vector2(2f, -2f);

        var row2 = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row2.transform.SetParent(transform, false);
        var h2 = row2.GetComponent<HorizontalLayoutGroup>();
        h2.spacing = 8;
        h2.childAlignment = TextAnchor.MiddleCenter;
        h2.childControlWidth = false;
        h2.childForceExpandWidth = false;
        row2.GetComponent<LayoutElement>().minHeight = 44f;

        mpRefundButton = CreateChipButton(row2.transform, "BtnMpRefund", "−", new Color(0.18f, 0.2f, 0.24f, 1f));
        mpRefundButton.onClick.AddListener(OnRefundClicked);

        mpSpendMarchPointsButton = CreateChipButton(row2.transform, "BtnMpSpend", "MP 사용", new Color(0.42f, 0.32f, 0.08f, 1f));
        mpSpendMarchPointsButton.onClick.AddListener(OnSpendMarchPointsClicked);
        var spendLe = mpSpendMarchPointsButton.GetComponent<LayoutElement>();
        if (spendLe != null)
        {
            spendLe.minWidth = 120f;
            spendLe.preferredWidth = 140f;
        }

        if (mpSpendMarchPointsButton.GetComponent<WorldMarketGoldButtonShimmer>() == null)
            mpSpendMarchPointsButton.gameObject.AddComponent<WorldMarketGoldButtonShimmer>();
    }

    static Button CreateChipButton(Transform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 72f;
        le.preferredWidth = 88f;
        le.minHeight = 40f;
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();
        var tmpGo = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI));
        tmpGo.transform.SetParent(go.transform, false);
        var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        StretchRect(tmpGo.GetComponent<RectTransform>());
        return btn;
    }

    static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
