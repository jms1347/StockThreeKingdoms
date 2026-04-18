using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 모든 씬에서 공통으로 유지되는 상단바/하단 탭바 UI.
/// SingletonLoader에서 프리팹을 Load하면 씬 전환에도 유지됩니다.
/// </summary>
public class GlobalUIManager : Singleton<GlobalUIManager>
{
    [Header("Top Bar")]
    [SerializeField] RectTransform topBarRoot;
    [SerializeField] TextMeshProUGUI userNameText;
    [SerializeField] TextMeshProUGUI totalAssetsText;
    [SerializeField] TextMeshProUGUI soldiersText;
    [Header("일일 유지비(정오)")]
    [SerializeField] TextMeshProUGUI maintenancePreviewText;
    [SerializeField] TextMeshProUGUI maintenanceCountdownText;

    [Header("Bottom Tab Bar (5)")]
    [SerializeField] RectTransform bottomTabRoot;
    [SerializeField] Button homeButton;
    [SerializeField] Button marketButton;
    [SerializeField] Button portfolioButton;
    [SerializeField] Button newsButton;
    [SerializeField] Button ordersButton;

    public event Action<string> TabSelected;

    public RectTransform AssetsTarget => totalAssetsText != null ? totalAssetsText.rectTransform : null;

    [Header("자원 텍스트 색")]
    [SerializeField] Color soldiersTextColor = Color.white;
    [SerializeField] Color assetsPositiveColor = Color.white;
    static readonly Color AssetsDebtColor = new Color(1f, 0f, 0f);

    [Header("탑바 자원 숫자 롤링")]
    [Tooltip("금화·병사가 늘거나 줄 때 탑바 숫자가 보간되는 시간(초). 0이면 즉시 반영.")]
    [FormerlySerializedAs("topBarDecreaseDuration")]
    [SerializeField] float topBarResourceRollDuration = 0.28f;

    Tweener _assetsTween;
    Tweener _soldiersTween;
    double _displayAssets;
    double _displaySoldiers;

    protected override void Awake()
    {
        base.Awake();
        WireTabs();
    }

    void Start()
    {
        StartCoroutine(BindGameManagerTopBarSync());
    }

    void OnDestroy()
    {
        _assetsTween?.Kill();
        _soldiersTween?.Kill();
        var gm = GameManager.InstanceOrNull;
        if (gm != null)
            gm.OnGoldChanged -= OnGameManagerGoldChanged;
    }

    IEnumerator BindGameManagerTopBarSync()
    {
        while (GameManager.InstanceOrNull == null)
            yield return null;

        var gm = GameManager.InstanceOrNull;
        gm.OnGoldChanged -= OnGameManagerGoldChanged;
        gm.OnGoldChanged += OnGameManagerGoldChanged;
        RefreshTopBarFromGameManager();
    }

    void OnGameManagerGoldChanged(double _)
    {
        RefreshTopBarFromGameManager();
    }

