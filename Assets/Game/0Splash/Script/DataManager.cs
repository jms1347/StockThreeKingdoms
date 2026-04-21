using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Sirenix.OdinInspector; // Odin 네임스페이스 추가
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>성별 투입 상한을 좌표축별로 분해한 값(최종 MAX는 남은 정원·미투입 병력·금화 중 최소).</summary>
public readonly struct DeployTroopCapBreakdown
{
    public int FinalMax { get; }
    public int LimitByPool { get; }
    /// <summary>보유 금화로 살 수 있는 최대 인원. 단가 없으면 <see cref="int.MaxValue"/>.</summary>
    public int LimitByGold { get; }
    /// <summary><c>maxTroops</c> − 내 주둔(이 성에 더 넣을 수 있는 슬롯).</summary>
    public int CastleVacancy { get; }

    public DeployTroopCapBreakdown(int finalMax, int limitPool, int limitGold, int castleVacancy)
    {
        FinalMax = finalMax;
        LimitByPool = limitPool;
        LimitByGold = limitGold;
        CastleVacancy = castleVacancy;
    }

    /// <summary>투입 확인창 등에 표시할 한 줄 설명(TMP rich text).</summary>
    public static string FormatHint(in DeployTroopCapBreakdown b)
    {
        if (b.FinalMax <= 0)
        {
            if (b.CastleVacancy <= 0)
                return "<color=#8899aa>AI 수비군을 더 이상 매수할 수 없습니다.</color>";
            if (b.LimitByGold < int.MaxValue && b.LimitByGold <= 0)
                return "<color=#8899aa>보유 금화로는 매수할 수 없습니다.</color>";
            return "<color=#8899aa>지금은 매수할 수 없습니다.</color>";
        }

        var parts = new List<string>(3);
        if (b.CastleVacancy > 0 && b.FinalMax == b.CastleVacancy)
            parts.Add($"AI 수비군 매수 한도 <color=#aaccff>{b.CastleVacancy:N0}명</color>");
        if (b.LimitByGold < int.MaxValue && b.FinalMax == b.LimitByGold)
            parts.Add($"금화로 약 <color=#ffd080>{b.LimitByGold:N0}명</color>");
        if (parts.Count == 0)
            return $"<color=#8899aa>MAX {b.FinalMax:N0}명</color>";
        return "<color=#8899aa>슬라이더 MAX</color>는 " + string.Join(" · ", parts) + " 중 가장 작은 값입니다.";
    }

    /// <summary>투입 버튼 비활성 시 푸터·카드용 한 줄(리치 텍스트 없음). <see cref="FinalMax"/> &gt; 0이면 빈 문자열.</summary>
    public static string FormatDeployButtonDisabledReason(in DeployTroopCapBreakdown b)
    {
        if (b.FinalMax > 0) return "";
        if (b.CastleVacancy <= 0)
            return "AI 수비군을 더 이상 매수할 수 없습니다.";
        if (b.LimitByGold < int.MaxValue && b.LimitByGold <= 0)
            return "보유 금화로는 매수할 수 없습니다.";
        return "지금은 매수할 수 없습니다.";
    }
}

public partial class DataManager : Singleton<DataManager>
{
    public Action OnDataReady;
    public Action OnStateDataReady;
    public Action<WorldNewsItem> OnNewsAdded;
    public Action OnStateTicked;

    /// <summary>런타임 맵·SO를 갱신한 뒤 천하 UI만 즉시 다시 그릴 때 호출(가짜 틱 제거 후).</summary>
    public void RequestWorldUiRefresh() => OnStateTicked?.Invoke();

    [Header("고정 마스터 (FixedSoDataManager)")]
    [Tooltip("같은 GameObject에 두면 자동 연결됩니다. 마스터 SO·맵은 여기서만 할당하세요.")]
    [SerializeField] FixedSoDataManager fixedSoDataManager;

    /// <summary>고정 SO·마스터 맵. 컴포넌트 참조 또는 싱글톤.</summary>
    public FixedSoDataManager FixedSo =>
        fixedSoDataManager != null ? fixedSoDataManager : FixedSoDataManager.InstanceOrNull;

    [Header("월드 초기 시나리오 SO (선택, 변동 상태 덮어쓰기)")]
    [Tooltip("castle_state.json 이 없을 때만 BuildStateDataFromMaster 뒤에 적용되는 성별 초기 AI/상태 덮어쓰기.")]
    [SerializeField] CastleWorldInitialScenarioSo castleWorldInitialScenarioSo;

    [Header("실시간 SO 미러 (천하·에디터)")]
    [Tooltip("전 성 실시간 상태. 런타임 갱신 시 에디터에서 SetDirty + 디바운스 SaveAssets.")]
    [SerializeField] CastleStateSo castleStateLiveSo;
    [Tooltip("유저 투자(성별 병력·평단) + 총 금화.")]
    [SerializeField] UserPortfolioSo userPortfolioLiveSo;

    static Dictionary<int, LevelRuleData> _fbLevelRule;
    static Dictionary<string, CastleMasterData> _fbCastle;
    static Dictionary<string, GeneralMasterData> _fbGeneral;
    static Dictionary<string, BuffMasterData> _fbBuff;
    static Dictionary<string, NationMasterData> _fbNation;
    static Dictionary<string, RegionMasterData> _fbRegion;
    static Dictionary<string, List<EventMasterData>> _fbEvent;
    static Dictionary<string, ConditionData> _fbCond;
    static Dictionary<string, EventStatModifierData> _fbStatMod;
    static Dictionary<string, NewsMasterData> _fbNews;
    static Dictionary<string, string> _fbCastleRegion;

    /// <summary><see cref="FixedSoDataManager.levelRuleMap"/> 위임(FixedSo 없으면 빈 맵).</summary>
    public Dictionary<int, LevelRuleData> levelRuleMap =>
        FixedSo != null ? FixedSo.levelRuleMap : (_fbLevelRule ??= new Dictionary<int, LevelRuleData>());

