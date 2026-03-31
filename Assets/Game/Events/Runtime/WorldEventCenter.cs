using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일간 월드 이벤트·뉴스 파이프라인(기획 연결 요약).
/// <list type="number">
/// <item><description>조건: <see cref="ConditionDataSo"/> condId 목록이 <see cref="EventMasterData.conditionIds"/>에 들어가며,
/// 한 행 안에서는 <b>AND</b>, 같은 eventId의 여러 행은 <b>OR</b>(<see cref="SelectRandomSatisfiedRow"/>).</description></item>
/// <item><description>당첨 가중치: 태수 <see cref="GeneralMasterDataSo"/> 스탯 + <see cref="EventStatModifierSo"/> 행(eventId)의 flat/perStat 보정 →
/// <see cref="CalculateFinalWeight"/> 후 /100으로 일일 확률·소문 확정 재주사위(임시, 추후 조정 가능).</description></item>
/// <item><description>발생 시 버프: <see cref="BuffMasterDataSo"/> 코드를 <see cref="WorldEventBuffApplier"/>로 적용(일차·duration 규칙은 버프 마스터).</description></item>
/// <item><description>기사: <see cref="NewsMasterDataSo"/> 코드가 소문(<see cref="EventMasterData.rumorNewsCodes"/>)·속보(<see cref="EventMasterData.breakingNewsCodes"/>)에 매핑.
/// <c>Direct</c> 태그는 즉시 속보, 그 외는 소문 → N일 뒤 확률로 팩트화 또는 허위.</description></item>
/// <item><description>거리: 본영~이벤트 성 거리에 따라 속보의 <see cref="WorldNewsItem.isVerifiedFact"/>가 꺼질 수 있음(<see cref="WorldNewsReach"/>, 임시).</description></item>
/// </list>
/// </summary>
public class WorldEventCenter : Singleton<WorldEventCenter>
{
    [Header("데이터 (선택)")]
    [Tooltip("DataManager 이벤트 맵이 비어 있을 때만 사용합니다.")]
    [SerializeField] EventMasterDataSo eventMasterDataSo;
    [Tooltip("이벤트 ID별 소문/속보 문구·리포터 에셋(시트와 병합)")]
    [SerializeField] NewsTemplateSo newsTemplateSo;

    [Header("소문 확정")]
    [Tooltip("사실무근(가짜)로 끝날 확률 — 확정 시각마다 이 구간에서 임계값을 한 번 뽑습니다.")]
    [SerializeField] float fakeRumorChanceMin = 0.10f;
    [SerializeField] float fakeRumorChanceMax = 0.20f;

    [Header("임시: 본영 거리별 속보 ‘확인됨’ 확률")]
    [Tooltip("지도 거리 180 미만")]
    [SerializeField] float newsVerifiedChanceNear = 0.92f;
    [Tooltip("180 이상 420 미만")]
    [SerializeField] float newsVerifiedChanceMid = 0.62f;
    [Tooltip("420 이상")]
    [SerializeField] float newsVerifiedChanceFar = 0.32f;

    /// <summary>동일 성에서 같은 eventId가 다시 뜨기까지 최소 일수(UTC 일 버킷).</summary>
    public const int SameCastleEventCooldownDays = 30;

    /// <summary>레거시 7% 표기. 일일 가중치 기본값은 <see cref="EventPickBaseWeight"/>.</summary>
    public const float DailyEventBaseProbability = 0.07f;

    /// <summary><c>Weight = 이 값 + flatProbBonus + 스탯항</c> (시트 스펙).</summary>
    public const float EventPickBaseWeight = 7f;

    /// <summary><c>Weight / 이 값</c>을 0~1로 클램프해 일일 당첨률로 사용합니다.</summary>
    public const float EventPickWeightToChanceDivisor = 100f;

    readonly List<string> _masterEventIds = new List<string>();
    readonly List<EventMasterData> _eventPickPool = new List<EventMasterData>();

    public IReadOnlyList<string> MasterEventIds => _masterEventIds;

    public NewsTemplateSo NewsTemplates => newsTemplateSo;

    public event Action<CastleStateData, EventMasterData> OnEventSample;

    protected override void Awake()
    {
        base.Awake();
        RebuildFromSources();
    }

