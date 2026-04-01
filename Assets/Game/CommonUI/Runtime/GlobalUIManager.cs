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

    [Header("자원 텍스트 색")]
    [SerializeField] Color foodTextColor = new Color(0.42f, 0.92f, 0.48f, 1f);
    [SerializeField] Color soldiersTextColor = Color.white;
    [Header("탑바 자원 숫자 롤링")]
    [Tooltip("금화·식량·병사가 늘거나 줄 때 탑바 숫자가 보간되는 시간(초). 구매 시 금화↓·식량↑ 등 모두 적용. 0이면 즉시 반영.")]
    [FormerlySerializedAs("topBarDecreaseDuration")]
    [SerializeField] float topBarResourceRollDuration = 0.28f;

    Tweener _assetsTween;
    Tweener _foodTween;
    Tweener _soldiersTween;
    double _displayAssets;
    double _displayFood;
    double _displaySoldiers;

    protected override void Awake()
    {
        base.Awake();
        ApplyResourceTextColors();
        WireTabs();
    }

    void Start()
    {
        StartCoroutine(BindGameManagerTopBarSync());
    }

    void OnDestroy()
    {
        _assetsTween?.Kill();
        _foodTween?.Kill();
        _soldiersTween?.Kill();
        var gm = GameManager.InstanceOrNull;
        if (gm != null)
        {
            gm.OnGoldChanged -= OnGameManagerGoldOrGrainChanged;
            gm.OnGrainChanged -= OnGameManagerGoldOrGrainChanged;
        }
    }

    IEnumerator BindGameManagerTopBarSync()
    {
        while (GameManager.InstanceOrNull == null)
            yield return null;

        var gm = GameManager.InstanceOrNull;
        gm.OnGoldChanged -= OnGameManagerGoldOrGrainChanged;
        gm.OnGoldChanged += OnGameManagerGoldOrGrainChanged;
        gm.OnGrainChanged -= OnGameManagerGoldOrGrainChanged;
        gm.OnGrainChanged += OnGameManagerGoldOrGrainChanged;
        RefreshTopBarFromGameManager();
    }

    void OnGameManagerGoldOrGrainChanged(long _)
    {
        RefreshTopBarFromGameManager();
    }

    /// <summary>GameManager 현재 값으로 상단 자원 숫자를 맞춥니다. (홈 UI 비활성 시에도 호출 가능)</summary>
    public void RefreshTopBarFromGameManager()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        SetTopBarNumbers(gm.currentUser.userName, gm.currentGold, gm.currentGrain, gm.currentUser.soldierCount);
    }

    void WireTabs()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => TabSelected?.Invoke("Home"));
        if (marketButton != null) marketButton.onClick.AddListener(() => TabSelected?.Invoke("Market"));
        // 포트폴리오·상점(맨 오른쪽)은 TabSelected 탭 전환에만 연결하지 않음
        if (newsButton != null) newsButton.onClick.AddListener(() => TabSelected?.Invoke("News"));
    }

    void ApplyResourceTextColors()
    {
        if (foodText != null) foodText.color = foodTextColor;
        if (soldiersText != null) soldiersText.color = soldiersTextColor;
    }

    public void SetTopBar(string userName, string totalAssets, string food)
    {
        if (userNameText != null) userNameText.text = FormatUserNameWithHomeCastle(userName);
        if (totalAssetsText != null) totalAssetsText.text = totalAssets;
        if (foodText != null)
        {
            foodText.text = food;
            foodText.color = foodTextColor;
        }
    }

    public void SetTopBar(string userName, string totalAssets, string food, string soldiers)
    {
        SetTopBar(userName, totalAssets, food);
        if (soldiersText != null)
        {
            soldiersText.text = soldiers;
            soldiersText.color = soldiersTextColor;
        }
    }

    public void SetTopBarNumbers(string userName, double totalAssets, double food, long soldiers)
    {
        if (userNameText != null) userNameText.text = FormatUserNameWithHomeCastle(userName);
        ApplyResourceTextColors();

        ApplyTopBarField(ref _assetsTween, () => _displayAssets, v => _displayAssets = v, totalAssets, v =>
        {
            if (totalAssetsText != null) totalAssetsText.text = FormatCompact(v);
        });

        ApplyTopBarField(ref _foodTween, () => _displayFood, v => _displayFood = v, food, v =>
        {
            if (foodText != null)
            {
                foodText.text = FormatCompact(v);
                foodText.color = foodTextColor;
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

    /// <summary>표시값이 목표와 다르면 증가·감소 모두 짧게 롤링합니다(구매 시 금화↓·식량/병사↑ 포함).</summary>
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

