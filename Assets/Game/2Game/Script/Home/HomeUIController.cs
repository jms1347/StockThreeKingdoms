using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 본영 화면 담당. GameManager.OnGoldChanged/OnGrainChanged 구독, 코루틴으로 창고 UI 갱신.
/// </summary>
[RequireComponent(typeof(HomeController))]
public class HomeUIController : MonoBehaviour
{
    [Header("자원 표시")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI grainText;
    [Tooltip("천하 거점에 배치한 병력 합계(본영에서는 모집 불가).")]
    public TextMeshProUGUI farmWorkersText;

    [Header("업그레이드 UI - 노동력")]
    public TextMeshProUGUI laborLabelText;
    public Button laborUpgradeButton;

    [Header("업그레이드 UI - 시장")]
    public TextMeshProUGUI marketLabelText;
    public TextMeshProUGUI marketAccumulateText;
    public Slider marketAccumulateSlider;
    public Button marketUpgradeButton;
    public Button collectMarketButton;

    [Header("업그레이드 UI - 농장")]
    public TextMeshProUGUI farmLabelText;
    public TextMeshProUGUI farmAccumulateText;
    public Slider farmAccumulateSlider;
    public Button farmUpgradeButton;
    public Button collectFarmButton;

    [Header("보급 UI")]
    public TextMeshProUGUI supplyLabelText;
    [Tooltip("구버전 병사 모집 버튼 슬롯. 런타임에서 숨깁니다.")]
    public Button hireFarmWorkerButton;
    public Button buyGrainButton;

    [Header("대문 터치")]
    public Button gateButton;

    [Header("만보기")]
    public Image pedometerGaugeFill;
    public TextMeshProUGUI pedometerStepsText;
    [Tooltip("2k, 5k, 7k, 10k 순서")]
    public Button[] stepRewardButtons = new Button[4];
    public TextMeshProUGUI[] stepRewardLabels = new TextMeshProUGUI[4];

    [Header("창고 연출")]
    public CollectionManager collectionManager;

    [Header("숫자 롤링")]
    public float resourceRollDuration = 0.42f;

    private HomeController _controller;
    long _displayGold;
    long _displayGrain;
    Tweener _goldRollTween;
    Tweener _grainRollTween;

    RectTransform _grainDialogRoot;
    Slider _grainSlider;
    TextMeshProUGUI _grainValueText;
    Button _grainConfirmButton;
    Button _grainCancelButton;
    int _grainMaxThisOpen;

    bool _supplyDialogsBuilt;

    void Awake()
    {
        _controller = GetComponent<HomeController>();
        if (collectionManager == null)
            collectionManager = GetComponent<CollectionManager>();
    }

    void OnEnable()
    {
        // 탭 전환으로 비활성화될 때 OnDisable에서 구독 해제됨. Start()는 한 번만 실행되므로
        // 홈으로 돌아올 때 반드시 여기서 다시 구독해야 상단 글로벌 탑바가 갱신됨.
        SubscribeEvents();
        SubscribeStepEvents();
        PushGlobalTopBar();
    }

    void Start()
    {
        if (_controller == null) return;

        // gateButton 참조가 Inspector에서 빠진 경우 자동 탐색
        if (gateButton == null)
            gateButton = transform.Find("GateButton")?.GetComponent<Button>();

        FixHomeCanvasScaleIfBroken();
        EnsureUiInputInfrastructure();

        // OnEnable보다 GameManager가 늦게 생기는 경우 1회 보강
        SubscribeEvents();
        SubscribeStepEvents();

        RefreshAllUI();
        BindButtons();

        StartCoroutine(UpdateAccumulateUICoroutine());
    }

    void OnDestroy()
    {
        _goldRollTween?.Kill();
        _grainRollTween?.Kill();
    }

    void OnDisable()
    {
        UnsubscribeStepEvents();
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged -= OnGoldChangedHandler;
        gm.OnGoldChanged += OnGoldChangedHandler;
        gm.OnGrainChanged -= OnGrainChangedHandler;
        gm.OnGrainChanged += OnGrainChangedHandler;
    }

    void UnsubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged -= OnGoldChangedHandler;
        gm.OnGrainChanged -= OnGrainChangedHandler;
    }