    public Dictionary<string, CastleMasterData> castleMasterDataMap =>
        FixedSo != null ? FixedSo.castleMasterDataMap : (_fbCastle ??= new Dictionary<string, CastleMasterData>());

    public Dictionary<string, GeneralMasterData> generalMasterDataMap =>
        FixedSo != null ? FixedSo.generalMasterDataMap : (_fbGeneral ??= new Dictionary<string, GeneralMasterData>());

    public Dictionary<string, BuffMasterData> buffMasterDataMap =>
        FixedSo != null ? FixedSo.buffMasterDataMap : (_fbBuff ??= new Dictionary<string, BuffMasterData>());

    public Dictionary<string, NationMasterData> nationMasterDataMap =>
        FixedSo != null ? FixedSo.nationMasterDataMap : (_fbNation ??= new Dictionary<string, NationMasterData>());

    public Dictionary<string, RegionMasterData> regionMasterDataMap =>
        FixedSo != null ? FixedSo.regionMasterDataMap : (_fbRegion ??= new Dictionary<string, RegionMasterData>());

    public Dictionary<string, List<EventMasterData>> eventMasterDataMap =>
        FixedSo != null
            ? FixedSo.eventMasterDataMap
            : (_fbEvent ??= new Dictionary<string, List<EventMasterData>>(StringComparer.Ordinal));

    public Dictionary<string, ConditionData> conditionDataMap =>
        FixedSo != null
            ? FixedSo.conditionDataMap
            : (_fbCond ??= new Dictionary<string, ConditionData>(StringComparer.Ordinal));

    public Dictionary<string, EventStatModifierData> eventStatModifierMap =>
        FixedSo != null
            ? FixedSo.eventStatModifierMap
            : (_fbStatMod ??= new Dictionary<string, EventStatModifierData>(StringComparer.Ordinal));

    public Dictionary<string, NewsMasterData> newsMasterDataMap =>
        FixedSo != null
            ? FixedSo.newsMasterDataMap
            : (_fbNews ??= new Dictionary<string, NewsMasterData>(StringComparer.Ordinal));

    public Dictionary<string, string> castleIdToRegionIdMap =>
        FixedSo != null ? FixedSo.castleIdToRegionIdMap : (_fbCastleRegion ??= new Dictionary<string, string>());

    [Header("State Data (Runtime)")]
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "성 ID", ValueLabel = "성 상태 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, CastleStateData> castleStateDataMap = new Dictionary<string, CastleStateData>();

    [ShowInInspector]
    public List<WorldNewsItem> worldNews = new List<WorldNewsItem>();

    [ShowInInspector]
    public List<PendingRumorWorldEvent> pendingRumorWorldEvents = new List<PendingRumorWorldEvent>();

    public bool IsReady { get; private set; } = false;
    public bool IsStateReady { get; private set; } = false;

    public long LastWeeklyDividendPaidAnchorUnix => _lastWeeklyDividendPaidAnchorUnix;

    public void SetLastWeeklyDividendPaidAnchorUnix(long anchorUnixSeconds) =>
        _lastWeeklyDividendPaidAnchorUnix = anchorUnixSeconds;

    float _nextSaveAt;
    bool _stateDirty;

    [Header("본영·이동 게이지 (UserPortfolioLiveSo와 동기화)")]
    [SerializeField, Tooltip("지도 거리 100단위당 소모 포인트")]
    float travelPointsPer100Distance = 5000f;
    [SerializeField, Tooltip("실시간 1분당 자동 충전 포인트")]
    float travelIdlePointsPerMinute = 10f;
    [SerializeField, Tooltip("만보기 1걸음당 이동 게이지 포인트")]
    float travelPointsPerStep = 1f;
    [SerializeField, Tooltip("이동 게이지 바 시각화용 상한(실제 값은 무제한에 가깝게 누적)")]
    float travelGaugeVisualCap = 25000f;

    string _homeCastleId = "";
    float _travelGaugePoints;
    int _lastStepCountSyncedForGauge;
    bool _gameManagerStepsHooked;
    string _pendingHqMoveCastleId = "";
    float _pendingHqMoveCost;

    /// <summary>마지막 주간 배당이 반영된 로컬 월요일 06:00 앵커(Unix 초). 세이브에 포함.</summary>
    long _lastWeeklyDividendPaidAnchorUnix;

#if UNITY_EDITOR
    float _nextEditorLiveSoSaveTime;
#endif

    const string StateSaveFileName = "castle_state.json";
    const int WorldNewsMaxCount = 100;

    protected override void Awake()
    {
        base.Awake();
        if (fixedSoDataManager == null)
            fixedSoDataManager = GetComponent<FixedSoDataManager>();
    }

    public void InitializeAllData()
    {
        if (FixedSo != null)
        {
            FixedSo.ApplyGlobalEconomyDefaultsFromSoIfPresent();
            FixedSo.SyncRuntimeMapsFromSo();
        }
        else
        {
            Debug.LogWarning(
                "[DataManager] FixedSoDataManager가 없습니다. DataManager와 같은 오브젝트에 FixedSoDataManager를 추가하고 마스터 SO를 연결하세요.");
        }

        IsReady = true;
        OnDataReady?.Invoke();
        int eventRowTotal = 0;
        if (FixedSo != null)
        {
            foreach (var kv in FixedSo.eventMasterDataMap)
                if (kv.Value != null) eventRowTotal += kv.Value.Count;
        }

        Debug.Log(
            $"[DataManager] 변동 데이터 매니저 준비. 마스터(FixedSo): {(FixedSo != null ? "OK" : "없음")} — 레벨룰 {levelRuleMap.Count}, 성 {castleMasterDataMap.Count}, 장수 {generalMasterDataMap.Count}, 버프 {buffMasterDataMap.Count}, 세력 {nationMasterDataMap.Count}, 지역 {regionMasterDataMap.Count}, 이벤트 {eventMasterDataMap.Count}종(행 {eventRowTotal}), 조건 {conditionDataMap.Count}, 확률보정 {eventStatModifierMap.Count}, 뉴스마스터 {newsMasterDataMap.Count}.");

        InitializeStateData();
    }

