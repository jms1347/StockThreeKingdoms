using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 구글 시트·SO로부터 채워지는 <b>고정 마스터 데이터</b> 전용 매니저.
/// 변동 상태(<see cref="CastleStateData"/> 등)는 <see cref="DataManager"/>가 담당합니다.
/// </summary>
public class FixedSoDataManager : Singleton<FixedSoDataManager>
{
    [Header("Master Data SO References")]
    [SerializeField] LevelRuleDataSo levelRuleDataSo;
    [SerializeField] CastleMasterDataSo castleMasterDataSo;
    [SerializeField] GeneralMasterDataSo generalMasterDataSo;
    [Tooltip("BuffMaster 시트 TSV. A:id, B:name, C:CastleStatType, D:CurveType, E:value, F:description")]
    [SerializeField] BuffMasterDataSo buffMasterDataSo;
    [SerializeField] NationMasterDataSo nationMasterDataSo;
    [SerializeField] RegionMasterDataSo regionMasterDataSo;
    [SerializeField] EventMasterDataSo eventMasterDataSo;
    [Tooltip("조건 라이브러리(Condition 탭). 시트 반영 시 list가 갱신됩니다.")]
    [SerializeField] ConditionDataSo conditionDataSo;
    [Tooltip("이벤트별 스탯 확률 보정(EventStatModifier 탭). eventId → 보정치.")]
    [SerializeField] EventStatModifierSo eventStatModifierSo;
    [Tooltip("뉴스 마스터(기사 분리). newsCode → headline/script/icon")]
    [SerializeField] NewsMasterDataSo newsMasterDataSo;
    [Tooltip("무작위 방문객 이벤트. A:id, B:visitorType, C:probability, D:effectReward")]
    [SerializeField] RandomVisitorDataSo randomVisitorDataSo;
    [Tooltip("만보기 미션. A:step, B:targetSteps, C:mpReward, D:remarks")]
    [SerializeField] StepMissionDataSo stepMissionDataSo;

    [Header("경제·월드 초기값 SO (선택)")]
    [Tooltip("GlobalEconomy 정적 필드 초기화.")]
    [SerializeField] GlobalEconomyDefaultsSo globalEconomyDefaultsSo;

    [Header("Runtime Master Maps (시트 파싱 / SO 동기화)")]
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

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "이벤트 ID", ValueLabel = "행 목록", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, List<EventMasterData>> eventMasterDataMap =
        new Dictionary<string, List<EventMasterData>>(StringComparer.Ordinal);

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "condId", ValueLabel = "ConditionData", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, ConditionData> conditionDataMap = new Dictionary<string, ConditionData>(StringComparer.Ordinal);

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "eventId", ValueLabel = "EventStatModifierData", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, EventStatModifierData> eventStatModifierMap =
        new Dictionary<string, EventStatModifierData>(StringComparer.Ordinal);

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "newsCode", ValueLabel = "NewsMasterData", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, NewsMasterData> newsMasterDataMap =
        new Dictionary<string, NewsMasterData>(StringComparer.Ordinal);

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "이벤트 ID", ValueLabel = "방문객 데이터", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<string, RandomVisitorData> randomVisitorMap =
        new Dictionary<string, RandomVisitorData>(StringComparer.Ordinal);

    [ShowInInspector]
    [DictionaryDrawerSettings(KeyLabel = "단계", ValueLabel = "만보기 미션", DisplayMode = DictionaryDisplayOptions.ExpandedFoldout)]
    public Dictionary<int, StepMissionData> stepMissionMap = new Dictionary<int, StepMissionData>();

    /// <summary>성 ID → 지역 코드(R01 등). <see cref="RebuildRegionCastleLookup"/>로 갱신.</summary>
    public Dictionary<string, string> castleIdToRegionIdMap = new Dictionary<string, string>();

    readonly Dictionary<string, NewsTemplateSheetRow> _newsTemplateSheetByEventId =
        new Dictionary<string, NewsTemplateSheetRow>(StringComparer.Ordinal);

    public LevelRuleData GetLevelData(int level)
    {
        if (levelRuleMap.TryGetValue(level, out LevelRuleData data)) return data;
        return null;
    }

    public RandomVisitorData GetRandomVisitor(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (randomVisitorMap.TryGetValue(id.Trim(), out RandomVisitorData data)) return data;
        return null;
    }

