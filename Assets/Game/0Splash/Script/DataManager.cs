using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Sirenix.OdinInspector; // Odin 네임스페이스 추가
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class DataManager : Singleton<DataManager>
{
    public Action OnDataReady;
    public Action OnStateDataReady;
    public Action<WorldNewsItem> OnNewsAdded;
    public Action OnStateTicked;

    /// <summary>런타임 맵·SO를 갱신한 뒤 천하 UI만 즉시 다시 그릴 때 호출(가짜 틱 제거 후).</summary>
    public void RequestWorldUiRefresh() => OnStateTicked?.Invoke();

    [Header("Master Data SO References")]
    [SerializeField] LevelRuleDataSo levelRuleDataSo;
    [SerializeField] CastleMasterDataSo castleMasterDataSo;
    [SerializeField] GeneralMasterDataSo generalMasterDataSo;
    [SerializeField] BuffMasterDataSo buffMasterDataSo;
    [SerializeField] NationMasterDataSo nationMasterDataSo;
    [SerializeField] RegionMasterDataSo regionMasterDataSo;

    [Header("경제 기본값 SO (선택)")]
    [Tooltip("GlobalEconomy 정적 필드 초기화.")]
    [SerializeField] GlobalEconomyDefaultsSo globalEconomyDefaultsSo;
    [Tooltip("castle_state.json 이 없을 때만 BuildStateDataFromMaster 뒤에 적용되는 성별 초기 AI/상태 덮어쓰기.")]
    [SerializeField] CastleWorldInitialScenarioSo castleWorldInitialScenarioSo;

    [Header("실시간 SO 미러 (천하·에디터)")]
    [Tooltip("전 성 실시간 상태. 런타임 갱신 시 에디터에서 SetDirty + 디바운스 SaveAssets.")]
    [SerializeField] CastleStateSo castleStateLiveSo;
    [Tooltip("유저 투자(성별 병력·평단) + 총 금화.")]
    [SerializeField] UserPortfolioSo userPortfolioLiveSo;

    [Header("Runtime Dictionaries")]

    // ★ Odin Inspector의 마법! 이 속성 하나로 딕셔너리가 인스펙터에 예쁘게 그려집니다.
    // [DictionaryDrawerSettings]를 추가하면 UI를 더 깔끔하게 다듬을 수 있습니다.
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "레벨", ValueLabel = "밸런스 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<int, LevelRuleData> levelRuleMap = new Dictionary<int, LevelRuleData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "성 ID", ValueLabel = "성 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, CastleMasterData> castleMasterDataMap = new Dictionary<string, CastleMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "장수 ID", ValueLabel = "장수 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, GeneralMasterData> generalMasterDataMap = new Dictionary<string, GeneralMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "버프 ID", ValueLabel = "버프 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, BuffMasterData> buffMasterDataMap = new Dictionary<string, BuffMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "세력 ID", ValueLabel = "세력 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, NationMasterData> nationMasterDataMap = new Dictionary<string, NationMasterData>();

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "지역 코드", ValueLabel = "지역 마스터 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, RegionMasterData> regionMasterDataMap = new Dictionary<string, RegionMasterData>();

    /// <summary>성 ID → 지역 코드(R01 등). <see cref="RebuildRegionCastleLookup"/>로 갱신.</summary>
    public Dictionary<string, string> castleIdToRegionIdMap = new Dictionary<string, string>();

    [Header("State Data (Runtime)")]
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "성 ID", ValueLabel = "성 상태 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, CastleStateData> castleStateDataMap = new Dictionary<string, CastleStateData>();

    [ShowInInspector]
    public List<WorldNewsItem> worldNews = new List<WorldNewsItem>();

    public bool IsReady { get; private set; } = false;
    public bool IsStateReady { get; private set; } = false;

    [Header("시세 (매수/매도 스프레드)")]
    [SerializeField, Range(0f, 0.2f)] float tradeSpread = 0.03f; // 매수/매도 스프레드(기본 3%)

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

#if UNITY_EDITOR
    float _nextEditorLiveSoSaveTime;
#endif

    const string StateSaveFileName = "castle_state.json";

    protected override void Awake()
    {
        base.Awake();
    }

    public void InitializeAllData()
    {
        ApplyGlobalEconomyDefaultsFromSoIfPresent();

        // 시트 파싱 결과가 비어있을 때를 대비해 SO에서 딕셔너리를 보강합니다.
        SyncRuntimeMapsFromSo();
        IsReady = true;
        OnDataReady?.Invoke(); // 구독 중인 UI(UpgradeButton 등)가 초기화되도록 호출
        Debug.Log($"[DataManager] 데이터 세팅 완료! 레벨룰 {levelRuleMap.Count}개, 성 {castleMasterDataMap.Count}개, 장수 {generalMasterDataMap.Count}개, 버프 {buffMasterDataMap.Count}개, 세력 {nationMasterDataMap.Count}개, 지역 {regionMasterDataMap.Count}개를 로드했습니다.");

        InitializeStateData();
    }

    void Update()
    {
        if (!IsReady || !IsStateReady) return;

        TickTravelGaugeIdle(Time.unscaledDeltaTime);
        TickCastleDailyHistoryRollover();

        float now = Time.unscaledTime;
        if (_stateDirty && now >= _nextSaveAt)
        {
            _nextSaveAt = now + 15f;
            SaveStateDataToDisk();
        }
    }

    public LevelRuleData GetLevelData(int level)
    {
        if (levelRuleMap.TryGetValue(level, out LevelRuleData data)) return data;
        return null;
    }

    public CastleMasterData GetCastleMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (castleMasterDataMap.TryGetValue(id.Trim(), out CastleMasterData data)) return data;
        return null;
    }

    public GeneralMasterData GetGeneralMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (generalMasterDataMap.TryGetValue(id.Trim(), out GeneralMasterData data)) return data;
        return null;
    }

    public BuffMasterData GetBuffMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (buffMasterDataMap.TryGetValue(id.Trim(), out BuffMasterData data)) return data;
        return null;
    }

    public NationMasterData GetNationMasterData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (nationMasterDataMap.TryGetValue(id.Trim(), out NationMasterData data)) return data;
        return null;
    }

    public RegionMasterData GetRegionMasterData(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return null;
        if (regionMasterDataMap.TryGetValue(regionId.Trim(), out RegionMasterData data)) return data;
        return null;
    }

    /// <summary>성 마스터 ID로 소속 지역(R01 등)을 조회합니다.</summary>
    public bool TryGetRegionIdForCastle(string castleId, out string regionId)
    {
        regionId = null;
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        return castleIdToRegionIdMap.TryGetValue(castleId.Trim(), out regionId) && !string.IsNullOrEmpty(regionId);
    }

    /// <summary>성 ID로 <see cref="RegionMasterData"/>를 조회합니다.</summary>
    public bool TryGetRegionByCastleId(string castleId, out RegionMasterData region)
    {
        region = null;
        if (!TryGetRegionIdForCastle(castleId, out string rid)) return false;
        return regionMasterDataMap.TryGetValue(rid, out region) && region != null;
    }

    /// <summary>UI·뉴스용 성 표시명 — R01/C01 같은 코드 대신 실제 성명·지역 문자열·섹터명을 우선합니다.</summary>
    public string GetCastleDisplayName(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return "";
        castleId = castleId.Trim();
        if (!castleMasterDataMap.TryGetValue(castleId, out var m) || m == null) return "";
        TryGetRegionByCastleId(castleId, out var byCastle);
        RegionMasterData byRidField = null;
        string rf = (m.regionId ?? "").Trim();
        if (!string.IsNullOrEmpty(rf))
            byRidField = GetRegionMasterData(rf);
        return CastleDisplayLabels.GetCastleTitle(m, byCastle, byRidField);
    }

    /// <summary>천하 카드 부제 등 — 지역(섹터) 표시명. 제목과 같으면 빈 문자열.</summary>
    public string GetCastleRegionSubtitle(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return "";
        castleId = castleId.Trim();
        if (!castleMasterDataMap.TryGetValue(castleId, out var m) || m == null) return "";
        TryGetRegionByCastleId(castleId, out var byCastle);
        RegionMasterData byRidField = null;
        string rf = (m.regionId ?? "").Trim();
        if (!string.IsNullOrEmpty(rf))
            byRidField = GetRegionMasterData(rf);
        string title = CastleDisplayLabels.GetCastleTitle(m, byCastle, byRidField);
        return CastleDisplayLabels.GetRegionSubtitle(m, byCastle, byRidField, title);
    }

    public void RebuildRegionCastleLookup()
    {
        castleIdToRegionIdMap.Clear();
        foreach (var kv in regionMasterDataMap)
        {
            string rid = kv.Key;
            var r = kv.Value;
            if (r == null || r.castleIds == null) continue;
            for (int i = 0; i < r.castleIds.Count; i++)
            {
                string cid = r.castleIds[i];
                if (string.IsNullOrWhiteSpace(cid)) continue;
                castleIdToRegionIdMap[cid.Trim()] = rid;
            }
        }
    }

    // ========================================================================
    // State Data Initialization / Save / Load
    // ========================================================================
    public void InitializeStateData()
    {
        if (!IsReady) return;

        bool loaded = LoadStateDataFromDisk();
        if (!loaded)
            BuildStateDataFromMaster();

        RecalculateAllPrices();

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            castleMasterDataMap.TryGetValue(s.id, out var master);
            EnsureCastleHistorySeeded(s, master);
            if (s.buyPricePrevDayClose < 0.5f)
                s.buyPricePrevDayClose = CalculateBuyPrice(s);
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
            castleStateDataMap[s.id] = s;
        }

        ApplyInitialGovernorsFromGenerals();
        ApplyCastleWorldInitialScenarioIfPresent();

        worldNews.Clear();
        AddNews($"[INIT] StateData 생성 완료 ({castleStateDataMap.Count}개 성).");
        _stateDirty = true;
    }

    /// <summary>
    /// <see cref="userPortfolioLiveSo"/> 보유(없으면 런타임 맵) + 라이브 매도가 기준 수익률(%). 미보유·평단 0이면 false.
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

        float sell = 0f;
        if (TryGetLiveCastleState(castleId, out var live) && live != null)
            sell = live.currentSellPrice;
        else if (castleStateDataMap.TryGetValue(castleId, out var s) && s != null)
            sell = s.currentSellPrice;
        else
            return false;

        roiPercent = (sell - avg) / avg * 100f;
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
                    currentSellPrice = s.currentSellPrice,
                    userDeployedTroops = s.userDeployedTroops,
                    averagePurchasePrice = s.averagePurchasePrice,
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
            userPortfolioLiveSo.totalGold = gm != null && gm.currentUser != null ? gm.currentUser.gold : 0L;
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

    void ApplyGlobalEconomyDefaultsFromSoIfPresent()
    {
        if (globalEconomyDefaultsSo == null) return;
        GlobalEconomy.totalServerSoldiers = globalEconomyDefaultsSo.initialTotalServerSoldiers;
        GlobalEconomy.grainPriceIndex = globalEconomyDefaultsSo.initialGrainPriceIndex;
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
                castleStateDataMap[s.id] = s;
            }

            worldNews = payload.news ?? new List<WorldNewsItem>();

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
                        buyPricePrevDayClose = 0f
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

    public void SaveStateDataToDisk()
    {
        try
        {
            var payload = new CastleStateSavePayload
            {
                castles = castleStateDataMap.Values.ToList(),
                news = worldNews ?? new List<WorldNewsItem>()
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
    /// 천하 탭 상단 필터별 목록. 필터 적용 후 공통 정렬: 이슈(전쟁·재해·호재) &gt; 내 투자 성 &gt; 등급.
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

    static int MtsIssueSortKeyLive(CastleStateSo.CastleLiveStateEntry e)
    {
        if (e == null) return 0;
        if (e.isWar || e.isDisaster) return 2;
        if (e.isFavorableEvent) return 1;
        return 0;
    }

    List<string> OrderWorldCastle_LiveSo(IEnumerable<CastleStateSo.CastleLiveStateEntry> q)
    {
        string home = (_homeCastleId ?? "").Trim();
        return q.OrderByDescending(MtsIssueSortKeyLive)
            .ThenByDescending(e => !string.IsNullOrEmpty(home) &&
                                   string.Equals(e.castleId.Trim(), home, StringComparison.Ordinal))
            .ThenByDescending(e => HasUserStockInPortfolio(e.castleId))
            .ThenBy(e => GetCastleGradeSortKey(e.castleId))
            .ThenBy(e => e.castleId.Trim(), StringComparer.Ordinal)
            .Select(e => e.castleId.Trim())
            .ToList();
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

    /// <summary>MTS 공통 정렬: 이슈(전쟁·재해 우선, 호재 보조) &gt; 내 투자 성 &gt; 등급 &gt; ID.</summary>
    static int MtsIssueSortKey(CastleStateData s)
    {
        if (s == null) return 0;
        if (s.isWar || s.isDisaster) return 2;
        if (s.isFavorableEvent) return 1;
        return 0;
    }

    bool IsPremiumCastleId(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        if (!castleMasterDataMap.TryGetValue(castleId.Trim(), out var m) || m == null) return false;
        return m.grade <= Grade.A;
    }

    List<string> OrderWorldCastle_MtsDefault(IEnumerable<CastleStateData> q) =>
        q.OrderByDescending(MtsIssueSortKey)
            .ThenByDescending(s => s.userDeployedTroops > 0)
            .ThenBy(s => GetCastleGradeSortKey(s.id))
            .ThenBy(s => s.id, StringComparer.Ordinal)
            .Select(s => s.id)
            .ToList();

    int GetCastleGradeSortKey(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return 99;
        if (!castleMasterDataMap.TryGetValue(castleId.Trim(), out var m) || m == null) return 99;
        return (int)m.grade;
    }

    /// <summary> 병력 추가 시 가중 평균으로 averagePurchasePrice 갱신. </summary>
    public void AddUserCastleDeployment(string castleId, int additionalTroops, float pricePerTroop)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || additionalTroops <= 0) return;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return;

        long newTotal = (long)s.userDeployedTroops + additionalTroops;
        if (newTotal > int.MaxValue) newTotal = int.MaxValue;

        if (s.userDeployedTroops <= 0)
            s.averagePurchasePrice = pricePerTroop;
        else
        {
            double sumCost = s.averagePurchasePrice * s.userDeployedTroops + pricePerTroop * additionalTroops;
            s.averagePurchasePrice = (float)(sumCost / newTotal);
        }

        s.userDeployedTroops = (int)newTotal;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    /// <summary>천하 탭 [회수]: 해당 성에 투입한 병력을 모두 철수합니다.</summary>
    public void RecallUserCastleDeployment(string castleId)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId)) return;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return;
        if (s.userDeployedTroops <= 0) return;
        s.userDeployedTroops = 0;
        s.averagePurchasePrice = 0f;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    /// <summary>
    /// 모든 성의 유저 투입 병력·매수 평단을 0으로 초기화. 라이브 SO 반영·즉시 디스크 저장·UI 갱신.
    /// (에디터 메뉴·테스트용)
    /// </summary>
    public void ClearAllUserCastleDeployments()
    {
        if (!IsStateReady || castleStateDataMap == null) return;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
        }

        _stateDirty = true;
        FlushLiveScriptableObjects();
        SaveStateDataToDisk();
        OnStateTicked?.Invoke();
    }

    // ========================================================================
    // Price calculation (Buy/Sell split) + Buff hooks
    // ========================================================================
    public void RecalculateAllPrices()
    {
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            s.currentBuyPrice = CalculateBuyPrice(s);
            s.currentSellPrice = CalculateSellPrice(s);
        }
    }

    float CalculateBuyPrice(CastleStateData s)
    {
        float basePrice = CalculateBasePrice(s);
        float buffMul = 1f + GetGovernorValueBonus(s.currentGovernorId, isBuy: true);
        return Mathf.Max(0f, basePrice * buffMul * (1f + tradeSpread));
    }

    float CalculateSellPrice(CastleStateData s)
    {
        float basePrice = CalculateBasePrice(s);
        float buffMul = 1f + GetGovernorValueBonus(s.currentGovernorId, isBuy: false);
        return Mathf.Max(0f, basePrice * buffMul * (1f - tradeSpread));
    }

    float CalculateBasePrice(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return 0f;
        if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var master) || master == null) return 0f;

        float gradeW = GradeWeight(master.grade);
        float sentimentMul = Mathf.Clamp01(s.currentSentiment / 100f);
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

    float GetGovernorValueBonus(string governorId, bool isBuy)
    {
        var buff = GetGovernorBuff(governorId);
        if (buff == null) return 0f;

        // 기본: ValueMultiplier는 Buy/Sell 모두 적용
        if (buff.type == BuffType.ValueMultiplier)
            return buff.value;

        // ParValueModifier: 액면가(매수 기준) 할인 — value=0.1이면 매수가 10% 유리 (multiplier -0.1)
        if (isBuy && buff.type == BuffType.ParValueModifier)
            return -Mathf.Abs(buff.value);

        return 0f;
    }

    BuffMasterData GetGovernorBuff(string governorId)
    {
        if (string.IsNullOrWhiteSpace(governorId)) return null;
        if (!generalMasterDataMap.TryGetValue(governorId.Trim(), out var g) || g == null) return null;
        if (string.IsNullOrWhiteSpace(g.buffId)) return null;
        if (!buffMasterDataMap.TryGetValue(g.buffId.Trim(), out var buff) || buff == null) return null;
        return buff;
    }

    // ========================================================================
    // News
    // ========================================================================
    void AddNews(string text)
    {
        var item = new WorldNewsItem
        {
            unixTime = TimeManager.GetUnixNow(),
            text = text
        };
        if (worldNews == null) worldNews = new List<WorldNewsItem>();
        worldNews.Add(item);
        while (worldNews.Count > 80)
            worldNews.RemoveAt(0);
        OnNewsAdded?.Invoke(item);
    }

    string GetCastleNameOrId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "(unknown)";
        string d = GetCastleDisplayName(id.Trim());
        if (!string.IsNullOrEmpty(d) && d != "성") return d;
        return id.Trim();
    }

    public void SyncRuntimeMapsFromSo()
    {
        SyncLevelRuleFromSoIfNeeded();
        SyncCastleFromSoIfNeeded();
        SyncGeneralFromSoIfNeeded();
        SyncBuffFromSoIfNeeded();
        SyncNationFromSoIfNeeded();
        SyncRegionFromSoIfNeeded();
    }

    public void SyncSoFromRuntimeMaps()
    {
        if (levelRuleDataSo != null)
            levelRuleDataSo.list = levelRuleMap.Values.OrderBy(x => x.level).ToList();

        if (castleMasterDataSo != null)
            castleMasterDataSo.list = castleMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (generalMasterDataSo != null)
            generalMasterDataSo.list = generalMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (buffMasterDataSo != null)
            buffMasterDataSo.list = buffMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (nationMasterDataSo != null)
            nationMasterDataSo.list = nationMasterDataMap.Values.OrderBy(x => x.id).ToList();

        if (regionMasterDataSo != null)
            regionMasterDataSo.list = regionMasterDataMap.Values.OrderBy(x => x.id).ToList();

#if UNITY_EDITOR
        if (levelRuleDataSo != null) EditorUtility.SetDirty(levelRuleDataSo);
        if (castleMasterDataSo != null) EditorUtility.SetDirty(castleMasterDataSo);
        if (generalMasterDataSo != null) EditorUtility.SetDirty(generalMasterDataSo);
        if (buffMasterDataSo != null) EditorUtility.SetDirty(buffMasterDataSo);
        if (nationMasterDataSo != null) EditorUtility.SetDirty(nationMasterDataSo);
        if (regionMasterDataSo != null) EditorUtility.SetDirty(regionMasterDataSo);
        AssetDatabase.SaveAssets();
#endif
    }

    void SyncLevelRuleFromSoIfNeeded()
    {
        if (levelRuleMap.Count > 0 || levelRuleDataSo == null || levelRuleDataSo.list == null)
            return;

        for (int i = 0; i < levelRuleDataSo.list.Count; i++)
        {
            var item = levelRuleDataSo.list[i];
            if (item == null)
                continue;
            levelRuleMap[item.level] = item;
        }
    }

    void SyncCastleFromSoIfNeeded()
    {
        if (castleMasterDataMap.Count > 0 || castleMasterDataSo == null || castleMasterDataSo.list == null)
            return;

        for (int i = 0; i < castleMasterDataSo.list.Count; i++)
        {
            var item = castleMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            castleMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncGeneralFromSoIfNeeded()
    {
        if (generalMasterDataMap.Count > 0 || generalMasterDataSo == null || generalMasterDataSo.list == null)
            return;

        for (int i = 0; i < generalMasterDataSo.list.Count; i++)
        {
            var item = generalMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            generalMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncBuffFromSoIfNeeded()
    {
        if (buffMasterDataMap.Count > 0 || buffMasterDataSo == null || buffMasterDataSo.list == null)
            return;

        for (int i = 0; i < buffMasterDataSo.list.Count; i++)
        {
            var item = buffMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            buffMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncNationFromSoIfNeeded()
    {
        if (nationMasterDataMap.Count > 0 || nationMasterDataSo == null || nationMasterDataSo.list == null)
            return;

        for (int i = 0; i < nationMasterDataSo.list.Count; i++)
        {
            var item = nationMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            nationMasterDataMap[item.id.Trim()] = item;
        }
    }

    void SyncRegionFromSoIfNeeded()
    {
        if (regionMasterDataMap.Count > 0 || regionMasterDataSo == null || regionMasterDataSo.list == null)
        {
            RebuildRegionCastleLookup();
            return;
        }

        for (int i = 0; i < regionMasterDataSo.list.Count; i++)
        {
            var item = regionMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            regionMasterDataMap[item.id.Trim()] = item;
        }

        RebuildRegionCastleLookup();
    }
}