    /// <summary>GameManager 현재 값으로 상단 자원 숫자를 맞춥니다.</summary>
    public void RefreshTopBarFromGameManager()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        var dm = DataManager.InstanceOrNull;
        long soldiers = dm != null && dm.IsStateReady
            ? UserPortfolioManager.GetTotalOwnedSoldiers(dm)
            : gm.currentUser.soldierCount;
        SetTopBarNumbers(gm.currentUser.userName, gm.currentGold, soldiers);
        RefreshMaintenanceHudFromEconomy();
    }

    void WireTabs()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => TabSelected?.Invoke("Home"));
        if (marketButton != null) marketButton.onClick.AddListener(() => TabSelected?.Invoke("Market"));
        if (portfolioButton != null) portfolioButton.onClick.AddListener(() => TabSelected?.Invoke("Portfolio"));
        if (newsButton != null) newsButton.onClick.AddListener(() => TabSelected?.Invoke("News"));
        if (ordersButton != null) ordersButton.onClick.AddListener(() => TabSelected?.Invoke("Orders"));
    }

    /// <summary>다음 정산 예정액·카운트다운 텍스트 갱신.</summary>
    public void RefreshMaintenanceHudFromEconomy()
    {
        double amt = EconomyManager.InstanceOrNull != null
            ? EconomyManager.InstanceOrNull.ComputeNextSettlementGold()
            : 0d;
        if (maintenancePreviewText != null)
            maintenancePreviewText.text = $"다음 정산 예정: {Utils.AbbreviateScore(amt)} G";
        if (maintenanceCountdownText != null)
            maintenanceCountdownText.text = $"정산까지: {EconomyManager.FormatCountdownUntilNextLocalNoon()}";
    }

    public void SetTopBar(string userName, string totalAssets, string soldiersLine)
    {
        if (userNameText != null) userNameText.text = FormatUserNameWithHomeCastle(userName);
        if (totalAssetsText != null)
        {
            totalAssetsText.text = totalAssets;
            totalAssetsText.color = assetsPositiveColor;
        }

        if (soldiersText != null)
        {
            soldiersText.text = soldiersLine;
            soldiersText.color = soldiersTextColor;
        }
    }

    public void SetTopBarNumbers(string userName, double totalAssets, long soldiers)
    {
        if (userNameText != null) userNameText.text = FormatUserNameWithHomeCastle(userName);

        ApplyTopBarField(ref _assetsTween, () => _displayAssets, v => _displayAssets = v, totalAssets, v =>
        {
            if (totalAssetsText != null)
            {
                totalAssetsText.text = FormatCompact(v);
                totalAssetsText.color = v < 0d ? AssetsDebtColor : assetsPositiveColor;
            }
        });

        ApplyTopBarField(ref _soldiersTween, () => _displaySoldiers, v => _displaySoldiers = v, soldiers, v =>
        {
            if (soldiersText != null)
            {
                soldiersText.text = $"{FormatCompact(v)}명";
                soldiersText.color = soldiersTextColor;
            }
        });
    }

    void ApplyTopBarField(ref Tweener tweenRef, Func<double> getDisplay, Action<double> setDisplay, double target,
        Action<double> applyFormatted)
    {
        if (applyFormatted == null || getDisplay == null || setDisplay == null) return;

        const double eps = 0.5;
        tweenRef?.Kill();
        tweenRef = null;

        double current = getDisplay();
        if (Math.Abs(current - target) < eps)
        {
            setDisplay(target);
            applyFormatted(target);
            return;
        }

        if (topBarResourceRollDuration > 0f)
        {
            double start = current;
            tweenRef = DOVirtual.Float(0f, 1f, topBarResourceRollDuration, u =>
            {
                float uu = Mathf.Clamp01(u);
                double v = start + (target - start) * uu;
                setDisplay(v);
                applyFormatted(v);
            }).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() =>
            {
                setDisplay(target);
                applyFormatted(target);
            });
            return;
        }

        setDisplay(target);
        applyFormatted(target);
    }

    static string FormatUserNameWithHomeCastle(string userName)
    {
        string n = userName ?? "";
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return n;
        string hid = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(hid)) return n;
        string cn = dm.GetCastleDisplayName(hid);
        if (string.IsNullOrWhiteSpace(cn)) cn = hid;
        return $"{n} · 본영 {cn}";
    }

    static string FormatCompact(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
        bool neg = value < 0d;
        double av = Math.Abs(value);
        string core;
        if (av < 1000d) core = Math.Round(av).ToString("0");
        else if (av < 1_000_000d) core = (av / 1_000d).ToString("0.#") + "K";
        else if (av < 1_000_000_000d) core = (av / 1_000_000d).ToString("0.#") + "M";
        else if (av < 1_000_000_000_000d) core = (av / 1_000_000_000d).ToString("0.#") + "G";
        else core = (av / 1_000_000_000_000d).ToString("0.#") + "T";

        return neg ? "-" + core : core;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void PunchAssetsText(float strength = 0.12f, float duration = 0.22f, int vibrato = 6)
    {
        if (totalAssetsText == null) return;
        var rt = totalAssetsText.rectTransform;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOPunchScale(Vector3.one * strength, duration, vibrato, 0.5f).SetUpdate(true);
    }
}