    public StepMissionData GetStepMission(int step)
    {
        if (stepMissionMap.TryGetValue(step, out StepMissionData data)) return data;
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

    public void MergeBuffMasterFromSoMissingKeys()
    {
        if (buffMasterDataSo == null || buffMasterDataSo.list == null) return;
        for (int i = 0; i < buffMasterDataSo.list.Count; i++)
        {
            var b = buffMasterDataSo.list[i];
            if (b == null || string.IsNullOrWhiteSpace(b.id)) continue;
            string id = b.id.Trim();
            if (!buffMasterDataMap.ContainsKey(id))
                buffMasterDataMap[id] = b;
        }
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

    public EventMasterData GetEventMasterData(string eventId)
    {
        if (TryGetEventMasterRows(eventId, out var rows) && rows != null && rows.Count > 0)
            return rows[0];
        return null;
    }

    public bool TryGetEventMasterRows(string eventId, out List<EventMasterData> rows)
    {
        rows = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        if (!eventMasterDataMap.TryGetValue(eventId.Trim(), out rows) || rows == null || rows.Count == 0)
            return false;
        return true;
    }

    public void AddEventMasterDataRow(EventMasterData row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.id)) return;
        string id = row.id.Trim();
        if (!eventMasterDataMap.TryGetValue(id, out var list) || list == null)
        {
            list = new List<EventMasterData>();
            eventMasterDataMap[id] = list;
        }

        list.Add(row);
    }

    public bool TryGetRegionIdForCastle(string castleId, out string regionId)
    {
        regionId = null;
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        return castleIdToRegionIdMap.TryGetValue(castleId.Trim(), out regionId) && !string.IsNullOrEmpty(regionId);
    }

    public bool TryGetRegionByCastleId(string castleId, out RegionMasterData region)
    {
        region = null;
        if (!TryGetRegionIdForCastle(castleId, out string rid)) return false;
        return regionMasterDataMap.TryGetValue(rid, out region) && region != null;
    }

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

    public void ApplyGlobalEconomyDefaultsFromSoIfPresent()
    {
        if (globalEconomyDefaultsSo == null) return;
        GlobalEconomy.totalServerSoldiers = globalEconomyDefaultsSo.initialTotalServerSoldiers;
    }

    public void ClearConditionLibrary()
    {
        if (conditionDataMap == null)
            conditionDataMap = new Dictionary<string, ConditionData>(StringComparer.Ordinal);
        else
            conditionDataMap.Clear();

        if (conditionDataSo != null && conditionDataSo.list != null)
            conditionDataSo.list.Clear();
    }