    void Update()
    {
        if (!IsReady || !IsStateReady) return;

        TickTravelGaugeIdle(Time.unscaledDeltaTime);
        TickCastleDailyHistoryRollover();
        DividendManager.Tick(this, Time.unscaledTime);

        float now = Time.unscaledTime;
        if (_stateDirty && now >= _nextSaveAt)
        {
            _nextSaveAt = now + 15f;
            SaveStateDataToDisk();
        }
    }

    public LevelRuleData GetLevelData(int level) => FixedSo != null ? FixedSo.GetLevelData(level) : null;

    public CastleMasterData GetCastleMasterData(string id) => FixedSo != null ? FixedSo.GetCastleMasterData(id) : null;

    public GeneralMasterData GetGeneralMasterData(string id) => FixedSo != null ? FixedSo.GetGeneralMasterData(id) : null;

    public BuffMasterData GetBuffMasterData(string id) => FixedSo != null ? FixedSo.GetBuffMasterData(id) : null;

    /// <summary>
    /// 구글 시트로 버프 맵을 채운 뒤, SO에만 있는 ID(예: 이벤트용 E01~E11)를 맵에 합칩니다.
    /// <see cref="GoogleSheetManager"/>에서 SetBuff 직후 호출.
    /// </summary>
    public void MergeBuffMasterFromSoMissingKeys() => FixedSo?.MergeBuffMasterFromSoMissingKeys();

    public NationMasterData GetNationMasterData(string id) => FixedSo != null ? FixedSo.GetNationMasterData(id) : null;

    public RegionMasterData GetRegionMasterData(string regionId) =>
        FixedSo != null ? FixedSo.GetRegionMasterData(regionId) : null;

    /// <summary>eventId의 <b>첫 행</b>(대표 메타·버프). 다중 행 중 조건 만족 행은 <see cref="TryGetEventMasterRows"/>로 조회.</summary>
    public EventMasterData GetEventMasterData(string eventId) =>
        FixedSo != null ? FixedSo.GetEventMasterData(eventId) : null;

    public bool TryGetEventMasterRows(string eventId, out List<EventMasterData> rows)
    {
        rows = null;
        return FixedSo != null && FixedSo.TryGetEventMasterRows(eventId, out rows);
    }

    public void AddEventMasterDataRow(EventMasterData row) => FixedSo?.AddEventMasterDataRow(row);

    /// <summary>성 마스터 ID로 소속 지역(R01 등)을 조회합니다.</summary>
    public bool TryGetRegionIdForCastle(string castleId, out string regionId)
    {
        regionId = null;
        return FixedSo != null && FixedSo.TryGetRegionIdForCastle(castleId, out regionId);
    }

    /// <summary>성 ID로 <see cref="RegionMasterData"/>를 조회합니다.</summary>
    public bool TryGetRegionByCastleId(string castleId, out RegionMasterData region)
    {
        region = null;
        return FixedSo != null && FixedSo.TryGetRegionByCastleId(castleId, out region);
    }

    /// <summary>UI·뉴스용 성 표시명 — R01/C01 같은 코드 대신 실제 성명·지역 문자열·섹터명을 우선합니다.</summary>
    public string GetCastleDisplayName(string castleId) =>
        FixedSo != null ? FixedSo.GetCastleDisplayName(castleId) : "";

    /// <summary>천하 카드 부제 등 — 지역(섹터) 표시명. 제목과 같으면 빈 문자열.</summary>
    public string GetCastleRegionSubtitle(string castleId) =>
        FixedSo != null ? FixedSo.GetCastleRegionSubtitle(castleId) : "";

    public void RebuildRegionCastleLookup() => FixedSo?.RebuildRegionCastleLookup();

    // ========================================================================
    // State Data Initialization / Save / Load
    // ========================================================================
    public void InitializeStateData()
    {
        if (!IsReady) return;

        bool loaded = LoadStateDataFromDisk();
        if (!loaded)
            BuildStateDataFromMaster();

        SyncCastleTaxRateFromMaster();

        EnsureAllCastlesAmmInitialized();

        RecalculateAllPrices();

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            castleMasterDataMap.TryGetValue(s.id, out var master);
            EnsureCastleHistorySeeded(s, master);
            if (s.buyPricePrevDayClose < 0.5f)
                s.buyPricePrevDayClose = CalculateCastleQuote(s);
        }

        IsStateReady = true;
        LoadWorldPortfolioHqFromSo();
        EnsureDefaultHomeCastleIfEmpty();
        HookGameManagerStepsForTravelGauge();

        OnStateDataReady?.Invoke();

        float now = Time.unscaledTime;
        _nextSaveAt = now + 10f;
        _stateDirty = true; // 첫 저장 보장
        FlushLiveScriptableObjects();

