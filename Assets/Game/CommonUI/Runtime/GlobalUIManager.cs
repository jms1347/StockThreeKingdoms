using System;
using UnityEngine;
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
    [SerializeField] TextMeshProUGUI foodText;
    [SerializeField] TextMeshProUGUI soldiersText;

    [Header("Bottom Tab Bar (5)")]
    [SerializeField] RectTransform bottomTabRoot;
    [SerializeField] Button homeButton;
    [SerializeField] Button marketButton;
    [SerializeField] Button portfolioButton;
    [SerializeField] Button newsButton;
    [SerializeField] Button ordersButton;

    public event Action<string> TabSelected;

    public RectTransform AssetsTarget => totalAssetsText != null ? totalAssetsText.rectTransform : null;
    public RectTransform FoodTarget => foodText != null ? foodText.rectTransform : null;
    public RectTransform SoldiersTarget => soldiersText != null ? soldiersText.rectTransform : null;

    [Header("Top Bar Rolling")]
    [SerializeField] float rollDuration = 0.42f;
    Tweener _assetsTween;
    Tweener _foodTween;
    Tweener _soldiersTween;
    double _displayAssets;
    double _displayFood;
    double _displaySoldiers;

    protected override void Awake()
    {
        base.Awake();
        WireTabs();
    }

    void WireTabs()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => TabSelected?.Invoke("Home"));
        if (marketButton != null) marketButton.onClick.AddListener(() => TabSelected?.Invoke("Market"));
        if (portfolioButton != null) portfolioButton.onClick.AddListener(() => TabSelected?.Invoke("Portfolio"));
        if (newsButton != null) newsButton.onClick.AddListener(() => TabSelected?.Invoke("News"));
        if (ordersButton != null) ordersButton.onClick.AddListener(() => TabSelected?.Invoke("Orders"));
    }

    public void SetTopBar(string userName, string totalAssets, string food)
    {
        if (userNameText != null) userNameText.text = userName;
        if (totalAssetsText != null) totalAssetsText.text = totalAssets;
        if (foodText != null) foodText.text = food;
    }

    public void SetTopBar(string userName, string totalAssets, string food, string soldiers)
    {
        SetTopBar(userName, totalAssets, food);
        if (soldiersText != null) soldiersText.text = soldiers;
    }

    public void SetTopBarNumbers(string userName, double totalAssets, double food, long soldiers)
    {
        if (userNameText != null) userNameText.text = userName ?? "";
        RollNumber(ref _assetsTween, _displayAssets, totalAssets, v => _displayAssets = v, v =>
        {
            if (totalAssetsText != null) totalAssetsText.text = FormatCompact(v);
        });
        RollNumber(ref _foodTween, _displayFood, food, v => _displayFood = v, v =>
        {
            if (foodText != null) foodText.text = FormatCompact(v);
        });
        RollNumber(ref _soldiersTween, _displaySoldiers, soldiers, v => _displaySoldiers = v, v =>
        {
            if (soldiersText != null) soldiersText.text = $"{FormatCompact(v)}명";
        });
    }

    void RollNumber(ref Tweener t, double display, double target, Action<double> setDisplay, Action<double> apply)
    {
        if (apply == null || setDisplay == null) return;
        if (rollDuration <= 0f)
        {
            setDisplay(target);
            apply(target);
            return;
        }

        t?.Kill();
        double start = display;
        t = DOVirtual.Float(0f, 1f, rollDuration, u =>
        {
            float uu = Mathf.Clamp01(u);
            double v = start + (target - start) * uu;
            setDisplay(v);
            apply(v);
        }).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    static string FormatCompact(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
        double abs = Math.Abs(value);

        if (abs < 1000d) return Math.Round(value).ToString("0");
        if (abs < 1_000_000d) return (value / 1_000d).ToString("0.#") + "K";
        if (abs < 1_000_000_000d) return (value / 1_000_000d).ToString("0.#") + "M";
        if (abs < 1_000_000_000_000d) return (value / 1_000_000_000d).ToString("0.#") + "G";
        return (value / 1_000_000_000_000d).ToString("0.#") + "T";
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>비행 수거 아이콘이 금화(자산) 텍스트에 도착했을 때 펀치 연출.</summary>
    public void PunchAssetsText(float strength = 0.12f, float duration = 0.22f, int vibrato = 6)
    {
        if (totalAssetsText == null) return;
        var rt = totalAssetsText.rectTransform;
        rt.DOKill();
        rt.localScale = Vector3.one; // 이전 펀치가 중단돼도 원상복구
        rt.DOPunchScale(Vector3.one * strength, duration, vibrato, 0.5f).SetUpdate(true);
    }

    /// <summary>비행 수거 아이콘이 식량 텍스트에 도착했을 때 펀치 연출.</summary>
    public void PunchFoodText(float strength = 0.12f, float duration = 0.22f, int vibrato = 6)
    {
        if (foodText == null) return;
        var rt = foodText.rectTransform;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOPunchScale(Vector3.one * strength, duration, vibrato, 0.5f).SetUpdate(true);
    }
}