    public void ApplyParsedConditionLibrary(List<ConditionData> rows)
    {
        if (conditionDataMap == null)
            conditionDataMap = new Dictionary<string, ConditionData>(StringComparer.Ordinal);
        conditionDataMap.Clear();

        var list = rows ?? new List<ConditionData>();
        if (conditionDataSo != null)
            conditionDataSo.list = list;

        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c == null || string.IsNullOrWhiteSpace(c.conditionId)) continue;
            string id = c.conditionId.Trim();
            conditionDataMap[id] = c;
        }
    }

    public void ApplyParsedEventStatModifier(List<EventStatModifierData> rows)
    {
        if (eventStatModifierMap == null)
            eventStatModifierMap = new Dictionary<string, EventStatModifierData>(StringComparer.Ordinal);
        eventStatModifierMap.Clear();

        var list = rows ?? new List<EventStatModifierData>();
        if (eventStatModifierSo != null)
            eventStatModifierSo.list = list;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrWhiteSpace(m.id)) continue;
            eventStatModifierMap[m.id.Trim()] = m;
        }
    }

    public void MergeEventStatModifierFromSoMissingKeys()
    {
        if (eventStatModifierSo == null || eventStatModifierSo.list == null) return;
        for (int i = 0; i < eventStatModifierSo.list.Count; i++)
        {
            var m = eventStatModifierSo.list[i];
            if (m == null || string.IsNullOrWhiteSpace(m.id)) continue;
            string id = m.id.Trim();
            if (!eventStatModifierMap.ContainsKey(id))
                eventStatModifierMap[id] = m;
        }
    }

    public bool TryGetNewsMasterData(string newsCode, out NewsMasterData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(newsCode)) return false;
        return newsMasterDataMap.TryGetValue(newsCode.Trim(), out data) && data != null;
    }

    public void ApplyParsedNewsMaster(List<NewsMasterData> rows)
    {
        if (newsMasterDataMap == null)
            newsMasterDataMap = new Dictionary<string, NewsMasterData>(StringComparer.Ordinal);
        newsMasterDataMap.Clear();

        var list = rows ?? new List<NewsMasterData>();
        if (newsMasterDataSo != null)
            newsMasterDataSo.list = list;

        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (n == null || string.IsNullOrWhiteSpace(n.newsCode)) continue;
            newsMasterDataMap[n.newsCode.Trim()] = n;
        }
    }

    public void MergeNewsMasterFromSoMissingKeys()
    {
        if (newsMasterDataSo == null || newsMasterDataSo.list == null) return;
        for (int i = 0; i < newsMasterDataSo.list.Count; i++)
        {
            var n = newsMasterDataSo.list[i];
            if (n == null || string.IsNullOrWhiteSpace(n.newsCode)) continue;
            string id = n.newsCode.Trim();
            if (!newsMasterDataMap.ContainsKey(id))
                newsMasterDataMap[id] = n;
        }
    }

    void RebuildConditionDataMapFromSoList()
    {
        if (conditionDataMap == null)
            conditionDataMap = new Dictionary<string, ConditionData>(StringComparer.Ordinal);
        conditionDataMap.Clear();
        if (conditionDataSo?.list == null) return;
        for (int i = 0; i < conditionDataSo.list.Count; i++)
        {
            var c = conditionDataSo.list[i];
            if (c == null || string.IsNullOrWhiteSpace(c.conditionId)) continue;
            conditionDataMap[c.conditionId.Trim()] = c;
        }
    }

    public void ClearNewsTemplateSheetRows() => _newsTemplateSheetByEventId.Clear();

    public void SetNewsTemplateSheetRow(string eventId, NewsTemplateSheetRow row)
    {
        if (string.IsNullOrWhiteSpace(eventId) || row == null) return;
        _newsTemplateSheetByEventId[eventId.Trim()] = row;
    }

    public bool TryGetNewsTemplateSheetRow(string eventId, out NewsTemplateSheetRow row)
    {
        row = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        return _newsTemplateSheetByEventId.TryGetValue(eventId.Trim(), out row);
    }

    public void SyncRuntimeMapsFromSo()
    {
        SyncLevelRuleFromSoIfNeeded();
        SyncCastleFromSoIfNeeded();
        SyncGeneralFromSoIfNeeded();
        SyncBuffFromSoIfNeeded();
        SyncNationFromSoIfNeeded();
        SyncRegionFromSoIfNeeded();
        SyncEventFromSoIfNeeded();
        SyncEventStatModifierFromSoIfNeeded();
        SyncNewsMasterFromSoIfNeeded();
        SyncConditionFromSoIfNeeded();
        SyncRandomVisitorFromSoIfNeeded();
        SyncStepMissionFromSoIfNeeded();
        ApplyCastleMasterDerivedDefaults();
    }

    void ApplyCastleMasterDerivedDefaults()
    {
        if (castleMasterDataMap == null) return;
        foreach (var kv in castleMasterDataMap)
        {
            if (kv.Value == null) continue;
            kv.Value.EnsureDerivedDefaults();
        }
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

        if (eventMasterDataSo != null)
        {
            var flat = new List<EventMasterData>();
            foreach (var kv in eventMasterDataMap.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (kv.Value == null) continue;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    var r = kv.Value[i];
                    if (r != null) flat.Add(r);
                }
            }

            eventMasterDataSo.list = flat;
        }

        if (conditionDataSo != null)
        {
            conditionDataSo.list = conditionDataMap.Count == 0
                ? new List<ConditionData>()
                : conditionDataMap.Values.OrderBy(x => x.conditionId ?? "", StringComparer.Ordinal).ToList();
        }

        if (eventStatModifierSo != null)
        {
            eventStatModifierSo.list = eventStatModifierMap.Count == 0
                ? new List<EventStatModifierData>()
                : eventStatModifierMap.Values.OrderBy(x => x.id ?? "", StringComparer.Ordinal).ToList();
        }

        if (newsMasterDataSo != null)
        {
            newsMasterDataSo.list = newsMasterDataMap.Count == 0
                ? new List<NewsMasterData>()
                : newsMasterDataMap.Values.OrderBy(x => x.newsCode ?? "", StringComparer.Ordinal).ToList();
        }

        if (randomVisitorDataSo != null)
            randomVisitorDataSo.list = randomVisitorMap.Count == 0
                ? new List<RandomVisitorData>()
                : randomVisitorMap.Values.OrderBy(x => x.id ?? "", StringComparer.Ordinal).ToList();

        if (stepMissionDataSo != null)
            stepMissionDataSo.list = stepMissionMap.Count == 0
                ? new List<StepMissionData>()
                : stepMissionMap.Values.OrderBy(x => x.step).ToList();

