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
    [Tooltip("중앙: 현재 거점 위치(본영 성 이름 등)")]
    [SerializeField] TextMeshProUGUI locationText;
    [SerializeField] Image locationNationIcon;
    [SerializeField] TextMeshProUGUI totalAssetsText;
    [SerializeField] TextMeshProUGUI soldiersText;
    [Header("일일 유지비(정오)")]
    [SerializeField] TextMeshProUGUI maintenancePreviewText;
    [SerializeField] TextMeshProUGUI maintenanceCountdownText;

    [Header("프로필 (좌측)")]
    [Tooltip("장착 캐릭터 초상.")]
    [SerializeField] Image userPortraitImage;
    [SerializeField] Image titleBadgeBackground;
    [SerializeField] TextMeshProUGUI titleBadgeText;
    [SerializeField] Outline titleBadgeOutline;
    [SerializeField] Outline avatarPortraitOutline;

    [Header("우측: 행군 MP")]
    [FormerlySerializedAs("foodText")]
    [SerializeField] TextMeshProUGUI marchPointsText;
    [SerializeField] Color marchPointsTextColor = Color.white;

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
    [SerializeField] Color soldiersTextColor = new Color(0.7f, 1f, 0.75f);
    [SerializeField] Color assetsPositiveColor = new Color(0.96f, 0.88f, 0.35f);
    static readonly Color AssetsDebtColor = new Color(1f, 0f, 0f);

    [Header("탑바 자원 숫자 롤링")]
    [Tooltip("금화·병사·MP가 변할 때 숫자 보간 시간(초). 0이면 즉시.")]
    [FormerlySerializedAs("topBarDecreaseDuration")]
    [SerializeField] float topBarResourceRollDuration = 0.28f;

    Tweener _assetsTween;
    Tweener _soldiersTween;
    Tweener _mpTween;
    double _displayAssets;
    double _displaySoldiers;
    int _displayMp;

    protected override void Awake()
    {
        base.Awake();
        FixGlobalUiScaleIfBroken();
        EnsureGlobalCanvasFillsScreen();
        EnsureTopBarAndScalerLayout();
        ResolveTopBarRefsIfMissing();
        WireTabs();
        ApplyBottomTabLabelAutoSizing();
        if (topBarRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(topBarRoot);
    }

    void ApplyBottomTabLabelAutoSizing()
    {
        if (bottomTabRoot == null) return;
        foreach (var btn in bottomTabRoot.GetComponentsInChildren<Button>(true))
        {
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) continue;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14;
            tmp.fontSizeMax = 26;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }

    void Start()
    {
        StartCoroutine(BindGameManagerTopBarSync());
    }

    void OnDestroy()
    {
        _assetsTween?.Kill();
        _soldiersTween?.Kill();
        _mpTween?.Kill();
        var gm = GameManager.InstanceOrNull;
        if (gm != null)
        {
            gm.OnGoldChanged -= OnGameManagerGoldChanged;
            gm.OnMarchPointsChanged -= OnMarchPointsChangedHandler;
        }
    }

    IEnumerator BindGameManagerTopBarSync()
    {
        while (GameManager.InstanceOrNull == null)
            yield return null;

        var gm = GameManager.InstanceOrNull;
        gm.OnGoldChanged -= OnGameManagerGoldChanged;
        gm.OnGoldChanged += OnGameManagerGoldChanged;
        gm.OnMarchPointsChanged -= OnMarchPointsChangedHandler;
        gm.OnMarchPointsChanged += OnMarchPointsChangedHandler;
        RefreshTopBarFromGameManager();
        InitMarchPointsDisplayFromSave();
    }

    void OnGameManagerGoldChanged(double _) => RefreshTopBarFromGameManager();

    void OnMarchPointsChangedHandler(int v) => RollMarchPointsTo(v);

    /// <summary>GameManager 현재 값으로 상단바를 맞춥니다.</summary>
    public void RefreshTopBarFromGameManager()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        var dm = DataManager.InstanceOrNull;
        long soldiers = dm != null && dm.IsStateReady
            ? UserPortfolioManager.GetTotalOwnedSoldiers(dm)
            : gm.currentUser.soldierCount;
        SetTopBarNumbers(gm.currentGold, soldiers);
        RefreshLocationHud();
        RefreshMaintenanceHudFromEconomy();
    }

    void WireTabs()
    {
        ResolveBottomTabButtonsIfMissing();

        if (homeButton != null) homeButton.onClick.AddListener(() => TabSelected?.Invoke("Home"));
        if (marketButton != null) marketButton.onClick.AddListener(() => TabSelected?.Invoke("Market"));
        if (portfolioButton != null) portfolioButton.onClick.AddListener(() => TabSelected?.Invoke("Portfolio"));
        if (newsButton != null) newsButton.onClick.AddListener(() => TabSelected?.Invoke("News"));
        if (ordersButton != null) ordersButton.onClick.AddListener(() => TabSelected?.Invoke("Orders"));
    }

    void ResolveBottomTabButtonsIfMissing()
    {
        if (bottomTabRoot == null)
            return;

        if (homeButton == null) homeButton = bottomTabRoot.Find("HomeTabButton")?.GetComponent<Button>();
        if (marketButton == null) marketButton = bottomTabRoot.Find("MarketTabButton")?.GetComponent<Button>();
        if (portfolioButton == null) portfolioButton = bottomTabRoot.Find("PortfolioTabButton")?.GetComponent<Button>();
        if (newsButton == null) newsButton = bottomTabRoot.Find("NewsTabButton")?.GetComponent<Button>();
        if (ordersButton == null) ordersButton = bottomTabRoot.Find("OrdersTabButton")?.GetComponent<Button>();
    }

    /// <summary>유지비 표시: 실시간 차감 모드 또는 레거시 정오 정산.</summary>
    public void RefreshMaintenanceHudFromEconomy()
    {
        var econ = EconomyManager.InstanceOrNull;
        if (econ != null && !econ.UsesLegacyNoonMaintenance)
        {
            double perSec = EconomyManager.ComputeRealtimeUpkeepGoldPerSecond();
            if (maintenancePreviewText != null)
                maintenancePreviewText.text = $"유지비: -{perSec:F2} G/s";
            if (maintenanceCountdownText != null)
                maintenanceCountdownText.text = "실시간 차감";
            return;
        }

        double amt = econ != null ? econ.ComputeNextSettlementGold() : 0d;
        if (maintenancePreviewText != null)
            maintenancePreviewText.text = $"다음 정산 예정: {Utils.AbbreviateScore(amt)} G";
        if (maintenanceCountdownText != null)
            maintenanceCountdownText.text = $"정산까지: {EconomyManager.FormatCountdownUntilNextLocalNoon()}";
    }

    void RefreshLocationHud()
    {
        if (locationText == null) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady)
        {
            locationText.text = "현재 위치: —";
            if (locationNationIcon != null) locationNationIcon.gameObject.SetActive(false);
            return;
        }
        string id = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            locationText.text = "현재 위치: 본영 미정";
            if (locationNationIcon != null) locationNationIcon.gameObject.SetActive(false);
            return;
        }
        string name = dm.GetCastleDisplayName(id);
        string place = string.IsNullOrWhiteSpace(name) ? id : name;
        locationText.text = $"현재 위치: {place}";
        if (locationNationIcon != null)
        {
            if (dm.castleStateDataMap != null && dm.castleStateDataMap.TryGetValue(id, out var st) && st != null)
            {
                locationNationIcon.gameObject.SetActive(true);
                locationNationIcon.color = FactionAccentColor(st.currentLord);
            }
            else
                locationNationIcon.gameObject.SetActive(false);
        }
    }

    static Color FactionAccentColor(Faction f)
    {
        switch (f)
        {
            case Faction.WEI: return new Color(0.24f, 0.42f, 0.86f, 1f);
            case Faction.SHU: return new Color(0.30f, 0.78f, 0.34f, 1f);
            case Faction.WU: return new Color(0.90f, 0.36f, 0.28f, 1f);
            case Faction.OTHERS: return new Color(0.78f, 0.62f, 0.22f, 1f);
            default: return new Color(0.68f, 0.68f, 0.68f, 1f);
        }
    }

    void ResolveTopBarRefsIfMissing()
    {
        if (topBarRoot == null)
        {
            var t = transform.Find("TopBar");
            if (t != null) topBarRoot = t as RectTransform;
        }
        if (topBarRoot == null) return;

        if (userNameText == null) userNameText = FindTextByName(topBarRoot, "UserNameText", "ProfileNameText");
        if (locationText == null) locationText = FindTextByName(topBarRoot, "LocationText");
        if (totalAssetsText == null) totalAssetsText = FindTextByName(topBarRoot, "AssetsText");
        if (soldiersText == null) soldiersText = FindTextByName(topBarRoot, "SoldiersText");
        if (marchPointsText == null) marchPointsText = FindTextByName(topBarRoot, "MarchPointsText");
        if (maintenancePreviewText == null) maintenancePreviewText = FindTextByName(topBarRoot, "MaintenancePreviewText");
        if (maintenanceCountdownText == null) maintenanceCountdownText = FindTextByName(topBarRoot, "MaintenanceCountdownText");
        if (locationNationIcon == null) locationNationIcon = FindImageByName(topBarRoot, "LocationNationIcon");

        // 프리팹 구조가 달라 이름 매칭이 안 될 때, 행 이름 기반 폴백을 사용합니다.
        if (totalAssetsText == null) totalAssetsText = FindValueTextUnderRow(topBarRoot, "AssetsRow");
        if (soldiersText == null) soldiersText = FindValueTextUnderRow(topBarRoot, "SoldiersRow");
        if (maintenancePreviewText == null) maintenancePreviewText = FindValueTextUnderRow(topBarRoot, "MaintenancePreviewRow");
        if (maintenanceCountdownText == null) maintenanceCountdownText = FindValueTextUnderRow(topBarRoot, "MaintenanceCountdownRow");
    }

    static TextMeshProUGUI FindTextByName(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0) return null;
        var list = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            for (int j = 0; j < list.Length; j++)
            {
                if (list[j] != null && list[j].name == n)
                    return list[j];
            }
        }

        return null;
    }

    static Image FindImageByName(Transform root, params string[] names)
    {
        if (root == null || names == null || names.Length == 0) return null;
        var list = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            for (int j = 0; j < list.Length; j++)
            {
                if (list[j] != null && list[j].name == n)
                    return list[j];
            }
        }

        return null;
    }

    static TextMeshProUGUI FindValueTextUnderRow(Transform root, string rowName)
    {
        if (root == null || string.IsNullOrEmpty(rowName)) return null;
        var row = FindChildByName(root, rowName);
        if (row == null) return null;
        return FindTextByName(row, "ValueText");
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    void FixGlobalUiScaleIfBroken()
    {
        var rt = transform as RectTransform;
        if (rt != null && rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = Vector3.one;

        if (topBarRoot != null && topBarRoot.localScale.sqrMagnitude < 1e-8f)
            topBarRoot.localScale = Vector3.one;
    }

    /// <summary>
    /// 루트가 (0,0) 고정 앵커로 들어가 있으면 레이아웃 그룹·스케일이 기대와 다르게 보일 수 있어,
    /// Screen Space Overlay 표준인 전체 스트레치로 맞춥니다.
    /// </summary>
    void EnsureGlobalCanvasFillsScreen()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// 프리팹 시리얼라이즈만으로는 체감이 어렵거나 에셋이 덮어씌워진 경우를 대비해,
    /// 실행 시점에 스케일러·탑바 높이를 고정합니다.
    /// </summary>
    void EnsureTopBarAndScalerLayout()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (topBarRoot == null)
        {
            var t = transform.Find("TopBar");
            if (t != null) topBarRoot = t as RectTransform;
        }

        if (topBarRoot != null)
        {
            const float topBarHeight = 180f;
            var sd = topBarRoot.sizeDelta;
            topBarRoot.sizeDelta = new Vector2(sd.x, topBarHeight);
        }
    }

    public void SetTopBar(string userName, string totalAssets, string soldiersLine)
    {
        if (userNameText != null) userNameText.text = string.IsNullOrEmpty(userName) ? "—" : userName;
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

    public void SetTopBarNumbers(double totalAssets, long soldiers)
    {
        RefreshProfileHud();
        ApplyTopBarResourceNumbers(totalAssets, soldiers);
    }

    /// <summary>세이브 로드 직후 등 MP 트윈 없이 표시값만 맞출 때 사용.</summary>
    public void InitMarchPointsDisplayFromSave()
    {
        _mpTween?.Kill();
        var u = UserManager.Current;
        if (marchPointsText == null || u == null) return;
        _displayMp = u.marchPoints;
        ApplyMarchPointsText(_displayMp);
    }

    /// <summary>MP 숫자를 DOTween으로 롤링합니다. <see cref="GameManager.AddMarchPoints"/>에서 호출됩니다.</summary>
    public void RollMarchPointsTo(int targetMp)
    {
        if (marchPointsText == null) return;
        _mpTween?.Kill();
        int start = _displayMp;
        if (Mathf.Abs(start - targetMp) < 1)
        {
            _displayMp = targetMp;
            ApplyMarchPointsText(targetMp);
            return;
        }
        if (topBarResourceRollDuration <= 0f)
        {
            _displayMp = targetMp;
            ApplyMarchPointsText(targetMp);
            return;
        }
        _mpTween = DOVirtual.Float(0f, 1f, topBarResourceRollDuration, u =>
        {
            float uu = Mathf.Clamp01(u);
            int v = Mathf.RoundToInt(Mathf.Lerp(start, targetMp, uu));
            _displayMp = v;
            ApplyMarchPointsText(v);
        }).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() =>
        {
            _displayMp = targetMp;
            ApplyMarchPointsText(targetMp);
        });
    }

    void ApplyMarchPointsText(int mp)
    {
        if (marchPointsText == null) return;
        marchPointsText.text = $"{FormatCompactInt(mp)} MP";
        marchPointsText.color = marchPointsTextColor;
    }

    void ApplyTopBarResourceNumbers(double totalAssets, long soldiers)
    {
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

    /// <summary><see cref="UserManager"/>·<see cref="UserData"/> 기준으로 프로필을 갱신합니다.</summary>
    public void RefreshProfileHud()
    {
        var u = UserManager.Current;
        if (u == null) return;

        if (userNameText != null)
            userNameText.text = UserManager.GetNickname();

        string titleId = UserManager.GetRankTitleId();
        string titleDisp = UserManager.GetRankTitleDisplay();
        int tier = PlayerTitleVisuals.ResolveTier(titleId, titleDisp);
        var st = PlayerTitleVisuals.GetStyle(tier);

        if (titleBadgeText != null)
        {
            titleBadgeText.text = titleDisp;
            titleBadgeText.color = st.BadgeText;
        }

        if (titleBadgeBackground != null)
            titleBadgeBackground.color = st.BadgeBackground;

        ApplyOutlineStyle(titleBadgeOutline, st.BadgeOutline, st.BadgeOutlineWidth);
        ApplyOutlineStyle(avatarPortraitOutline, st.AvatarOutline, st.AvatarOutlineWidth);

        if (userPortraitImage != null)
        {
            var sp = UserPortraitLoader.GetPortrait(UserManager.GetEquippedCharacterId());
            userPortraitImage.sprite = sp;
            userPortraitImage.preserveAspect = true;
            userPortraitImage.color = sp != null ? Color.white : new Color(0.25f, 0.28f, 0.32f, 1f);
        }
    }

    static void ApplyOutlineStyle(Outline outline, Color color, float width)
    {
        if (outline == null) return;
        bool on = width > 0.05f;
        outline.enabled = on;
        if (!on) return;
        outline.effectColor = color;
        float w = Mathf.Max(0.5f, width);
        outline.effectDistance = new Vector2(w * 0.65f, -w * 0.65f);
    }

    static string FormatCompactInt(int value)
    {
        double v = value;
        return FormatCompact(v);
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
