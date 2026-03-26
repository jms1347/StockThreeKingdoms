using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Sirenix.OdinInspector; // Odin 네임스페이스 추가
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataManager : Singleton<DataManager>
{
    public Action OnDataReady;
    public Action OnStateDataReady;
    public Action<WorldNewsItem> OnNewsAdded;
    public Action OnStateTicked;

    [Header("Master Data SO References")]
    [SerializeField] LevelRuleDataSo levelRuleDataSo;
    [SerializeField] CastleMasterDataSo castleMasterDataSo;
    [SerializeField] GeneralMasterDataSo generalMasterDataSo;
    [SerializeField] BuffMasterDataSo buffMasterDataSo;

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

    [Header("State Data (Runtime)")]
    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "성 ID", ValueLabel = "성 상태 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, CastleStateData> castleStateDataMap = new Dictionary<string, CastleStateData>();

    [ShowInInspector]
    public List<WorldNewsItem> worldNews = new List<WorldNewsItem>();

    public bool IsReady { get; private set; } = false;
    public bool IsStateReady { get; private set; } = false;

    [Header("Economy Engine (Emulator)")]
    [SerializeField] float economyTickIntervalSec = 5f;
    [SerializeField] float randomEventIntervalSec = 30f;
    [SerializeField, Range(0f, 1f)] float randomEventChance = 0.10f;
    [SerializeField, Range(0f, 0.05f)] float populationJitterPct = 0.005f; // ±0.5%
    [SerializeField] float sentimentJitterMax = 0.5f; // ±0.5
    [SerializeField, Range(0f, 0.5f)] float governorCharmBias = 0.15f; // 0이면 무시
    [SerializeField, Range(0f, 0.2f)] float tradeSpread = 0.03f; // 매수/매도 스프레드(기본 3%)

    float _nextEconomyTickAt;
    float _nextRandomEventAt;
    float _nextSaveAt;
    bool _stateDirty;

    const string StateSaveFileName = "castle_state.json";

    protected override void Awake()
    {
        base.Awake();
    }

    public void InitializeAllData()
    {
        // 시트 파싱 결과가 비어있을 때를 대비해 SO에서 딕셔너리를 보강합니다.
        SyncRuntimeMapsFromSo();
        IsReady = true;
        OnDataReady?.Invoke(); // 구독 중인 UI(UpgradeButton 등)가 초기화되도록 호출
        Debug.Log($"[DataManager] 데이터 세팅 완료! 레벨룰 {levelRuleMap.Count}개, 성 {castleMasterDataMap.Count}개, 장수 {generalMasterDataMap.Count}개, 버프 {buffMasterDataMap.Count}개를 로드했습니다.");

        InitializeStateData();
    }

    void Update()
    {
        if (!IsReady || !IsStateReady) return;

        float now = Time.unscaledTime;
        if (now >= _nextEconomyTickAt)
        {
            _nextEconomyTickAt = now + Mathf.Max(0.5f, economyTickIntervalSec);
            UpdateEconomyTick();
        }

        if (now >= _nextRandomEventAt)
        {
            _nextRandomEventAt = now + Mathf.Max(2f, randomEventIntervalSec);
            TryRandomEvent();
        }

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

        IsStateReady = true;
        OnStateDataReady?.Invoke();

        float now = Time.unscaledTime;
        _nextEconomyTickAt = now + Mathf.Max(0.5f, economyTickIntervalSec);
        _nextRandomEventAt = now + Mathf.Max(2f, randomEventIntervalSec);
        _nextSaveAt = now + 10f;
        _stateDirty = true; // 첫 저장 보장
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
            s.currentLord = master.initialLord;
            s.currentPopulation = master.initPopulation;
            s.currentSentiment = 100f;
            s.currentGovernorId = "";
            s.isWar = false;
            s.isDisaster = false;
            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
            s.sentimentHistory = new List<float>(7) { s.currentSentiment };
            castleStateDataMap[s.id] = s;
        }

        worldNews.Clear();
        AddNews($"[INIT] StateData 생성 완료 ({castleStateDataMap.Count}개 성).");
        _stateDirty = true;
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
                        currentLord = master.initialLord,
                        currentPopulation = master.initPopulation,
                        currentSentiment = 100f,
                        currentGovernorId = "",
                        isWar = false,
                        isDisaster = false,
                        userDeployedTroops = 0,
                        averagePurchasePrice = 0f,
                        sentimentHistory = new List<float>(7) { 100f }
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
    /// 천하 탭 성 리스트: 이슈(전쟁·재해) → 내 투자 성 → 등급(SS→D) → id.
    /// </summary>
    public List<string> GetOrderedWorldCastleIds()
    {
        if (castleStateDataMap == null || castleStateDataMap.Count == 0)
            return new List<string>();

        return castleStateDataMap.Values
            .Where(s => s != null)
            .OrderByDescending(s => s.isWar || s.isDisaster)
            .ThenByDescending(s => s.userDeployedTroops > 0)
            .ThenBy(s => GetCastleGradeSortKey(s.id))
            .ThenBy(s => s.id, StringComparer.Ordinal)
            .Select(s => s.id)
            .ToList();
    }

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
    }

    // ========================================================================
    // Economy Engine (client-side emulator)
    // ========================================================================
    public void UpdateEconomyTick()
    {
        if (!IsStateReady) return;

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;

            // Population: ±0.5% (기본) + 버프(성장) 약간 반영
            float popMul = 1f + UnityEngine.Random.Range(-populationJitterPct, populationJitterPct);
            float popBuff = GetGovernorPopulationGrowthBonus(s.currentGovernorId);
            popMul *= (1f + popBuff);
            int nextPop = Mathf.Max(0, Mathf.RoundToInt(s.currentPopulation * popMul));
            s.currentPopulation = nextPop;

            // Sentiment: ±0.5 기본 + 태수 Charm에 따른 상향 바이어스 + 버프(회복) 반영
            float delta = UnityEngine.Random.Range(-sentimentJitterMax, sentimentJitterMax);
            delta += GetGovernorCharmSentimentBias(s.currentGovernorId);
            delta += GetGovernorSentimentRecoveryBonus(s.currentGovernorId);

            s.currentSentiment = Mathf.Clamp(s.currentSentiment + delta, 0f, 100f);
            PushSentimentHistory(s);

            if (s.isDisaster && UnityEngine.Random.value < 0.04f)
                s.isDisaster = false;
        }

        RecalculateAllPrices();
        _stateDirty = true;
        OnStateTicked?.Invoke();
    }

    void TryRandomEvent()
    {
        if (castleStateDataMap.Count <= 0) return;
        if (UnityEngine.Random.value > randomEventChance) return;

        // 무작위 성 하나 선택
        int idx = UnityEngine.Random.Range(0, castleStateDataMap.Count);
        var s = castleStateDataMap.Values.ElementAt(idx);
        if (s == null) return;

        float roll = UnityEngine.Random.value;
        if (roll < 0.34f)
        {
            Faction old = s.currentLord;
            s.currentLord = RandomOtherFaction(old);
            AddNews($"[WAR] {s.currentLord}가(이) {GetCastleNameOrId(s.id)}을(를) 점령했습니다! (from {old})");
        }
        else if (roll < 0.67f)
        {
            s.isWar = !s.isWar;
            AddNews(s.isWar
                ? $"[URGENT] {GetCastleNameOrId(s.id)}에서 전쟁 발생!"
                : $"[INFO] {GetCastleNameOrId(s.id)} 전쟁 종료.");
        }
        else
        {
            s.isDisaster = !s.isDisaster;
            AddNews(s.isDisaster
                ? $"[DISASTER] {GetCastleNameOrId(s.id)}에 재해·병충이 확산합니다!"
                : $"[RECOVER] {GetCastleNameOrId(s.id)} 재해가 진정되었습니다.");
        }

        _stateDirty = true;
    }

    static Faction RandomOtherFaction(Faction old)
    {
        // NONE 제외하고 3대 세력 + OTHERS 중 랜덤
        Faction[] pool = { Faction.WEI, Faction.SHU, Faction.WU, Faction.OTHERS };
        for (int i = 0; i < 8; i++)
        {
            var f = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (f != old) return f;
        }
        return old == Faction.WEI ? Faction.SHU : Faction.WEI;
    }

    void PushSentimentHistory(CastleStateData s)
    {
        if (s.sentimentHistory == null) s.sentimentHistory = new List<float>(7);
        s.sentimentHistory.Add(s.currentSentiment);
        while (s.sentimentHistory.Count > 7)
            s.sentimentHistory.RemoveAt(0);
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

    float GetGovernorCharmSentimentBias(string governorId)
    {
        if (string.IsNullOrWhiteSpace(governorId) || governorCharmBias <= 0f) return 0f;
        if (!generalMasterDataMap.TryGetValue(governorId.Trim(), out var g) || g == null) return 0f;
        // charm 0~100 기준: 최대 +governorCharmBias * sentimentJitterMax 정도까지 편향
        float t = Mathf.Clamp01(g.charm / 100f);
        return sentimentJitterMax * governorCharmBias * t;
    }

    float GetGovernorSentimentRecoveryBonus(string governorId)
    {
        var buff = GetGovernorBuff(governorId);
        if (buff == null) return 0f;
        if (buff.type != BuffType.SentimentRecovery) return 0f;
        return buff.value; // value를 +sentiment delta로 직접 사용(시트에서 0.1~0.5 같은 값 추천)
    }

    float GetGovernorPopulationGrowthBonus(string governorId)
    {
        var buff = GetGovernorBuff(governorId);
        if (buff == null) return 0f;
        if (buff.type != BuffType.PopulationGrowth) return 0f;
        return buff.value; // value를 성장률로 사용(예: 0.02 = +2%)
    }

    float GetGovernorValueBonus(string governorId, bool isBuy)
    {
        var buff = GetGovernorBuff(governorId);
        if (buff == null) return 0f;

        // 기본: ValueMultiplier는 Buy/Sell 모두 적용
        if (buff.type == BuffType.ValueMultiplier)
            return buff.value;

        // 확장: BuyDiscount는 Buy쪽에만 유리 (value=0.1이면 -10% → multiplier 관점에선 -0.1)
        if (isBuy && buff.type == BuffType.BuyDiscount)
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
        if (castleMasterDataMap.TryGetValue(id.Trim(), out var m) && m != null && !string.IsNullOrWhiteSpace(m.name))
            return m.name;
        return id.Trim();
    }

    public void SyncRuntimeMapsFromSo()
    {
        SyncLevelRuleFromSoIfNeeded();
        SyncCastleFromSoIfNeeded();
        SyncGeneralFromSoIfNeeded();
        SyncBuffFromSoIfNeeded();
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

#if UNITY_EDITOR
        if (levelRuleDataSo != null) EditorUtility.SetDirty(levelRuleDataSo);
        if (castleMasterDataSo != null) EditorUtility.SetDirty(castleMasterDataSo);
        if (generalMasterDataSo != null) EditorUtility.SetDirty(generalMasterDataSo);
        if (buffMasterDataSo != null) EditorUtility.SetDirty(buffMasterDataSo);
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
}