    public void RebuildFromSources()
    {
        _masterEventIds.Clear();
        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.eventMasterDataMap != null && dm.eventMasterDataMap.Count > 0)
        {
            foreach (var kv in dm.eventMasterDataMap)
            {
                string key = kv.Key?.Trim();
                if (string.IsNullOrEmpty(key) || kv.Value == null || kv.Value.Count == 0)
                    continue;
                _masterEventIds.Add(key);
            }
        }
        else if (eventMasterDataSo != null && eventMasterDataSo.list != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < eventMasterDataSo.list.Count; i++)
            {
                var e = eventMasterDataSo.list[i];
                if (e == null || string.IsNullOrWhiteSpace(e.id)) continue;
                string id = e.id.Trim();
                if (seen.Add(id))
                    _masterEventIds.Add(id);
            }
        }
    }

    /// <summary>외부 호출 별칭.</summary>
    public void CheckDailyEvents() => CheckDailyEventsFromDataManager();

    /// <summary>자정 틱에서 <see cref="DataManager"/>가 호출합니다.</summary>
    public void CheckDailyEventsFromDataManager()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null)
            return;

        NewsManager.EnsureCreated();
        RebuildFromSources();

        ProcessPendingRumorConfirmations(dm);

        if (_masterEventIds.Count == 0)
            return;

        var nm = NewsManager.InstanceOrNull;
        if (nm == null)
            return;

        int today = (int)(TimeManager.GetUnixNow() / 86400L);

        foreach (var kv in dm.castleStateDataMap)
        {
            var state = kv.Value;
            if (state == null || string.IsNullOrWhiteSpace(state.id))
                continue;

            if (CastleHasPendingRumor(dm, state.id))
                continue;

            _eventPickPool.Clear();
            for (int mi = 0; mi < _masterEventIds.Count; mi++)
            {
                string eid = _masterEventIds[mi];
                if (string.IsNullOrWhiteSpace(eid)) continue;
                if (IsWorldEventOnCooldown(state, eid, today)) continue;
                var winningRow = SelectRandomSatisfiedRow(dm, eid, state, out bool anyRow);
                if (!anyRow || winningRow == null)
                    continue;
                _eventPickPool.Add(winningRow);
            }

            if (_eventPickPool.Count == 0)
                continue;

            ShuffleEventPoolInPlace(_eventPickPool);
            for (int pi = 0; pi < _eventPickPool.Count; pi++)
            {
                var ev = _eventPickPool[pi];
                float weight = CalculateFinalWeight(ev, state, dm);
                float chance = Mathf.Clamp01(weight / EventPickWeightToChanceDivisor);
                if (UnityEngine.Random.value < chance)
                {
                    if (AffinityContainsToken(ev.affinityTagsRaw, "Direct"))
                        TriggerDirectBreakingEvent(dm, nm, state, ev, today);
                    else
                        StartRumorPipeline(dm, nm, state, ev, today);
                    break;
                }
            }

        }
    }

    public static bool IsAllConditionsMet(DataManager dm, EventMasterData ev, CastleStateData castle) =>
        EventConditionEvaluator.IsAllSatisfiedFromLibrary(dm, castle, ev);

    /// <summary>
    /// 동일 eventId의 다중 행(<b>OR</b>) 중, 행 내 <see cref="EventMasterData.conditionIds"/>가 모두 만족하는 행만 모아
    /// 그중 무작위로 한 행을 반환합니다. 만족 행이 없으면 <c>null</c>.
    /// </summary>
    public static EventMasterData SelectRandomSatisfiedRow(DataManager dm, string eventId, CastleStateData castle,
        out bool anyRowSatisfied)
    {
        anyRowSatisfied = false;
        if (dm == null || !dm.TryGetEventMasterRows(eventId, out var rows) || rows == null || rows.Count == 0)
            return null;

        List<EventMasterData> satisfied = null;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r == null) continue;
            if (!EventConditionEvaluator.IsAllSatisfiedFromLibrary(dm, castle, r))
                continue;
            satisfied ??= new List<EventMasterData>();
            satisfied.Add(r);
        }

        if (satisfied == null || satisfied.Count == 0)
            return null;
        anyRowSatisfied = true;
        return satisfied[UnityEngine.Random.Range(0, satisfied.Count)];
    }

    /// <summary><see cref="EventStatModifierData"/> 조회. 없으면 <c>false</c>, out은 default.</summary>
    public static bool TryGetEventStatModifier(DataManager dm, string eventId, out EventStatModifierData mod)
    {
        mod = null;
        if (dm?.eventStatModifierMap == null || string.IsNullOrEmpty(eventId))
            return false;
        return dm.eventStatModifierMap.TryGetValue(eventId.Trim(), out mod) && mod != null;
    }

    /// <summary>스펙 명칭. <c>FinalProb = 7 + flatProbBonus + …</c> (일일/확정 주사위는 <c>/100</c> 클램프).</summary>
    public static float CalculateFinalProb(EventMasterData row, CastleStateData castle, DataManager dm) =>
        CalculateFinalWeight(row, castle, dm);

    /// <summary>
    /// <see cref="EventStatModifierData"/> 기준 가중치. 음수면 0.
    /// Weight = 7 + flatProbBonus + might×perMight + intel×perIntel + charm×perCharm + infamy×perInfamy
    /// </summary>
    public static float CalculateFinalWeight(EventMasterData row, CastleStateData castle, DataManager dm)
    {
        if (row == null)
            return 0f;

        TryGetEventStatModifier(dm, row.id, out var mod);
        float flat = mod != null ? mod.flatProbBonus : 0f;
        float perMight = mod != null ? mod.perMight : 0f;
        float perIntel = mod != null ? mod.perIntel : 0f;
        float perCharm = mod != null ? mod.perCharm : 0f;
        float perInfamy = mod != null ? mod.perInfamy : 0f;

        var gov = GetGovernorGeneral(dm, castle);
        float might = gov != null ? gov.power : 0f;
        float intel = gov != null ? gov.intel : 0f;
        float charm = gov != null ? gov.charm : 0f;
        float infamy = gov != null ? gov.infamy : 0f;

        float w = EventPickBaseWeight + flat + might * perMight + intel * perIntel +
                  charm * perCharm + infamy * perInfamy;
        return Mathf.Max(0f, w);
    }

    /// <summary>디버그·UI용. <see cref="CalculateFinalWeight"/>를 0~1 확률로 환산합니다.</summary>
    public static float CalculateFinalProbability(EventMasterData ev, CastleStateData castle, DataManager dm) =>
        Mathf.Clamp01(CalculateFinalWeight(ev, castle, dm) / EventPickWeightToChanceDivisor);

    /// <summary>콤마·세미콜론 구분 태그에 토큰이 포함되는지(대소문자 무시, 공백 트림).</summary>
    public static bool AffinityContainsToken(string affinityTagsRaw, string token)
    {
        if (string.IsNullOrWhiteSpace(affinityTagsRaw) || string.IsNullOrWhiteSpace(token)) return false;
        var parts = affinityTagsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i].Trim(), token.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static void ShuffleEventPoolInPlace(List<EventMasterData> list)
    {
        if (list == null || list.Count < 2) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    void ProcessPendingRumorConfirmations(DataManager dm)
    {
        if (dm.pendingRumorWorldEvents == null || dm.pendingRumorWorldEvents.Count == 0)
            return;

        var nm = NewsManager.InstanceOrNull;
        if (nm == null) return;

        int today = (int)(TimeManager.GetUnixNow() / 86400L);

        for (int i = dm.pendingRumorWorldEvents.Count - 1; i >= 0; i--)
        {
            var p = dm.pendingRumorWorldEvents[i];
            if (p == null)
            {
                dm.pendingRumorWorldEvents.RemoveAt(i);
                continue;
            }

            if (today < p.confirmOnUtcDay)
                continue;

            string primary = (p.targetCastleId ?? "").Trim();
            var affected = ParseAffectedCastleIds(dm, p);
            if (affected.Count == 0)
            {
                dm.pendingRumorWorldEvents.RemoveAt(i);
                continue;
            }

            bool isFake;
            if (p.confirmUsesEventProbabilityRoll)
            {
                if (!dm.castleStateDataMap.TryGetValue(primary, out var primaryState) || primaryState == null)
                {
                    dm.pendingRumorWorldEvents.RemoveAt(i);
                    continue;
                }

                var rowAtConfirm = SelectRandomSatisfiedRow(dm, p.eventId, primaryState, out bool anySat);
                if (!anySat || rowAtConfirm == null)
                    isFake = true;
                else
                {
                    float finalProb = CalculateFinalProb(rowAtConfirm, primaryState, dm);
                    float pRoll = Mathf.Clamp01(finalProb / EventPickWeightToChanceDivisor);
                    isFake = UnityEngine.Random.value >= pRoll;
                }
            }
            else
            {
                float lo = Mathf.Min(fakeRumorChanceMin, fakeRumorChanceMax);
                float hi = Mathf.Max(fakeRumorChanceMin, fakeRumorChanceMax);
                float fakeThreshold = UnityEngine.Random.Range(lo, hi);
                isFake = UnityEngine.Random.value < fakeThreshold;
            }

            if (!isFake)
            {
                var buffList = p.buffCodesToApply != null && p.buffCodesToApply.Count > 0
                    ? p.buffCodesToApply
                    : dm.GetEventMasterData(p.eventId ?? "")?.buffCodes;
                BuffMasterData ctxBuff = NewsFormatter.TryGetFirstBuffFromCodes(dm, buffList);

                foreach (var cid in affected)
                {
                    if (!dm.castleStateDataMap.TryGetValue(cid, out var st) || st == null) continue;
                    ApplyCastleDelta(st, p.largeSentimentDelta, p.largePopulationDelta);
                    if (buffList != null && buffList.Count > 0)
                        WorldEventBuffApplier.ApplyBuffCodesToCastle(dm, st, buffList, p.eventId ?? "", today);
                }

                string breakCode = "";
                if (!TryResolveNewsFromCodes(dm, p.pendingBreakingNewsCodes, primary, out var head, out var body,
                        out breakCode, ctxBuff))
                    ResolveNewsLegacyTemplates(dm, p.eventId, primary, true, out head, out body, ctxBuff);
                var rumorItem = FindPendingRumorNews(dm, p.linkedNewsUnixTime, p.eventId, primary);
                if (rumorItem != null)
                {
                    UpgradeRumorItemToBreaking(rumorItem, head, body);
                    rumorItem.newsMasterCode = breakCode ?? "";
                    rumorItem.newsIconResourcePath = "";
                    ApplyPlayerNewsReach(dm, rumorItem, primary, WorldNewsFeedKind.Breaking);
                }
                else
                {
                    var w = nm.AddNewsAndReturn(WorldNewsFeedKind.Breaking, p.eventId, primary, head, body, true);
                    if (w != null)
                    {
                        w.newsMasterCode = breakCode ?? "";
                        w.newsIconResourcePath = "";
                    }

                    ApplyPlayerNewsReach(dm, w, primary, WorldNewsFeedKind.Breaking);
                }
            }
            else
            {
                var rumorItem = FindPendingRumorNews(dm, p.linkedNewsUnixTime, p.eventId, primary);
                if (rumorItem != null)
                    UpgradeRumorItemToDebunked(rumorItem, primary, dm);
                else
                {
                    BuildDebunkHeadBody(primary, dm, out var dHead, out var dBody);
                    var w = nm.AddNewsAndReturn(WorldNewsFeedKind.Breaking, p.eventId, primary, dHead, dBody, true);
                    if (w != null)
                    {
                        w.isDebunked = true;
                        w.isVerifiedFact = false;
                        w.isRumorContent = false;
                    }
                }
            }

            dm.pendingRumorWorldEvents.RemoveAt(i);
            dm.MarkCastleStateDirty();
        }
    }

    static List<string> ParseAffectedCastleIds(DataManager dm, PendingRumorWorldEvent p)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.affectedCastleIdsRaw))
        {
            var parts = p.affectedCastleIdsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (!string.IsNullOrEmpty(id)) list.Add(id);
            }
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(p.targetCastleId))
            list.Add(p.targetCastleId.Trim());
        return list;
    }

    static WorldNewsItem FindPendingRumorNews(DataManager dm, long unixTime, string eventId, string primaryCastle)
    {
        if (dm.worldNews == null || unixTime <= 0) return null;
        string eid = eventId ?? "";
        string pc = (primaryCastle ?? "").Trim();
        for (int i = dm.worldNews.Count - 1; i >= 0; i--)
        {
            var w = dm.worldNews[i];
            if (w == null) continue;
            if (w.unixTime != unixTime) continue;
            if (!string.Equals(w.eventId ?? "", eid, StringComparison.Ordinal)) continue;
            if (w.newsKind != (byte)WorldNewsFeedKind.Rumor || w.isConfirmed) continue;
            if (!string.Equals((w.targetCastleId ?? "").Trim(), pc, StringComparison.Ordinal)) continue;
            return w;
        }

        return null;
    }

    static void UpgradeRumorItemToBreaking(WorldNewsItem w, string head, string body)
    {
        string hl = head?.Trim() ?? "";
        string bd = body?.Trim() ?? "";
        w.isConfirmed = true;
        w.newsKind = (byte)WorldNewsFeedKind.Breaking;
        w.headline = hl;
        w.bodyContent = bd;
        w.detailTitle = hl;
        w.detailBody = bd;
        string tag = NewsManager.GetWorldNewsFeedTag(WorldNewsFeedKind.Breaking);
        w.text = string.IsNullOrEmpty(tag) ? hl : $"{tag} {hl}";
        w.debuffIconsHint = tag;
        w.isRumorContent = false;
        w.isVerifiedFact = true;
        w.isDebunked = false;
    }

    static void BuildDebunkHeadBody(string primaryCastleId, DataManager dm, out string head, out string body)
    {
        string cid = (primaryCastleId ?? "").Trim();
        string cn = dm != null ? dm.GetCastleDisplayName(cid) : cid;
        if (string.IsNullOrWhiteSpace(cn)) cn = cid;
        head = $"사실무근 — {cn} 관련 소문";
        body = $"조사 결과, 전해진 소문은 사실이 아닌 것으로 확인되었습니다. {cn} 일대에는 보도와 다른 뚜렷한 정황이 없었습니다.";
    }

    static void UpgradeRumorItemToDebunked(WorldNewsItem w, string primaryCastleId, DataManager dm)
    {
        BuildDebunkHeadBody(primaryCastleId, dm, out var hl, out var bd);
        w.isConfirmed = true;
        w.newsKind = (byte)WorldNewsFeedKind.Breaking;
        w.headline = hl;
        w.bodyContent = bd;
        w.detailTitle = hl;
        w.detailBody = bd;
        string tag = NewsManager.GetWorldNewsFeedTag(WorldNewsFeedKind.Breaking);
        w.text = string.IsNullOrEmpty(tag) ? hl : $"{tag} {hl}";
        w.debuffIconsHint = tag;
        w.isRumorContent = false;
        w.isVerifiedFact = false;
        w.isDebunked = true;
    }

    void StartRumorPipeline(DataManager dm, NewsManager nm, CastleStateData triggerState, EventMasterData ev, int today)
    {
        string triggerCid = triggerState.id.Trim();
        var affected = ResolveAffectedCastles(dm, ev.scope, triggerCid);
        if (affected.Count == 0)
            affected.Add(triggerCid);

        float sSmall = UnityEngine.Random.Range(-2.5f, 2.5f);
        foreach (var cid in affected)
        {
            if (!dm.castleStateDataMap.TryGetValue(cid, out var st) || st == null) continue;
            RecordWorldEventCooldown(st, ev.id, today);
            int popJitter = Mathf.Max(1, st.currentPopulation / 300);
            int pSmall = UnityEngine.Random.Range(-popJitter, popJitter + 1);
            ApplyCastleDelta(st, sSmall, pSmall);
        }

        float sLarge = UnityEngine.Random.Range(-12f, 12f);
        int refPop = triggerState.currentPopulation;
        int popBig = Mathf.Max(5, refPop / 40);
        int pLarge = UnityEngine.Random.Range(-popBig, popBig + 1);

        int minD = Mathf.Max(0, ev.minDays);
        int maxD = Mathf.Max(minD, ev.maxDays);
        int pendingDays = (minD == 0 && maxD == 0)
            ? UnityEngine.Random.Range(3, 6)
            : UnityEngine.Random.Range(minD, maxD + 1);
        int confirmDay = today + pendingDays;

        string relatedRaw = string.Join(",", affected);

        BuffMasterData rumorCtxBuff = NewsFormatter.TryGetFirstBuffForEvent(dm, ev);
        string rumorCode = "";
        if (!TryResolveNewsFromCodes(dm, ev.rumorNewsCodes, triggerCid, out var rh, out var rb, out rumorCode,
                rumorCtxBuff))
            ResolveNewsLegacyTemplates(dm, ev.id, triggerCid, false, out rh, out rb, rumorCtxBuff);
        var item = nm.AddNewsAndReturn(WorldNewsFeedKind.Rumor, ev.id, triggerCid, rh, rb, false);
        if (item != null)
        {
            item.relatedCastleIdsRaw = relatedRaw;
            item.newsMasterCode = rumorCode ?? "";
            item.newsIconResourcePath = "";
            ApplyPlayerNewsReach(dm, item, triggerCid, WorldNewsFeedKind.Rumor);
        }

        if (dm.pendingRumorWorldEvents == null)
            dm.pendingRumorWorldEvents = new List<PendingRumorWorldEvent>();

        var buffToApply = ev.buffCodes != null ? new List<string>(ev.buffCodes) : new List<string>();
        var breakingSnapshot = ev.breakingNewsCodes != null ? new List<string>(ev.breakingNewsCodes) : new List<string>();
        dm.pendingRumorWorldEvents.Add(new PendingRumorWorldEvent
        {
            eventId = ev.id,
            targetCastleId = triggerCid,
            confirmOnUtcDay = confirmDay,
            linkedNewsUnixTime = item != null ? item.unixTime : 0,
            affectedCastleIdsRaw = relatedRaw,
            largeSentimentDelta = sLarge,
            largePopulationDelta = pLarge,
            pendingBreakingNewsCodes = breakingSnapshot,
            buffCodesToApply = buffToApply,
            confirmUsesEventProbabilityRoll = AffinityContainsToken(ev.affinityTagsRaw, "Rumor")
        });

        OnEventSample?.Invoke(triggerState, ev);
        dm.MarkCastleStateDirty();
    }

    static List<string> ResolveAffectedCastles(DataManager dm, EventScope scope, string triggerCastleId)
    {
        var list = new List<string>();
        if (dm == null || string.IsNullOrWhiteSpace(triggerCastleId)) return list;
        triggerCastleId = triggerCastleId.Trim();
        if (scope == EventScope.Castle)
        {
            list.Add(triggerCastleId);
            return list;
        }

        if (!dm.castleMasterDataMap.TryGetValue(triggerCastleId, out var m) || m == null)
        {
            list.Add(triggerCastleId);
            return list;
        }

        string rid = (m.regionId ?? "").Trim();
        foreach (var kv in dm.castleMasterDataMap)
        {
            if (kv.Value == null) continue;
            if (string.Equals((kv.Value.regionId ?? "").Trim(), rid, StringComparison.Ordinal))
                list.Add(kv.Key.Trim());
        }

        if (list.Count == 0)
            list.Add(triggerCastleId);
        return list;
    }

    /// <summary>
    /// 맵에 존재하는 코드만 모아 무작위 1개로 <see cref="NewsMasterData"/>를 적용합니다. 없으면 false(레거시 템플릿).
    /// </summary>
    public static bool TryResolveNewsFromCodes(DataManager dm, IList<string> codes, string castleId,
        out string headline, out string body, out string pickedCode,
        BuffMasterData buffForPlaceholders = null)
    {
        headline = body = pickedCode = "";
        if (dm == null || codes == null || codes.Count == 0) return false;

        var valid = new List<string>();
        for (int i = 0; i < codes.Count; i++)
        {
            string c = codes[i];
            if (string.IsNullOrWhiteSpace(c)) continue;
            string t = c.Trim();
            if (dm.TryGetNewsMasterData(t, out var row) && row != null)
                valid.Add(t);
        }

        if (valid.Count == 0) return false;
        pickedCode = valid[UnityEngine.Random.Range(0, valid.Count)];
        if (!dm.TryGetNewsMasterData(pickedCode, out var newsRow) || newsRow == null) return false;

        headline = NewsFormatter.FormatNews(newsRow.headline ?? "", castleId, dm, buffForPlaceholders);
        body = NewsFormatter.FormatNews(newsRow.script ?? "", castleId, dm, buffForPlaceholders);
        if (string.IsNullOrWhiteSpace(body))
            body = headline;
        return true;
    }

    static string PickTemplateLine(string preferred, string globalSheet, string soEntry)
    {
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred.Trim();
        if (!string.IsNullOrWhiteSpace(globalSheet)) return globalSheet.Trim();
        return soEntry?.Trim() ?? "";
    }

    /// <summary>뉴스 코드 매핑 실패 시 <see cref="NewsTemplateSo"/>·런타임 시트 맵·기본 문구.</summary>
    void ResolveNewsLegacyTemplates(DataManager dm, string eventId, string castleId, bool breaking, out string head,
        out string body, BuffMasterData buffForPlaceholders = null)
    {
        if (dm == null) dm = DataManager.InstanceOrNull;
        NewsTemplateSheetRow gSheet = null;
        dm?.TryGetNewsTemplateSheetRow(eventId, out gSheet);
        NewsTemplateEntry soOnly = null;
        newsTemplateSo?.TryGetSoEntryOnly(eventId, out soOnly);

        string rawH = breaking
            ? PickTemplateLine(null, gSheet?.breakingHeadline, soOnly?.breakingHeadline)
            : PickTemplateLine(null, gSheet?.rumorHeadline, soOnly?.rumorHeadline);
        string rawB = breaking
            ? PickTemplateLine(null, gSheet?.breakingBody, soOnly?.breakingBody)
            : PickTemplateLine(null, gSheet?.rumorBody, soOnly?.rumorBody);
        if (string.IsNullOrWhiteSpace(rawB))
            rawB = PickTemplateLine(null, gSheet?.reporterScript, soOnly?.reporterScript);

        EventMasterData em = dm?.GetEventMasterData(eventId);
        string evLabel = em != null && !string.IsNullOrWhiteSpace(em.name) ? em.name.Trim() : (eventId ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(rawH) || !string.IsNullOrWhiteSpace(rawB))
        {
            head = NewsFormatter.FormatNews(rawH, castleId, dm, buffForPlaceholders);
            body = NewsFormatter.FormatNews(rawB, castleId, dm, buffForPlaceholders);
            if (string.IsNullOrWhiteSpace(head))
                head = breaking
                    ? $"{NewsFormatter.FormatNews("{Castle}", castleId, dm, buffForPlaceholders)} 관련 속보"
                    : $"{NewsFormatter.FormatNews("{Castle}", castleId, dm, buffForPlaceholders)}에 대한 소문";
            if (string.IsNullOrWhiteSpace(body))
                body = head;
            return;
        }

        string cn = dm != null ? dm.GetCastleDisplayName(castleId) : castleId;
        if (string.IsNullOrWhiteSpace(cn)) cn = castleId;
        if (breaking)
        {
            head = $"{evLabel} — {cn} 확인";
            body = $"{cn}({castleId})에서 {evLabel} 관련 정황이 공식 확인되었습니다.";
        }
        else
        {
            head = $"{cn} 풍문: {evLabel}";
            body = $"{cn}을 둘러싼 {evLabel} 소문이 무성합니다. 진위는 시간이 지나야 알 수 있을 전망입니다.";
        }
    }

    /// <summary>[시스템 1: Direct] <see cref="EventMasterData.breakingNewsCodes"/>에서 즉시 1개 추첨 → 속보·버프·쿨다운.</summary>
    public void TriggerDirectBreakingEvent(DataManager dm, NewsManager nm, CastleStateData castle, EventMasterData ev,
        int todayUtcDay)
    {
        if (dm == null || nm == null || castle == null || ev == null) return;

        string triggerCid = castle.id.Trim();
        var affected = ResolveAffectedCastles(dm, ev.scope, triggerCid);
        if (affected.Count == 0)
            affected.Add(triggerCid);

        foreach (var cid in affected)
        {
            if (dm.castleStateDataMap.TryGetValue(cid, out var st) && st != null)
                RecordWorldEventCooldown(st, ev.id, todayUtcDay);
        }

        BuffMasterData directCtxBuff = NewsFormatter.TryGetFirstBuffForEvent(dm, ev);
        string code = "";
        if (!TryResolveNewsFromCodes(dm, ev.breakingNewsCodes, triggerCid, out var head, out var body, out code,
                directCtxBuff))
            ResolveNewsLegacyTemplates(dm, ev.id, triggerCid, true, out head, out body, directCtxBuff);

        var item = nm.AddNewsAndReturn(WorldNewsFeedKind.Breaking, ev.id, triggerCid, head, body, true);
        if (item != null)
        {
            item.relatedCastleIdsRaw = string.Join(",", affected);
            item.newsMasterCode = code ?? "";
            item.newsIconResourcePath = "";
            ApplyPlayerNewsReach(dm, item, triggerCid, WorldNewsFeedKind.Breaking);
        }

        var buff = ev.buffCodes;
        foreach (var aid in affected)
        {
            if (!dm.castleStateDataMap.TryGetValue(aid, out var st) || st == null) continue;
            if (buff == null || buff.Count == 0) continue;
            WorldEventBuffApplier.ApplyBuffCodesToCastle(dm, st, buff, ev.id, todayUtcDay);
        }

        OnEventSample?.Invoke(castle, ev);
        dm.MarkCastleStateDirty();
    }

    static void ApplyCastleDelta(CastleStateData s, float dSentiment, int dPopulation)
    {
        if (s == null) return;
        s.currentSentiment = Mathf.Clamp(s.currentSentiment + dSentiment, 0f, 200f);
        s.currentPopulation = Mathf.Max(1, s.currentPopulation + dPopulation);
    }

    static bool CastleHasPendingRumor(DataManager dm, string castleId)
    {
        if (dm?.pendingRumorWorldEvents == null) return false;
        string cid = (castleId ?? "").Trim();
        if (string.IsNullOrEmpty(cid)) return false;
        for (int i = 0; i < dm.pendingRumorWorldEvents.Count; i++)
        {
            var p = dm.pendingRumorWorldEvents[i];
            if (p == null) continue;
            var ids = ParseAffectedCastleIds(dm, p);
            for (int j = 0; j < ids.Count; j++)
            {
                if (string.Equals((ids[j] ?? "").Trim(), cid, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    static bool IsWorldEventOnCooldown(CastleStateData state, string eventId, int todayUtcDay)
    {
        if (state?.worldEventCooldowns == null) return false;
        string eid = (eventId ?? "").Trim();
        if (string.IsNullOrEmpty(eid)) return false;
        for (int i = 0; i < state.worldEventCooldowns.Count; i++)
        {
            var c = state.worldEventCooldowns[i];
            if (c == null) continue;
            if (!string.Equals((c.eventId ?? "").Trim(), eid, StringComparison.Ordinal)) continue;
            return todayUtcDay - c.lastOccurredUtcDay < SameCastleEventCooldownDays;
        }

        return false;
    }

    static void RecordWorldEventCooldown(CastleStateData state, string eventId, int todayUtcDay)
    {
        if (state == null) return;
        string eid = (eventId ?? "").Trim();
        if (string.IsNullOrEmpty(eid)) return;
        if (state.worldEventCooldowns == null)
            state.worldEventCooldowns = new List<WorldEventCooldownEntry>();
        for (int i = 0; i < state.worldEventCooldowns.Count; i++)
        {
            var c = state.worldEventCooldowns[i];
            if (c == null) continue;
            if (!string.Equals((c.eventId ?? "").Trim(), eid, StringComparison.Ordinal)) continue;
            c.lastOccurredUtcDay = todayUtcDay;
            return;
        }

        state.worldEventCooldowns.Add(new WorldEventCooldownEntry
        {
            eventId = eid,
            lastOccurredUtcDay = todayUtcDay
        });
    }

    static GeneralMasterData GetGovernorGeneral(DataManager dm, CastleStateData state)
    {
        if (dm == null || state == null || string.IsNullOrWhiteSpace(state.currentGovernorId))
            return null;
        return dm.GetGeneralMasterData(state.currentGovernorId);
    }

    /// <summary>본영 거리 기반 임시 도달·확실도. 소문은 부가 문구, 속보는 <see cref="WorldNewsItem.isVerifiedFact"/> 보정.</summary>
    void ApplyPlayerNewsReach(DataManager dm, WorldNewsItem item, string primaryCastleId, WorldNewsFeedKind kind)
    {
        if (item == null || dm == null) return;
        string pc = (primaryCastleId ?? "").Trim();
        if (kind == WorldNewsFeedKind.Rumor)
        {
            WorldNewsReach.ApplyDistanceTagToRumor(dm, item, pc);
            return;
        }

        if (kind == WorldNewsFeedKind.Breaking)
        {
            WorldNewsReach.ApplyDistanceIntelToBreakingNews(dm, item, pc, newsVerifiedChanceNear, newsVerifiedChanceMid,
                newsVerifiedChanceFar);
        }
    }

    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(WorldEventCenter));
        go.AddComponent<WorldEventCenter>();
    }
}