    void SubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged -= OnStepsTodayChangedHandler;
        gm.OnStepsChanged += OnStepsTodayChangedHandler;
    }

    void UnsubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged -= OnStepsTodayChangedHandler;
    }

    void OnGateButtonClickFallback()
    {
        if (gateButton == null) return;
        gateButton.GetComponent<GateButtonHold>()?.OnGateTapFromButton();
    }

    /// <summary>EventSystem·GraphicRaycaster 누락 시 UI 클릭/터치가 전부 무시될 수 있어 보강합니다.</summary>
    void EnsureUiInputInfrastructure()
    {
        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 프리팹 실수로 Canvas 루트 <c>localScale</c>이 0이면 UI가 보여도 히트 박스가 0이라 버튼이 전부 죽습니다.
    /// </summary>
    void FixHomeCanvasScaleIfBroken()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var rt = canvas.transform as RectTransform;
        if (rt == null) return;
        if (rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = Vector3.one;
    }

    void OnStepsTodayChangedHandler(int _) => RefreshPedometerNow();

    void OnGoldChangedHandler(long gold)
    {
        RollGoldDisplay(gold);
        // 상단 탑바는 GlobalUIManager가 GameManager 이벤트로 갱신 (중복 SetTopBarNumbers 시 롤링 트윈이 꼬일 수 있음)
        UpdateSupplyUI();
    }

    void OnGrainChangedHandler(long grain)
    {
        RollGrainDisplay(grain);
    }

    System.Collections.IEnumerator UpdateAccumulateUICoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            if (_controller == null || GameManager.InstanceOrNull == null) continue;

            double mAcc = _controller.CurrentMarketAccumulated;
            double mMax = _controller.GetMarketMaxCapacity();
            if (collectionManager != null && collectionManager.IsFlyBusy) mAcc = 0;
            if (marketAccumulateText != null)
            {
                marketAccumulateText.text = mMax > 0 ? $"{mAcc:F0} / {mMax:F0}" : "0 / 0";
                marketAccumulateText.color = (mMax > 0 && mAcc >= mMax) ? Color.red : Color.white;
            }
            if (marketAccumulateSlider != null && mMax > 0)
                marketAccumulateSlider.value = (float)Math.Min(1.0, mAcc / mMax);

            double fAcc = _controller.CurrentFarmAccumulated;
            double fMax = _controller.GetFarmMaxCapacity();
            if (collectionManager != null && collectionManager.IsFlyBusy) fAcc = 0;
            if (farmAccumulateText != null)
            {
                farmAccumulateText.text = fMax > 0 ? $"{fAcc:F0} / {fMax:F0}" : "0 / 0";
                farmAccumulateText.color = (fMax > 0 && fAcc >= fMax) ? Color.red : Color.white;
            }
            if (farmAccumulateSlider != null && fMax > 0)
                farmAccumulateSlider.value = (float)Math.Min(1.0, fAcc / fMax);

            RefreshPedometerUI();
        }
    }

    void BindButtons()
    {
        if (buyGrainButton == null)
            buyGrainButton = transform.Find("SupplyPanel/SupplyButtons/BuyGrainButton")?.GetComponent<Button>();

        if (gateButton == null)
        {
            Debug.LogWarning("[HomeUIController] gateButton이 연결되지 않았습니다. Inspector에서 GateButton을 할당하세요.");
        }
        else
        {
            var hold = gateButton.GetComponent<GateButtonHold>();
            if (hold == null) hold = gateButton.gameObject.AddComponent<GateButtonHold>();
            hold.controller = _controller;
            hold.collectionManager = collectionManager;
            gateButton.onClick.RemoveListener(OnGateButtonClickFallback);
            gateButton.onClick.AddListener(OnGateButtonClickFallback);
        }
        WireHoldRepeat(laborUpgradeButton, () =>
        {
            _controller?.UpgradeLabor();
            UpdateLaborUI();
            UpdateSupplyUI();
        });
        WireHoldRepeat(marketUpgradeButton, () =>
        {
            _controller?.UpgradeMarket();
            DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
            UpdateMarketUI();
            UpdateSupplyUI();
        });
        if (collectMarketButton != null)
            collectMarketButton.onClick.AddListener(() =>
                _controller?.TryFlyCollectFromWarehouse(collectionManager, requireActivePiles: false));
        WireHoldRepeat(farmUpgradeButton, () =>
        {
            _controller?.UpgradeFarm();
            DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
            UpdateFarmUI();
            UpdateSupplyUI();
        });
        if (hireFarmWorkerButton != null)
            hireFarmWorkerButton.gameObject.SetActive(false);

        if (buyGrainButton != null)
        {
            buyGrainButton.onClick.RemoveAllListeners();
            var hr = buyGrainButton.GetComponent<ButtonHoldRepeat>();
            if (hr != null)
                Destroy(hr);
            buyGrainButton.onClick.AddListener(OpenGrainDialog);
        }

        if (stepRewardButtons != null && _controller != null)
        {
            for (int i = 0; i < stepRewardButtons.Length; i++)
            {
                if (stepRewardButtons[i] == null) continue;
                int idx = i;
                stepRewardButtons[i].onClick.AddListener(() =>
                {
                    if (_controller.ClaimStepReward(idx))
                    {
                        RefreshPedometerUI();
                        UpdateGrainUI(GameManager.InstanceOrNull?.currentGrain ?? 0, instant: true);
                    }
                });
            }
        }
    }

    /// <summary>탭 1회 + 길게 누르면 재화가 될 때까지 반복 (대문 홀드와 유사 UX).</summary>
    void WireHoldRepeat(Button btn, Action tick)
    {
        if (btn == null || tick == null) return;
        btn.onClick.RemoveAllListeners();
        var hr = btn.GetComponent<ButtonHoldRepeat>() ?? btn.gameObject.AddComponent<ButtonHoldRepeat>();
        hr.Configure(tick);
    }

    void RefreshAllUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        UpdateGoldUI(gm.currentGold, instant: true);
        UpdateGrainUI(gm.currentGrain, instant: true);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        UpdateLaborUI();
        UpdateMarketUI();
        UpdateFarmUI();
        UpdateSupplyUI();
        RefreshPedometerUI();
    }

    void RollGoldDisplay(long target)
    {
        if (goldText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
        _goldRollTween?.Kill();
        long start = _displayGold;
        float p = 0f;
        _goldRollTween = DOTween.To(() => p, x =>
        {
            p = x;
            float u = Mathf.Clamp01(x);
            _displayGold = (long)(start + (target - start) * (double)u);
            goldText.text = _displayGold.ToString("N0");
        }, 1f, resourceRollDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    void RollGrainDisplay(long target)
    {
        if (grainText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
        _grainRollTween?.Kill();
        long start = _displayGrain;
        float p = 0f;
        _grainRollTween = DOTween.To(() => p, x =>
        {
            p = x;
            float u = Mathf.Clamp01(x);
            _displayGrain = (long)(start + (target - start) * (double)u);
            grainText.text = _displayGrain.ToString("N0");
        }, 1f, resourceRollDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    void UpdateGoldUI(long gold, bool instant)
    {
        if (goldText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
        if (instant)
        {
            _goldRollTween?.Kill();
            _displayGold = gold;
            goldText.text = gold.ToString("N0");
        }
        else
            RollGoldDisplay(gold);
    }

    void UpdateGrainUI(long grain, bool instant)
    {
        if (grainText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
        if (instant)
        {
            _grainRollTween?.Kill();
            _displayGrain = grain;
            grainText.text = grain.ToString("N0");
        }
        else
            RollGrainDisplay(grain);
    }

    /// <summary>만보기 텍스트·게이지·버튼을 즉시 갱신 (에디터 테스트용 등)</summary>
    public void RefreshPedometerNow() => RefreshPedometerUI();

    void RefreshPedometerUI()
    {
        var u = GameManager.InstanceOrNull?.currentUser;
        if (u == null) return;

        int steps = u.stepsToday;
        if (pedometerGaugeFill != null)
            pedometerGaugeFill.fillAmount = Mathf.Clamp01(steps / 10000f);
        if (pedometerStepsText != null)
            pedometerStepsText.text = $"{steps:N0} / 10,000";

        if (u.stepRewardsClaimed == null || u.stepRewardsClaimed.Length != HomeController.StepMilestones.Length)
            u.stepRewardsClaimed = new bool[HomeController.StepMilestones.Length];

        for (int i = 0; i < HomeController.StepMilestones.Length && i < stepRewardButtons.Length; i++)
        {
            var btn = stepRewardButtons[i];
            if (btn == null) continue;
            bool claimed = u.stepRewardsClaimed[i];
            bool canClaim = steps >= HomeController.StepMilestones[i] && !claimed;
            btn.interactable = canClaim;

            if (i < stepRewardLabels.Length && stepRewardLabels[i] != null)
            {
                int grain = i < HomeController.StepRewardGrain.Length ? HomeController.StepRewardGrain[i] : 0;
                // 텍스트는 짧게(레이아웃 깨짐 방지). 라벨은 아이콘으로 대체 예정이므로 군더더기 제거.
                string state = claimed ? "완료" : canClaim ? "수령" : "";
                stepRewardLabels[i].text = state.Length > 0
                    ? $"{HomeController.StepMilestones[i]:N0}\n+{grain}\n{state}"
                    : $"{HomeController.StepMilestones[i]:N0}\n+{grain}";
            }
        }
    }

    void UpdateFarmWorkersUI()
    {
        if (farmWorkersText == null) return;
        var dm = DataManager.InstanceOrNull;
        long n = dm != null && dm.IsStateReady ? UserPortfolioManager.GetTotalOwnedSoldiers(dm) : 0L;
        farmWorkersText.text = n.ToString("N0");
    }

    void PushGlobalTopBar()
    {
        var gm = GameManager.InstanceOrNull;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gm?.currentUser == null || gui == null) return;

        // 홈탭은 로컬 ResourceBar 대신 GlobalUI 탑바에 표시
        string userName = gm.currentUser.userName;
        long soldiers = DataManager.InstanceOrNull != null && DataManager.InstanceOrNull.IsStateReady
            ? UserPortfolioManager.GetTotalOwnedSoldiers(DataManager.InstanceOrNull)
            : gm.currentUser.soldierCount;
        gui.SetTopBarNumbers(userName, gm.currentGold, gm.currentGrain, soldiers);
    }

    void UpdateLaborUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (laborLabelText == null || _controller == null || gm == null) return;

        int lv = gm.clickPowerLevel;
        double current = _controller.GoldPerClick;
        double next = HomeController.BaseGoldPerClick + ((lv + 1) * HomeController.ExtraValuePerLaborLevel);
        double cost = HomeController.UpgradeCost(HomeController.LaborBaseCost, lv);

        laborLabelText.text =
            $"클릭당 금화 획득량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Tap -> 다음: +{next:F0} Gold/Tap\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateMarketUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (marketLabelText == null || _controller == null || gm == null) return;

        int lv = gm.autoIncomeLevel;
        double current = lv <= 0 ? 0 : gm.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : gm.GetAutoIncomeValue(lv + 1);
        double cost = HomeController.UpgradeCost(HomeController.MarketBaseCost, lv);

        marketLabelText.text =
            $"초당 금화 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Sec -> 다음: +{next:F0} Gold/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateFarmUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (farmLabelText == null || _controller == null || gm == null) return;

        int lv = gm.currentUser?.farmLevel ?? 0;
        double current = lv <= 0 ? 0 : gm.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : gm.GetAutoIncomeValue(lv + 1);
        double cost = HomeController.UpgradeCost(HomeController.FarmBaseCost, lv);

        farmLabelText.text =
            $"초당 식량 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Grain/Sec -> 다음: +{next:F0} Grain/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateSupplyUI()
    {
        if (supplyLabelText == null || _controller == null) return;

        int maxGrain = _controller.GetMaxAffordableGrain();

        supplyLabelText.text =
            "병사는 <b>천하</b> 탭에서 AI 수비군을 매수하세요.\n" +
            $"(식량: 최대 {maxGrain} 구매 가능 · 버튼으로 수량 선택)";
    }

    Transform SupplyDialogCanvasRoot()
    {
        var c = GetComponentInParent<Canvas>();
        return c != null ? c.transform : transform;
    }

    void EnsureSupplyDialogsBuilt()
    {
        if (_supplyDialogsBuilt) return;
        var parent = SupplyDialogCanvasRoot();
        BuildGrainDialog(parent);
        _supplyDialogsBuilt = true;
    }

    void OpenGrainDialog()
    {
        EnsureSupplyDialogsBuilt();
        if (_grainDialogRoot == null || _controller == null || _grainSlider == null) return;

        _grainMaxThisOpen = _controller.GetMaxAffordableGrain();
        _grainDialogRoot.SetAsLastSibling();
        _grainDialogRoot.gameObject.SetActive(true);

        if (_grainMaxThisOpen <= 0)
        {
            _grainSlider.wholeNumbers = true;
            _grainSlider.minValue = 0;
            _grainSlider.maxValue = 0;
            _grainSlider.SetValueWithoutNotify(0);
            _grainSlider.interactable = false;
            if (_grainValueText != null)
            {
                _grainValueText.text =
                    "<color=#8899aa>보유 금화로 구매할 수 없습니다.</color>\n\n" +
                    $"단가 (1)   {HomeController.GrainCost:N0}  G";
            }

            if (_grainConfirmButton != null)
                _grainConfirmButton.interactable = false;
        }
        else
        {
            _grainSlider.wholeNumbers = true;
            _grainSlider.minValue = 1;
            _grainSlider.maxValue = _grainMaxThisOpen;
            _grainSlider.interactable = true;
            int start = Mathf.Max(1, _grainMaxThisOpen / 2);
            _grainSlider.SetValueWithoutNotify(start);
            if (_grainConfirmButton != null)
                _grainConfirmButton.interactable = true;
            OnGrainSlider(_grainSlider.value);
        }
    }

    void CloseGrainDialog()
    {
        if (_grainDialogRoot != null)
            _grainDialogRoot.gameObject.SetActive(false);
    }

    void OnGrainSlider(float v)
    {
        if (_grainValueText == null || _controller == null) return;
        int n = Mathf.RoundToInt(v);
        int unit = HomeController.GrainCost;
        if (n <= 0)
        {
            _grainValueText.text =
                "<color=#8899aa>보유 금화로 구매할 수 없습니다.</color>\n\n" +
                $"단가 (1)   {unit:N0}  G";
            return;
        }

        long total = (long)n * unit;
        _grainValueText.text =
            $"<b>{n:N0}</b> 식량 구매\n\n" +
            $"단가 (1)         {unit:N0}  G\n" +
            $"<color=#8899aa>─────────────────</color>\n" +
            $"<color=#ffd080>총 비용</color>         <b>{total:N0}</b>  G";
    }

    void OnGrainConfirm()
    {
        if (_controller == null || _grainSlider == null) return;
        int n = Mathf.RoundToInt(_grainSlider.value);
        if (n <= 0) return;
        _controller.BuyGrain(n);
        CloseGrainDialog();
        UpdateSupplyUI();
        var gm = GameManager.InstanceOrNull;
        if (gm != null)
        {
            RollGoldDisplay(gm.currentGold);
            RollGrainDisplay(gm.currentGrain);
        }

        PushGlobalTopBar();
    }

    void BuildGrainDialog(Transform parent)
    {
        _grainDialogRoot = BuildSupplyPurchaseDialog(
            parent,
            "HomeGrainDialog",
            "식량 구매",
            new Color(0.28f, 0.42f, 0.58f, 1f),
            CloseGrainDialog,
            out _grainSlider,
            out _grainValueText,
            out _grainCancelButton,
            out _grainConfirmButton);
        _grainDialogRoot.gameObject.SetActive(false);

        if (_grainCancelButton != null)
        {
            _grainCancelButton.onClick.RemoveAllListeners();
            _grainCancelButton.onClick.AddListener(CloseGrainDialog);
        }

        if (_grainConfirmButton != null)
        {
            _grainConfirmButton.onClick.RemoveAllListeners();
            _grainConfirmButton.onClick.AddListener(OnGrainConfirm);
        }

        if (_grainSlider != null)
        {
            _grainSlider.onValueChanged.RemoveListener(OnGrainSlider);
            _grainSlider.onValueChanged.AddListener(OnGrainSlider);
        }
    }

    static RectTransform BuildSupplyPurchaseDialog(
        Transform parent,
        string rootName,
        string title,
        Color fillColor,
        Action onDimClose,
        out Slider slider,
        out TextMeshProUGUI valueText,
        out Button cancelBtn,
        out Button confirmBtn)
    {
        slider = null;
        valueText = null;
        cancelBtn = null;
        confirmBtn = null;

        var rootGo = new GameObject(rootName, typeof(RectTransform), typeof(Image), typeof(SupplyDialogDimClose));
        var root = rootGo.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        StretchFullRoot(root);
        root.SetAsLastSibling();
        var rootImg = rootGo.GetComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.52f);
        rootImg.raycastTarget = true;

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        box.transform.SetParent(root, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 400f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.15f, 0.995f);
        var bv = box.GetComponent<VerticalLayoutGroup>();
        bv.padding = new RectOffset(26, 26, 22, 20);
        bv.spacing = 16;
        bv.childAlignment = TextAnchor.UpperCenter;
        bv.childControlWidth = true;
        bv.childControlHeight = true;
        bv.childForceExpandWidth = true;

        var titleTmp = SupplyCreateTmp(box.transform, "Title", title, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleTmp.color = new Color(1f, 0.96f, 0.88f, 1f);
        titleTmp.gameObject.GetComponent<LayoutElement>().minHeight = 40f;
        titleTmp.gameObject.GetComponent<LayoutElement>().preferredHeight = 44f;

        valueText = SupplyCreateTmp(box.transform, "Value", "", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
        valueText.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        valueText.enableWordWrapping = true;
        valueText.richText = true;
        valueText.lineSpacing = 6f;
        valueText.gameObject.GetComponent<LayoutElement>().minHeight = 140f;
        valueText.gameObject.GetComponent<LayoutElement>().preferredHeight = 152f;

        var sgo = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sgo.transform.SetParent(box.transform, false);
        sgo.GetComponent<LayoutElement>().minHeight = 46f;
        sgo.GetComponent<LayoutElement>().preferredHeight = 48f;
        slider = sgo.GetComponent<Slider>();
        slider.minValue = 1;
        slider.maxValue = 100;
        slider.wholeNumbers = true;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sgo.transform, false);
        StretchFullRoot(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 1f);
        var fillA = new GameObject("Fill Area", typeof(RectTransform));
        fillA.transform.SetParent(sgo.transform, false);
        StretchFullRoot(fillA.GetComponent<RectTransform>());
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillA.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = fillColor;
        slider.fillRect = fillRt;

        var handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(sgo.transform, false);
        StretchFullRoot(handleSlide.GetComponent<RectTransform>());
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleSlide.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(24f, 32f);
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        handle.GetComponent<Image>().color = new Color(0.92f, 0.93f, 0.96f, 1f);
        slider.handleRect = hRt;
        slider.targetGraphic = handle.GetComponent<Image>();

        var hBtn = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        hBtn.transform.SetParent(box.transform, false);
        hBtn.GetComponent<LayoutElement>().minHeight = 56f;
        hBtn.GetComponent<LayoutElement>().preferredHeight = 60f;
        var hhg = hBtn.GetComponent<HorizontalLayoutGroup>();
        hhg.spacing = 14;
        hhg.childControlWidth = true;
        hhg.childForceExpandWidth = true;
        hhg.childControlHeight = true;
        hhg.childForceExpandHeight = true;
        cancelBtn = SupplyCreateFooterBtn(hBtn.transform, "취소", new Color(0.38f, 0.39f, 0.44f));
        confirmBtn = SupplyCreateFooterBtn(hBtn.transform, "확인", new Color(0.20f, 0.48f, 0.68f));

        if (onDimClose != null)
            rootGo.GetComponent<SupplyDialogDimClose>().Configure(boxRt, onDimClose);

        return root;
    }

    static TextMeshProUGUI SupplyCreateTmp(Transform parent, string name, string text, float size, FontStyles fs,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = fs;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button SupplyCreateFooterBtn(Transform parent, string label, Color bg)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 52f;
        le.preferredHeight = 52f;
        le.flexibleWidth = 1f;
        var btn = go.GetComponent<Button>();
        var tmp = SupplyCreateTmp(go.transform, "Lbl", label, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        tmp.color = Color.white;
        StretchFullRoot(tmp.rectTransform);
        return btn;
    }

    static void StretchFullRoot(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