        DividendManager.TryProcessWeeklyDividend(this);
    }

    void BuildStateDataFromMaster()
    {
        castleStateDataMap.Clear();
        for (int i = 0; i < castleMasterDataMap.Count; i++) { } // keep compiler happy (no foreach alloc worries not needed)

        foreach (var kv in castleMasterDataMap)
        {
            var master = kv.Value;
            if (master == null || string.IsNullOrWhiteSpace(master.id)) continue;

            var s = new CastleStateData();
            s.id = master.id.Trim();
            s.currentLord = master.GetInitialLordFaction();
            s.currentPopulation = master.initPopulation;
            s.currentSentiment = 100f;
            s.currentGovernorId = "";
            s.isWar = false;
            s.isDisaster = false;
            s.isFavorableEvent = false;
            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
            s.sentimentHistory = new List<float>(10) { s.currentSentiment };
            s.populationHistory = new List<int>(10) { s.currentPopulation };
            s.historyPopulation7Day = new List<float>();
            s.historySentiment7Day = new List<float>();
            s.buyPricePrevDayClose = 0f;
            s.castleTaxRatePercent = master.initialTaxRatePercent;
            s.worldEventCooldowns = new List<WorldEventCooldownEntry>();
            castleStateDataMap[s.id] = s;
        }

        ApplyInitialGovernorsFromGenerals();
        ApplyCastleWorldInitialScenarioIfPresent();

        worldNews.Clear();
        if (pendingRumorWorldEvents != null)
            pendingRumorWorldEvents.Clear();
        AddNews($"[INIT] StateData 생성 완료 ({castleStateDataMap.Count}개 성).");
        _stateDirty = true;
    }

    /// <summary>저장에 세율이 없던 성은 마스터 <see cref="CastleMasterData.initialTaxRatePercent"/>로 보강.</summary>
    void SyncCastleTaxRateFromMaster()
    {
        if (castleStateDataMap == null || castleMasterDataMap == null) return;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            if (!castleMasterDataMap.TryGetValue(s.id, out var m) || m == null) continue;
            if (s.castleTaxRatePercent <= 0f && m.initialTaxRatePercent > 0f)
                s.castleTaxRatePercent = m.initialTaxRatePercent;
        }
    }

    /// <summary>
    /// <see cref="userPortfolioLiveSo"/> 보유(없으면 런타임 맵) + 성채 호가 기준 수익률(%). 미보유·평단 0이면 false.
    /// </summary>
    public bool TryGetCastleRoiSellBasis(string castleId, out float roiPercent)
    {
        roiPercent = 0f;
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId)) return false;
        castleId = castleId.Trim();

        float avg = 0f;
        if (TryGetUserCastleStock(castleId, out var stock) && stock != null && stock.troopCount > 0)
            avg = stock.averagePurchasePrice;
        else if (castleStateDataMap.TryGetValue(castleId, out var sm) && sm != null && sm.IsUserInvested)
            avg = sm.averagePurchasePrice;
        else
            return false;

        if (avg < 1e-4f) return false;

        float mark = 0f;
        if (TryGetLiveCastleState(castleId, out var live) && live != null)
            mark = live.currentBuyPrice;
        else if (castleStateDataMap.TryGetValue(castleId, out var s) && s != null)
            mark = s.currentBuyPrice;
        else
            return false;

        roiPercent = (mark - avg) / avg * 100f;
        return true;
    }

    /// <summary><see cref="castleStateLiveSo"/>에서 성 라이브 엔트리 조회.</summary>
    public bool TryGetLiveCastleState(string castleId, out CastleStateSo.CastleLiveStateEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(castleId) || castleStateLiveSo == null || castleStateLiveSo.castles == null)
            return false;
        castleId = castleId.Trim();
        var list = castleStateLiveSo.castles;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || string.IsNullOrWhiteSpace(e.castleId)) continue;
            if (string.Equals(e.castleId.Trim(), castleId, StringComparison.Ordinal))
            {
                entry = e;
                return true;
            }
        }

        return false;
    }

    /// <summary><see cref="userPortfolioLiveSo"/>에서 성별 유저 보유 조회.</summary>
    public bool TryGetUserCastleStock(string castleId, out UserPortfolioSo.UserCastleStock stock)
    {
        stock = null;
        if (string.IsNullOrWhiteSpace(castleId) || userPortfolioLiveSo == null || userPortfolioLiveSo.holdings == null)
            return false;
        castleId = castleId.Trim();
        var list = userPortfolioLiveSo.holdings;
        for (int i = 0; i < list.Count; i++)
        {
            var h = list[i];
            if (h == null || string.IsNullOrWhiteSpace(h.castleId)) continue;
            if (string.Equals(h.castleId.Trim(), castleId, StringComparison.Ordinal))
            {
                stock = h;
                return true;
            }
        }

        return false;
    }

    public bool HasUserStockInPortfolio(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        castleId = castleId.Trim();
        if (TryGetLiveCastleState(castleId, out var live) && live != null && live.userDeployedTroops > 0)
            return true;
        if (TryGetUserCastleStock(castleId, out var st) && st != null && st.troopCount > 0)
            return true;
        return castleStateDataMap.TryGetValue(castleId, out var s) && s != null && s.IsUserInvested;
    }

    bool HasLiveCastleSoListForWorldUi() =>
        castleStateLiveSo != null && castleStateLiveSo.castles != null && castleStateLiveSo.castles.Count > 0;

    /// <summary>천하 리스트 헤더 등 — <see cref="castleStateLiveSo"/> 행 수 우선, 없으면 런타임 맵.</summary>
    public int GetWorldCastleUiTotalCount()
    {
        if (castleStateLiveSo != null && castleStateLiveSo.castles != null && castleStateLiveSo.castles.Count > 0)
            return castleStateLiveSo.castles.Count;
        return castleStateDataMap != null ? castleStateDataMap.Count : 0;
    }

    /// <summary>UI용 별칭. <see cref="TryGetCastleRoiSellBasis"/>.</summary>
    public bool TryGetRoiPercent(string castleId, out float roiPercent) => TryGetCastleRoiSellBasis(castleId, out roiPercent);

    /// <summary><see cref="castleStateLiveSo"/>·<see cref="userPortfolioLiveSo"/>에 런타임 맵을 반영. 에디터에서만 디스크 저장(디바운스).</summary>
    public void FlushLiveScriptableObjects()
    {
        if (!IsStateReady || castleStateDataMap == null) return;

        if (castleStateLiveSo != null)
        {
            if (castleStateLiveSo.castles == null)
                castleStateLiveSo.castles = new List<CastleStateSo.CastleLiveStateEntry>();
            castleStateLiveSo.castles.Clear();
            var keys = new List<string>(castleStateDataMap.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!castleStateDataMap.TryGetValue(keys[i], out var s) || s == null) continue;
                castleStateLiveSo.castles.Add(new CastleStateSo.CastleLiveStateEntry
                {
                    castleId = s.id,
                    currentPopulation = s.currentPopulation,
                    currentSentiment = s.currentSentiment,
                    isWar = s.isWar,
                    isDisaster = s.isDisaster,
                    isFavorableEvent = s.isFavorableEvent,
                    currentGovernorId = s.currentGovernorId ?? "",
                    currentLord = s.currentLord,
                    currentBuyPrice = s.currentBuyPrice,
                    castleTaxRatePercent = s.castleTaxRatePercent,
                    userDeployedTroops = s.userDeployedTroops,
                    averagePurchasePrice = s.averagePurchasePrice,
                    maxGarrison = s.maxGarrison,
                    currentAiGarrison = s.currentAiGarrison,
                    goldReserve = s.goldReserve,
                    constantK = s.constantK,
                    accumulatedDividendPool = s.accumulatedDividendPool,
                    historyPopulation7Day = s.historyPopulation7Day != null ? new List<float>(s.historyPopulation7Day) : new List<float>(),
                    historySentiment7Day = s.historySentiment7Day != null ? new List<float>(s.historySentiment7Day) : new List<float>(),
                    buyPricePrevDayClose = s.buyPricePrevDayClose
                });
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(castleStateLiveSo);
#endif
        }

        if (userPortfolioLiveSo != null)
        {
            if (userPortfolioLiveSo.holdings == null)
                userPortfolioLiveSo.holdings = new List<UserPortfolioSo.UserCastleStock>();
            userPortfolioLiveSo.holdings.Clear();
            foreach (var kv in castleStateDataMap)
            {
                var s = kv.Value;
                if (s == null || s.userDeployedTroops <= 0) continue;
                userPortfolioLiveSo.holdings.Add(new UserPortfolioSo.UserCastleStock
                {
                    castleId = s.id,
                    troopCount = s.userDeployedTroops,
                    averagePurchasePrice = s.averagePurchasePrice
                });
            }

            var gm = GameManager.InstanceOrNull;
            userPortfolioLiveSo.totalGold = gm != null && gm.currentUser != null ? (long)gm.currentUser.gold : 0L;
            userPortfolioLiveSo.homeCastleId = _homeCastleId ?? "";
            userPortfolioLiveSo.travelGaugePoints = _travelGaugePoints;
            userPortfolioLiveSo.currentStepCount = _lastStepCountSyncedForGauge;
#if UNITY_EDITOR
            EditorUtility.SetDirty(userPortfolioLiveSo);
#endif
        }

#if UNITY_EDITOR
        if (castleStateLiveSo != null || userPortfolioLiveSo != null)
        {
            float t = Time.realtimeSinceStartup;
            if (t >= _nextEditorLiveSoSaveTime)
            {
                _nextEditorLiveSoSaveTime = t + 0.65f;
                AssetDatabase.SaveAssets();
            }
        }
#endif
    }

    void ApplyCastleWorldInitialScenarioIfPresent()
    {
        if (castleWorldInitialScenarioSo == null || !castleWorldInitialScenarioSo.enabled) return;
        if (castleWorldInitialScenarioSo.entries == null) return;

        for (int i = 0; i < castleWorldInitialScenarioSo.entries.Count; i++)
        {
            var e = castleWorldInitialScenarioSo.entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.castleId)) continue;
            string id = e.castleId.Trim();
            if (!castleStateDataMap.TryGetValue(id, out var st) || st == null) continue;
            e.ApplyTo(st);
        }
    }

    /// <summary>
    /// 장수 마스터의 <see cref="GeneralMasterData.initialCastleId"/>를 기준으로 성마다 <see cref="CastleStateData.currentGovernorId"/>를 채웁니다.
    /// 동일 성에 여러 장수가 있으면 등급(숫자 작을수록 상위) 우선, 동급이면 ID 순입니다.
    /// </summary>
    void ApplyInitialGovernorsFromGenerals()
    {
        var bestByCastle = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kv in generalMasterDataMap)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.initialCastleId)) continue;
            string cid = g.initialCastleId.Trim();
            if (!castleStateDataMap.ContainsKey(cid)) continue;

            if (!bestByCastle.TryGetValue(cid, out string bestId))
            {
                bestByCastle[cid] = g.id;
                continue;
            }

            if (!generalMasterDataMap.TryGetValue(bestId, out var bestG) || bestG == null)
            {
                bestByCastle[cid] = g.id;
                continue;
            }

            if (g.grade < bestG.grade)
                bestByCastle[cid] = g.id;
            else if (g.grade == bestG.grade && string.CompareOrdinal(g.id, bestId) < 0)
                bestByCastle[cid] = g.id;
        }

        foreach (var kv in bestByCastle)
        {
            if (!castleStateDataMap.TryGetValue(kv.Key, out var st) || st == null) continue;
            st.currentGovernorId = kv.Value;
            st.lastDailyBuffGovernorId = "";
            st.lastDailyBuffTime = 0;
        }
    }

    string GetStateSavePath()
    {
        return Path.Combine(Application.persistentDataPath, StateSaveFileName);
    }

    bool LoadStateDataFromDisk()
    {
        try
        {
            string path = GetStateSavePath();
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;

            var payload = JsonUtility.FromJson<CastleStateSavePayload>(json);
            if (payload == null || payload.castles == null) return false;

            castleStateDataMap.Clear();
            for (int i = 0; i < payload.castles.Count; i++)
            {
                var s = payload.castles[i];
                if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
                s.id = s.id.Trim();
                if (s.sentimentHistory == null) s.sentimentHistory = new List<float>();
                if (s.populationHistory == null)
                    s.populationHistory = new List<int> { s.currentPopulation };
                if (s.historyPopulation7Day == null) s.historyPopulation7Day = new List<float>();
                if (s.historySentiment7Day == null) s.historySentiment7Day = new List<float>();
                if (s.worldEventCooldowns == null) s.worldEventCooldowns = new List<WorldEventCooldownEntry>();
                s.NormalizeStabilityIfUnset();
                castleStateDataMap[s.id] = s;
            }

            worldNews = payload.news ?? new List<WorldNewsItem>();
            TrimWorldNewsToCap();
            pendingRumorWorldEvents = payload.pendingRumorWorldEvents ?? new List<PendingRumorWorldEvent>();
            _lastWeeklyDividendPaidAnchorUnix = payload.lastWeeklyDividendPaidAnchorUnix;
            PlayerPrefs.SetString(DividendManager.PlayerPrefsLastAnchorUnix,
                _lastWeeklyDividendPaidAnchorUnix.ToString());

            // 마스터가 바뀌었을 때를 대비해 누락분 보강
            foreach (var kv in castleMasterDataMap)
            {
                var master = kv.Value;
                if (master == null || string.IsNullOrWhiteSpace(master.id)) continue;
                string id = master.id.Trim();
                if (!castleStateDataMap.ContainsKey(id))
                {
                    var s = new CastleStateData
                    {
                        id = id,
                        currentLord = master.GetInitialLordFaction(),
                        currentPopulation = master.initPopulation,
                        currentSentiment = 100f,
                        currentGovernorId = "",
                        isWar = false,
                        isDisaster = false,
                        isFavorableEvent = false,
                        userDeployedTroops = 0,
                        averagePurchasePrice = 0f,
                        sentimentHistory = new List<float>(10) { 100f },
                        populationHistory = new List<int>(10) { master.initPopulation },
                        historyPopulation7Day = new List<float>(),
                        historySentiment7Day = new List<float>(),
                        buyPricePrevDayClose = 0f,
                        castleTaxRatePercent = master.initialTaxRatePercent,
                        worldEventCooldowns = new List<WorldEventCooldownEntry>(),
                        stabilityScore = 100f
                    };
                    castleStateDataMap[id] = s;
                }
            }

            AddNews($"[LOAD] StateData 로드 완료 ({castleStateDataMap.Count}개 성).");
            _stateDirty = false;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] StateData 로드 실패: {e.Message}");
            return false;
        }
    }

    public void MarkCastleStateDirty() => _stateDirty = true;

    public void ClearConditionLibrary() => FixedSo?.ClearConditionLibrary();

    /// <summary>시트 파싱 결과를 FixedSo 조건 SO·맵에 반영합니다.</summary>
    public void ApplyParsedConditionLibrary(List<ConditionData> rows) => FixedSo?.ApplyParsedConditionLibrary(rows);

    /// <summary>EventStatModifier 시트 파싱 결과를 FixedSo에 반영합니다.</summary>
    public void ApplyParsedEventStatModifier(List<EventStatModifierData> rows) =>
        FixedSo?.ApplyParsedEventStatModifier(rows);

    public void MergeEventStatModifierFromSoMissingKeys() => FixedSo?.MergeEventStatModifierFromSoMissingKeys();

    public bool TryGetNewsMasterData(string newsCode, out NewsMasterData data)
    {
        data = null;
        return FixedSo != null && FixedSo.TryGetNewsMasterData(newsCode, out data);
    }

    public void ApplyParsedNewsMaster(List<NewsMasterData> rows) => FixedSo?.ApplyParsedNewsMaster(rows);

    public void MergeNewsMasterFromSoMissingKeys() => FixedSo?.MergeNewsMasterFromSoMissingKeys();

    public void SaveStateDataToDisk()
    {
        try
        {
            var payload = new CastleStateSavePayload
            {
                castles = castleStateDataMap.Values.ToList(),
                news = worldNews ?? new List<WorldNewsItem>(),
                pendingRumorWorldEvents = pendingRumorWorldEvents ?? new List<PendingRumorWorldEvent>(),
                lastWeeklyDividendPaidAnchorUnix = _lastWeeklyDividendPaidAnchorUnix
            };
            string json = JsonUtility.ToJson(payload, prettyPrint: true);

            string path = GetStateSavePath();
            File.WriteAllText(path, json);
            _stateDirty = false;
            FlushLiveScriptableObjects();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] StateData 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// <see cref="castleStateDataMap"/> 전수: 위(WEI)·촉(SHU)·오(WU)·기타(NONE/OTHERS 등) 점령 성 비율 (각 0~1, 합계 1).
    /// </summary>
    public FactionCastleShare GetFactionCastleOwnershipShare()
    {
        var share = new FactionCastleShare();
        if (castleStateDataMap == null || castleStateDataMap.Count == 0)
            return share;

        int cWei = 0, cShu = 0, cWu = 0, cOth = 0;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            switch (s.currentLord)
            {
                case Faction.WEI: cWei++; break;
                case Faction.SHU: cShu++; break;
                case Faction.WU: cWu++; break;
                default: cOth++; break; // NONE, OTHERS
            }
        }

        int n = cWei + cShu + cWu + cOth;
        if (n <= 0) return share;

        share.wei = cWei / (float)n;
        share.shu = cShu / (float)n;
        share.wu = cWu / (float)n;
        share.others = cOth / (float)n;
        return share;
    }

    /// <summary>
    /// 천하 탭 성 리스트: <see cref="WorldMarketCastleListFilter.All"/> 기준 정렬.
    /// </summary>
    public List<string> GetOrderedWorldCastleIds()
    {
        return GetOrderedWorldCastleIds(WorldMarketCastleListFilter.All);
    }

    /// <summary>
    /// 천하 탭 상단 필터별 목록. 필터 적용 후 공통 정렬: <b>본영 고정 1번</b> → 나머지는 <b>본영과의 거리</b>(posX,posY) 오름차순.
    /// </summary>
    public List<string> GetOrderedWorldCastleIds(WorldMarketCastleListFilter filter)
    {
        if (HasLiveCastleSoListForWorldUi())
            return GetOrderedWorldCastleIdsFromLiveSo(filter);

        if (castleStateDataMap == null || castleStateDataMap.Count == 0)
            return new List<string>();

        IEnumerable<CastleStateData> q = castleStateDataMap.Values.Where(s => s != null);

        switch (filter)
        {
            case WorldMarketCastleListFilter.All:
                return OrderWorldCastle_MtsDefault(q);
            case WorldMarketCastleListFilter.MyHoldings:
                return OrderWorldCastle_MtsDefault(q.Where(s => s.userDeployedTroops > 0));
            case WorldMarketCastleListFilter.War:
                return OrderWorldCastle_MtsDefault(q.Where(s => s.isWar));
            case WorldMarketCastleListFilter.Event:
                return OrderWorldCastle_MtsDefault(q.Where(s => s.isDisaster || s.isFavorableEvent));
            case WorldMarketCastleListFilter.Premium:
                return OrderWorldCastle_MtsDefault(q.Where(s => IsPremiumCastleId(s.id)));
            case WorldMarketCastleListFilter.Attention:
                return OrderWorldCastle_MtsDefault(q.Where(IsAttentionCastle));
            default:
                return OrderWorldCastle_MtsDefault(q);
        }
    }

    List<string> GetOrderedWorldCastleIdsFromLiveSo(WorldMarketCastleListFilter filter)
    {
        IEnumerable<CastleStateSo.CastleLiveStateEntry> q = castleStateLiveSo.castles
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.castleId));

        switch (filter)
        {
            case WorldMarketCastleListFilter.MyHoldings:
                q = q.Where(e => HasUserStockInPortfolio(e.castleId));
                break;
            case WorldMarketCastleListFilter.War:
                q = q.Where(e => e.isWar);
                break;
            case WorldMarketCastleListFilter.Event:
                q = q.Where(e => e.isDisaster || e.isFavorableEvent);
                break;
            case WorldMarketCastleListFilter.Premium:
                q = q.Where(e => IsPremiumCastleId(e.castleId));
                break;
            case WorldMarketCastleListFilter.Attention:
                q = q.Where(e => IsAttentionCastleId(e.castleId));
                break;
        }

        return OrderWorldCastle_LiveSo(q);
    }

    List<string> OrderWorldCastle_LiveSo(IEnumerable<CastleStateSo.CastleLiveStateEntry> q)
    {
        var ids = q.Select(e => e.castleId.Trim()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        return OrderIdsHomeFirstThenDistance(ids);
    }

    /// <summary>요주의: B·C·D 등급 성만 (하이리스크·저평가 종목 필터).</summary>
    bool IsAttentionCastle(CastleStateData s) =>
        s != null && IsAttentionCastleId(s.id);

    bool IsAttentionCastleId(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        if (!castleMasterDataMap.TryGetValue(castleId.Trim(), out var m) || m == null) return false;
        return m.grade >= Grade.B;
    }

    bool IsPremiumCastleId(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        if (!castleMasterDataMap.TryGetValue(castleId.Trim(), out var m) || m == null) return false;
        return m.grade <= Grade.A;
    }

    List<string> OrderWorldCastle_MtsDefault(IEnumerable<CastleStateData> q)
    {
        var ids = q.Select(s => s.id.Trim()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        return OrderIdsHomeFirstThenDistance(ids);
    }

    /// <summary>
    /// 본영 성은 필터에 포함되지 않아도 항상 첫 카드로 고정하고,
    /// 나머지는 본영까지 거리 오름차순입니다.
    /// </summary>
    List<string> OrderIdsHomeFirstThenDistance(List<string> ids)
    {
        string home = (_homeCastleId ?? "").Trim();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var others = new List<string>();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                string t = id.Trim();
                if (!string.IsNullOrEmpty(home) && string.Equals(t, home, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (seen.Add(t))
                    others.Add(t);
            }
        }

        if (!string.IsNullOrEmpty(home))
            others.Sort((a, b) => CompareCastleDistanceFromHome(home, a, b));
        else
            others.Sort(StringComparer.OrdinalIgnoreCase);

        var result = new List<string>(others.Count + 1);
        if (!string.IsNullOrEmpty(home) && castleStateDataMap != null && castleStateDataMap.ContainsKey(home))
            result.Add(home);
        result.AddRange(others);
        return result;
    }

    int CompareCastleDistanceFromHome(string home, string a, string b)
    {
        if (string.IsNullOrEmpty(home))
            return string.Compare(a, b, StringComparison.Ordinal);

        float da = GetDistance(home, a);
        float db = GetDistance(home, b);
        const float bad = 1e15f;
        if (da < 0f) da = bad;
        if (db < 0f) db = bad;
        int c = da.CompareTo(db);
        return c != 0 ? c : string.Compare(a, b, StringComparison.Ordinal);
    }

    int GetCastleGradeSortKey(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return 99;
        if (!castleMasterDataMap.TryGetValue(castleId.Trim(), out var m) || m == null) return 99;
        return (int)m.grade;
    }

    /// <summary>천하 거점에 배치된 병력 합계(상단바 병사 표시와 동일 기준).</summary>
    public long GetUserSoldierPool() => UserPortfolioManager.GetTotalOwnedSoldiers(this);

    /// <summary><see cref="ComputeDeployTroopCapBreakdown"/>의 <see cref="DeployTroopCapBreakdown.FinalMax"/>.</summary>
    public int ComputeMaxDeployTroopsForCastle(string castleId) =>
        ComputeDeployTroopCapBreakdown(castleId).FinalMax;

    void RefreshGlobalTopBarIfPossible()
    {
        var gm = GameManager.InstanceOrNull;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gm?.currentUser == null || gui == null) return;
        gui.RefreshTopBarFromGameManager();
    }

    /// <summary>
    /// 모든 성의 유저 주둔을 0으로 하고, 해당 병력을 AI 수비군 풀로 환원(K 불변).
    /// </summary>
    public void ClearAllUserCastleDeployments()
    {
        if (!IsStateReady || castleStateDataMap == null) return;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            int u = s.userDeployedTroops;
            if (u <= 0)
            {
                s.averagePurchasePrice = 0f;
                continue;
            }

            castleMasterDataMap.TryGetValue(s.id, out var m);
            EnsureCastleAmmForState(s, m);
            if (CastleAmmCore.IsInitialized(s))
            {
                int g = s.currentAiGarrison;
                int merge = Mathf.Min(u, Mathf.Max(0, s.maxGarrison - g));
                if (merge > 0)
                {
                    s.currentAiGarrison = g + merge;
                    s.goldReserve = (long)Math.Round(s.constantK / s.currentAiGarrison);
                }
            }

            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
        }

        _stateDirty = true;
        FlushLiveScriptableObjects();
        SaveStateDataToDisk();
        OnStateTicked?.Invoke();
    }

    // ========================================================================
    // 성채 호가 + 태수 버프 (입성·회수 동일 호가, 관부는 입성 시에만)
    // ========================================================================
    public void RecalculateAllPrices()
    {
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            s.currentBuyPrice = CalculateCastleQuote(s);
        }
    }

    public float GetCastleTaxRatePercent(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return 0f;
        if (!castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null) return 0f;
        return Mathf.Max(0f, s.castleTaxRatePercent);
    }

    float CalculateBasePrice(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return 0f;
        if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var master) || master == null) return 0f;

        float gradeW = GradeWeight(master.grade);
        // 민심 0~200, 100=기존 1.0 배율에 해당
        float sentimentMul = Mathf.Clamp(s.currentSentiment / 100f, 0f, 2f);
        float popMul = Mathf.Max(0f, s.currentPopulation / 1000f);
        return master.baseValue * sentimentMul * popMul * gradeW;
    }

    static float GradeWeight(Grade g)
    {
        switch (g)
        {
            case Grade.SS: return 1.60f;
            case Grade.S: return 1.35f;
            case Grade.A: return 1.20f;
            case Grade.B: return 1.10f;
            case Grade.C: return 1.00f;
            case Grade.D: return 0.90f;
            default: return 1.00f;
        }
    }

    float GetGovernorQuoteModifier(string governorId)
    {
        var buff = GetGovernorBuff(governorId);
        if (buff == null) return 0f;

        if (buff.statType == CastleStatType.CastleValue)
            return buff.value;

        if (buff.statType == CastleStatType.PriceValue)
            return -Mathf.Abs(buff.value);

        return 0f;
    }

    BuffMasterData GetGovernorBuff(string governorId)
    {
        if (string.IsNullOrWhiteSpace(governorId)) return null;
        // 장수 마스터 G열은 악명(int)으로 통합됨. 구 시트의 버프 코드(B01 등) 기반 태수 버프 연동은 사용하지 않습니다.
        return null;
    }

    public void ClearNewsTemplateSheetRows() => FixedSo?.ClearNewsTemplateSheetRows();

    public void SetNewsTemplateSheetRow(string eventId, NewsTemplateSheetRow row) =>
        FixedSo?.SetNewsTemplateSheetRow(eventId, row);

    public bool TryGetNewsTemplateSheetRow(string eventId, out NewsTemplateSheetRow row)
    {
        row = null;
        return FixedSo != null && FixedSo.TryGetNewsTemplateSheetRow(eventId, out row);
    }

    // ========================================================================
    // News
    // ========================================================================

    void TrimWorldNewsToCap()
    {
        if (worldNews == null) return;
        while (worldNews.Count > WorldNewsMaxCount)
        {
            int removeIdx = -1;
            for (int i = 0; i < worldNews.Count; i++)
            {
                var w = worldNews[i];
                if (w == null || w.newsKind != (byte)WorldNewsFeedKind.System)
                {
                    removeIdx = i;
                    break;
                }
            }

            if (removeIdx < 0)
                break;
            worldNews.RemoveAt(removeIdx);
        }
    }

    void AddNews(string text)
    {
        AddNewsItem(new WorldNewsItem
        {
            unixTime = TimeManager.GetUnixNow(),
            text = text,
            newsKind = (byte)WorldNewsFeedKind.System,
            headline = text.Trim(),
            bodyContent = text.Trim(),
            isConfirmed = true
        });
    }

    /// <summary>뉴스 한 건 추가(상세 팝업 필드 포함 가능).</summary>
    public void AddNewsItem(WorldNewsItem item)
    {
        if (item == null) return;
        if (item.unixTime <= 0)
            item.unixTime = TimeManager.GetUnixNow();
        if (worldNews == null) worldNews = new List<WorldNewsItem>();
        worldNews.Add(item);
        TrimWorldNewsToCap();
        _stateDirty = true;
        OnNewsAdded?.Invoke(item);
    }

    string GetCastleNameOrId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "(unknown)";
        string d = GetCastleDisplayName(id.Trim());
        if (!string.IsNullOrEmpty(d) && d != "성") return d;
        return id.Trim();
    }

    public void SyncRuntimeMapsFromSo() => FixedSo?.SyncRuntimeMapsFromSo();

    public void SyncSoFromRuntimeMaps() => FixedSo?.SyncSoFromRuntimeMaps();
}