#if UNITY_EDITOR
        if (levelRuleDataSo != null) EditorUtility.SetDirty(levelRuleDataSo);
        if (castleMasterDataSo != null) EditorUtility.SetDirty(castleMasterDataSo);
        if (generalMasterDataSo != null) EditorUtility.SetDirty(generalMasterDataSo);
        if (buffMasterDataSo != null) EditorUtility.SetDirty(buffMasterDataSo);
        if (nationMasterDataSo != null) EditorUtility.SetDirty(nationMasterDataSo);
        if (regionMasterDataSo != null) EditorUtility.SetDirty(regionMasterDataSo);
        if (eventMasterDataSo != null) EditorUtility.SetDirty(eventMasterDataSo);
        if (conditionDataSo != null) EditorUtility.SetDirty(conditionDataSo);
        if (eventStatModifierSo != null) EditorUtility.SetDirty(eventStatModifierSo);
        if (newsMasterDataSo != null) EditorUtility.SetDirty(newsMasterDataSo);
        if (randomVisitorDataSo != null) EditorUtility.SetDirty(randomVisitorDataSo);
        if (stepMissionDataSo != null) EditorUtility.SetDirty(stepMissionDataSo);
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

    void SyncEventFromSoIfNeeded()
    {
        if (eventMasterDataMap.Count > 0 || eventMasterDataSo == null || eventMasterDataSo.list == null)
            return;

        for (int i = 0; i < eventMasterDataSo.list.Count; i++)
        {
            var item = eventMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            AddEventMasterDataRow(item);
        }
    }

    void SyncEventStatModifierFromSoIfNeeded()
    {
        if (eventStatModifierMap.Count > 0 || eventStatModifierSo == null || eventStatModifierSo.list == null
            || eventStatModifierSo.list.Count == 0)
            return;

        for (int i = 0; i < eventStatModifierSo.list.Count; i++)
        {
            var item = eventStatModifierSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            eventStatModifierMap[item.id.Trim()] = item;
        }
    }

    void SyncNewsMasterFromSoIfNeeded()
    {
        if (newsMasterDataMap.Count > 0 || newsMasterDataSo == null || newsMasterDataSo.list == null
            || newsMasterDataSo.list.Count == 0)
            return;

        for (int i = 0; i < newsMasterDataSo.list.Count; i++)
        {
            var item = newsMasterDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.newsCode))
                continue;
            newsMasterDataMap[item.newsCode.Trim()] = item;
        }
    }

    void SyncConditionFromSoIfNeeded()
    {
        if (conditionDataMap.Count > 0 || conditionDataSo == null || conditionDataSo.list == null
            || conditionDataSo.list.Count == 0)
            return;

        RebuildConditionDataMapFromSoList();
    }

    void SyncRandomVisitorFromSoIfNeeded()
    {
        if (randomVisitorMap.Count > 0 || randomVisitorDataSo == null || randomVisitorDataSo.list == null)
            return;

        for (int i = 0; i < randomVisitorDataSo.list.Count; i++)
        {
            var item = randomVisitorDataSo.list[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;
            randomVisitorMap[item.id.Trim()] = item;
        }
    }

    void SyncStepMissionFromSoIfNeeded()
    {
        if (stepMissionMap.Count > 0 || stepMissionDataSo == null || stepMissionDataSo.list == null)
            return;

        for (int i = 0; i < stepMissionDataSo.list.Count; i++)
        {
            var item = stepMissionDataSo.list[i];
            if (item == null)
                continue;
            stepMissionMap[item.step] = item;
        }
    }
}
