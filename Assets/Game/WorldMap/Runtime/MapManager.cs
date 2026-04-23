using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>성 프리팹 생성 및 선택/상세 UI 연동.</summary>
public class MapManager : WorldMapSingleton<MapManager>
{
    [Tooltip("켜면 아래 CastleData만 사용합니다. 끄면 시트 동기화 SO(DataManager) → CastleMaster SO 순으로 시도합니다.")]
    [SerializeField] bool useCastleDataOverride;
    [SerializeField] CastleData castleDataSet;
    [SerializeField] CastleMasterDataSo castleMasterSo;
    [SerializeField] GeneralMasterDataSo generalMasterSo;
    [SerializeField] Castle castlePrefab;
    [SerializeField] Transform castleParent;
    [SerializeField] CountryColorProvider countryColorProvider;
    [SerializeField] CastleDetailPanel detailPanel;
    [SerializeField] TMP_Text dayHudText;

    [Header("출정 행군")]
    [Tooltip("도로(월드 거리)당 초 이동 속도. 거리가 길수록 도착까지 더 오래 걸립니다.")]
    [SerializeField] float marchWorldUnitsPerSecond = 2.1f;
    [Tooltip("출정 시 보내는 병력 비율(0~1). 최소 병력은 별도로 클램프합니다.")]
    [SerializeField] float marchArmyFraction = 0.28f;
    [SerializeField] int marchArmyMinimum = 400;

    [Header("도로·출정 연결")]
    [Tooltip(
        "한쪽(또는 양쪽) 성의 인접 시트가 비어 있거나 BFS로 못 찾을 때, 이 월드 거리 이내면 출정 허용(목 맵·데이터 누락 보정). 실데이터 양쪽 모두 인접이 채워진 경우에는 BFS만 사용합니다.")]
    [SerializeField] float roadlessTacticalNeighborDistance = 9f;

    WorldMapAutopilotSimulator _autopilot;

    Castle _selected;
    readonly Dictionary<string, Castle> _castleByMasterId = new Dictionary<string, Castle>(StringComparer.OrdinalIgnoreCase);
    Transform _marchesRoot;

    public Transform CastleParentOrSelf => castleParent != null ? castleParent : transform;

    public CountryColorProvider CountryColorsOrNull => countryColorProvider;

    void Start()
    {
        if (countryColorProvider == null)
            countryColorProvider = UnityEngine.Object.FindFirstObjectByType<CountryColorProvider>();

        _autopilot = GetComponent<WorldMapAutopilotSimulator>();
        if (_autopilot == null)
            _autopilot = gameObject.AddComponent<WorldMapAutopilotSimulator>();

        if (GetComponent<WorldMapWarManager>() == null)
            gameObject.AddComponent<WorldMapWarManager>();

        var rows = ResolveSpawnRows();
        if (rows == null || rows.Length == 0)
        {
            Debug.LogError("[MapManager] 표시할 성 데이터가 없습니다.");
            return;
        }

        SpawnCastles(rows);
        RebuildCastleMasterLookup();
        RebuildRoads();
        EnsureMarchesRoot();

        WorldMapGeneralRoster.RebuildFromDataManager(DataManager.InstanceOrNull);

        if (WorldTimeManager.InstanceOrNull != null)
            WorldTimeManager.InstanceOrNull.OnNewDayTick += OnWorldSimulationDay;

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateTicked += OnDataManagerStateForWorldMap;
            dm.OnStateDataReady += OnDataManagerStateForWorldMap;
        }

        RefreshAllCastleMapStatuses();
    }

    void OnDataManagerStateForWorldMap()
    {
        WorldMapGeneralRoster.RebuildFromDataManager(DataManager.InstanceOrNull);
        RefreshAllCastleMapStatuses();
    }

    public void NotifySiegeEndedForUi()
    {
        if (detailPanel != null && _selected != null)
            detailPanel.RefreshFromBound();
    }

    void OnDestroy()
    {
        if (WorldTimeManager.InstanceOrNull != null)
            WorldTimeManager.InstanceOrNull.OnNewDayTick -= OnWorldSimulationDay;

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateTicked -= OnDataManagerStateForWorldMap;
            dm.OnStateDataReady -= OnDataManagerStateForWorldMap;
        }
    }

    public void RefreshAllCastleMapStatuses()
    {
        Transform parent = castleParent != null ? castleParent : transform;
        if (parent == null) return;
        var huds = parent.GetComponentsInChildren<CastleWorldHud>(true);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null)
                huds[i].Refresh();
        }
    }

    void OnWorldSimulationDay(int day)
    {
        _autopilot?.Run(day);

        if (dayHudText != null)
            dayHudText.text = $"시뮬레이션 Day {day}";
        if (detailPanel != null && _selected != null)
            detailPanel.RefreshFromBound();
        RefreshAllCastleMapStatuses();
    }

    CastleSheetRow[] ResolveSpawnRows()
    {
        if (useCastleDataOverride && castleDataSet != null && castleDataSet.rows != null && castleDataSet.rows.Length > 0)
            return castleDataSet.rows;

        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.castleMasterDataMap != null && dm.castleMasterDataMap.Count > 0)
        {
            var fromDm = WorldMapRowFactory.FromCastleMasterDictionary(dm.castleMasterDataMap, dm.generalMasterDataMap);
            if (fromDm != null && fromDm.Length > 0)
            {
                Debug.Log($"[MapManager] DataManager 구글 시트 동기화 맵에서 성 {fromDm.Length}개 로드.");
                return fromDm;
            }
        }

        if (castleMasterSo != null && castleMasterSo.list != null && castleMasterSo.list.Count > 0)
        {
            var fromSo = WorldMapRowFactory.FromCastleMasterSo(castleMasterSo, generalMasterSo);
            if (fromSo != null && fromSo.Length > 0)
            {
                Debug.Log($"[MapManager] CastleMasterDataSo(시트와 동일 소스)에서 성 {fromSo.Length}개 로드.");
                return fromSo;
            }
        }

        if (castleDataSet != null && castleDataSet.rows != null && castleDataSet.rows.Length > 0)
            return castleDataSet.rows;

        Debug.LogWarning("[MapManager] 목(Mock) Castle 데이터를 사용합니다. 인스펙터에 CastleMasterDataSo를 넣거나 시트를 받아오세요.");
        return MockCastleDataProvider.BuildDefaultRows();
    }

    void SpawnCastles(CastleSheetRow[] rows)
    {
        if (castlePrefab == null) return;
        Transform parent = castleParent != null ? castleParent : transform;
        foreach (var row in rows)
        {
            if (row == null) continue;
            var inst = Instantiate(castlePrefab, parent);
            inst.Initialize(row, countryColorProvider);
        }
    }

    void RebuildRoads()
    {
        Transform parent = castleParent != null ? castleParent : transform;
        if (parent == null) return;
        var net = parent.GetComponent<CastleRoadNetwork>();
        if (net == null)
            net = parent.gameObject.AddComponent<CastleRoadNetwork>();
        net.RebuildFromCastles(parent, countryColorProvider);
    }

    void RebuildCastleMasterLookup()
    {
        _castleByMasterId.Clear();
        Transform parent = castleParent != null ? castleParent : transform;
        if (parent == null) return;
        var arr = parent.GetComponentsInChildren<Castle>(true);
        for (int i = 0; i < arr.Length; i++)
        {
            var c = arr[i];
            if (c == null) continue;
            var mid = c.MasterId;
            if (string.IsNullOrEmpty(mid)) continue;
            _castleByMasterId[mid] = c;
        }
    }

    void EnsureMarchesRoot()
    {
        if (_marchesRoot != null) return;
        var p = CastleParentOrSelf;
        if (p == null) return;
        var existing = p.Find("Marches");
        if (existing != null)
        {
            _marchesRoot = existing;
            return;
        }

        var go = new GameObject("Marches");
        go.transform.SetParent(p, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _marchesRoot = go.transform;
    }

    public bool TryGetCastleByMasterId(string masterId, out Castle castle)
    {
        castle = null;
        if (string.IsNullOrWhiteSpace(masterId)) return false;
        return _castleByMasterId.TryGetValue(masterId.Trim(), out castle) && castle != null;
    }

    /// <summary>인접 목록 중 같은 세력 성만 반환합니다.</summary>
    public List<Castle> GetAdjacentAlliedCastles(Castle from)
    {
        var list = new List<Castle>(8);
        if (from == null || string.IsNullOrEmpty(from.MasterId))
            return list;

        foreach (var nid in SplitAdjacentIdsRaw(from.AdjacentIdsRaw))
        {
            if (!TryGetCastleByMasterId(nid, out var to) || to == null) continue;
            if (to.CountryId != from.CountryId) continue;
            list.Add(to);
        }

        return list;
    }

    /// <summary>인접 목록 중 다른 국가(적) 성만 반환합니다.</summary>
    public List<Castle> GetAdjacentEnemyCastles(Castle from)
    {
        var list = new List<Castle>(8);
        if (from == null || string.IsNullOrEmpty(from.MasterId))
            return list;

        foreach (var nid in SplitAdjacentIdsRaw(from.AdjacentIdsRaw))
        {
            if (!TryGetCastleByMasterId(nid, out var to) || to == null) continue;
            if (to.CountryId == from.CountryId) continue;
            list.Add(to);
        }

        return list;
    }

    static IEnumerable<string> SplitAdjacentIdsRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var s = parts[i].Trim();
            if (!string.IsNullOrEmpty(s)) yield return s;
        }
    }

    public bool AreAdjacentByMasterId(Castle a, Castle b)
    {
        if (a == null || b == null || string.IsNullOrEmpty(a.MasterId) || string.IsNullOrEmpty(b.MasterId))
            return false;
        var target = b.MasterId.Trim();
        foreach (var nid in SplitAdjacentIdsRaw(a.AdjacentIdsRaw))
        {
            if (string.Equals(nid, target, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>인접 도로 그래프상 출발 성에서 목표 성까지 도달 가능하면 true.</summary>
    public bool AreConnectedByRoads(Castle from, Castle to)
    {
        if (from == null || to == null || string.IsNullOrEmpty(from.MasterId) || string.IsNullOrEmpty(to.MasterId))
            return false;
        var start = from.MasterId.Trim();
        var goal = to.MasterId.Trim();
        if (string.Equals(start, goal, StringComparison.OrdinalIgnoreCase))
            return false;

        if (AreAdjacentByMasterId(from, to) || AreAdjacentByMasterId(to, from))
            return true;

        bool fromHasGraph = !string.IsNullOrWhiteSpace(from.AdjacentIdsRaw);
        bool toHasGraph = !string.IsNullOrWhiteSpace(to.AdjacentIdsRaw);

        if (fromHasGraph && toHasGraph && TryReachByBfsRoads(start, goal))
            return true;

        if (fromHasGraph && toHasGraph)
            return false;

        return TacticalWorldDistanceAllowsMarch(from, to);
    }

    bool TryReachByBfsRoads(string start, string goal)
    {
        var q = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        q.Enqueue(start);
        seen.Add(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (string.Equals(cur, goal, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!TryGetCastleByMasterId(cur, out var c) || c == null) continue;
            foreach (var nid in SplitAdjacentIdsRaw(c.AdjacentIdsRaw))
            {
                if (string.IsNullOrEmpty(nid) || seen.Contains(nid)) continue;
                seen.Add(nid);
                q.Enqueue(nid);
            }
        }

        return false;
    }

    bool TacticalWorldDistanceAllowsMarch(Castle from, Castle to)
    {
        float d = Vector2.Distance((Vector2)from.transform.position, (Vector2)to.transform.position);
        return d <= Mathf.Max(0.1f, roadlessTacticalNeighborDistance);
    }

    /// <summary>같은 세력 성만 도로를 따라 만나는 성들(자기 자신 제외). 전장 지원 대상 탐색에 사용.</summary>
    public List<Castle> GetAlliesConnectedByRoadsExceptSelf(Castle origin)
    {
        var list = new List<Castle>(32);
        if (origin == null || string.IsNullOrEmpty(origin.MasterId))
            return list;

        var faction = origin.CountryId;
        var start = origin.MasterId.Trim();
        var q = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        q.Enqueue(start);
        seen.Add(start);

        while (q.Count > 0)
        {
            var curId = q.Dequeue();
            if (!TryGetCastleByMasterId(curId, out var c) || c == null) continue;
            foreach (var nid in SplitAdjacentIdsRaw(c.AdjacentIdsRaw))
            {
                if (string.IsNullOrEmpty(nid) || seen.Contains(nid)) continue;
                if (!TryGetCastleByMasterId(nid, out var n) || n == null) continue;
                if (n.CountryId != faction) continue;
                seen.Add(nid);
                q.Enqueue(nid);
                if (!string.Equals(nid, start, StringComparison.OrdinalIgnoreCase))
                    list.Add(n);
            }
        }

        return list;
    }

    /// <summary>출발 성에서 목표 성으로 행군 마커를 보냅니다. 도착 시 목표에 전쟁중이 표시됩니다.</summary>
    public void StartMarch(Castle from, Castle to)
    {
        if (from == null || to == null)
            return;
        if (string.IsNullOrEmpty(from.MasterId))
        {
            Debug.LogWarning("[MapManager] 마스터 ID가 없는 성에서는 출정할 수 없습니다.");
            return;
        }

        if (!AreConnectedByRoads(from, to))
        {
            Debug.LogWarning("[MapManager] 도로로 연결되지 않은 성으로는 출정할 수 없습니다.");
            return;
        }

        if (from.CountryId == to.CountryId)
        {
            Debug.LogWarning("[MapManager] 동일 세력 성으로는 출정(공격)할 수 없습니다.");
            return;
        }

        var wm = WorldMapWarManager.InstanceOrNull;
        if (wm != null)
        {
            if (wm.IsCastleSiegeDefender(from))
            {
                Debug.LogWarning("[MapManager] 수비 공성 중인 성에서는 출정할 수 없습니다.");
                return;
            }

            if (wm.CountActiveSiegesWhereAttacker(from) >= wm.MaxConcurrentAttacksPerCastle)
            {
                Debug.LogWarning(
                    $"[MapManager] 이 성에서 동시에 벌일 수 있는 공격이 상한({wm.MaxConcurrentAttacksPerCastle})에 도달했습니다.");
                return;
            }

            if (wm.IsCastleInAnyWar(to))
            {
                Debug.LogWarning("[MapManager] 전쟁(공성) 중인 성은 전쟁이 끝날 때까지 공격할 수 없습니다.");
                return;
            }
        }

        EnsureMarchesRoot();
        if (_marchesRoot == null) return;

        int maxSend = Mathf.Max(1, from.Army);
        int want = Mathf.RoundToInt(from.Army * Mathf.Clamp01(marchArmyFraction));
        int troops = Mathf.Clamp(Mathf.Max(marchArmyMinimum, want), 1, maxSend);
        from.AddArmy(-troops);

        var go = new GameObject($"March_{from.MasterId}_to_{to.MasterId}");
        go.transform.SetParent(_marchesRoot, false);
        var marker = go.AddComponent<MarchingTroopMarker>();
        string leadGen = WorldMapGeneralRoster.ResolveMarchingGovernorId(from);
        WorldMapGovernorSuccession.TryHandOverWhenGovernorMarches(leadGen, from, countryColorProvider);
        marker.Begin(from, to, from.GovernorName, troops, marchWorldUnitsPerSecond, leadGen);
        WorldMapGeneralRoster.BeginMarch(leadGen, from, to, marker);

        if (detailPanel != null && _selected == from)
            detailPanel.RefreshFromBound();

        Debug.Log(
            $"[MapManager] 출정: {from.DisplayCastleName} → {to.DisplayCastleName}, 병력 {troops:N0}, 장수 {from.GovernorName}");
    }

    public void SelectCastle(Castle castle)
    {
        _selected = castle;
        if (detailPanel != null)
            detailPanel.Bind(castle);
    }

    static int ComputeSupportTroopPlan(Castle supporter)
    {
        if (supporter == null) return 0;
        int n = Mathf.Max(100, Mathf.RoundToInt(supporter.Army * 0.16f));
        return Mathf.Min(n, supporter.Army);
    }

    /// <summary>도로로 연결된 아군 성이 <b>수비</b> 공성 중이면 그 성과 지원 병력(예정)을 반환합니다.</summary>
    public bool TryGetSiegeDefenseSupportOpportunity(Castle supporter, out Castle besiegedAlly, out int plannedTroops)
    {
        besiegedAlly = null;
        plannedTroops = 0;
        if (supporter == null) return false;
        var wm = WorldMapWarManager.InstanceOrNull;
        if (wm == null) return false;

        foreach (var n in GetAlliesConnectedByRoadsExceptSelf(supporter))
        {
            if (n == null) continue;
            if (!wm.TryFindWarDefendingCastle(n, out _)) continue;
            besiegedAlly = n;
            plannedTroops = ComputeSupportTroopPlan(supporter);
            return plannedTroops > 0;
        }

        return false;
    }

    /// <summary>도로로 연결된 아군 성이 <b>공격(공성)</b> 중이면 그 성과 지원 병력(예정)을 반환합니다.</summary>
    public bool TryGetSiegeAttackSupportOpportunity(Castle supporter, out Castle attackerAlly, out int plannedTroops)
    {
        attackerAlly = null;
        plannedTroops = 0;
        if (supporter == null) return false;
        var wm = WorldMapWarManager.InstanceOrNull;
        if (wm == null) return false;

        foreach (var n in GetAlliesConnectedByRoadsExceptSelf(supporter))
        {
            if (n == null) continue;
            if (!wm.TryFindWarAttackingCastle(n, out _)) continue;
            attackerAlly = n;
            plannedTroops = ComputeSupportTroopPlan(supporter);
            return plannedTroops > 0;
        }

        return false;
    }

    /// <summary>인접 아군 수비 공성에 병력을 보냅니다.</summary>
    public bool TrySendSiegeDefenseSupport(Castle supporter, Castle besiegedAlly)
    {
        if (supporter == null || besiegedAlly == null) return false;
        if (!TryGetSiegeDefenseSupportOpportunity(supporter, out var target, out var troops)) return false;
        if (target != besiegedAlly) return false;
        var wm = WorldMapWarManager.InstanceOrNull;
        return wm != null && wm.TrySendNeighborReinforcement(supporter, besiegedAlly, troops);
    }

    /// <summary>인접 아군 공격 공성에 병력을 보냅니다.</summary>
    public bool TrySendSiegeAttackSupport(Castle supporter, Castle attackerAlly)
    {
        if (supporter == null || attackerAlly == null) return false;
        if (!TryGetSiegeAttackSupportOpportunity(supporter, out var target, out var troops)) return false;
        if (target != attackerAlly) return false;
        var wm = WorldMapWarManager.InstanceOrNull;
        return wm != null && wm.TrySendNeighborAttackReinforcement(supporter, attackerAlly, troops);
    }

    public void RefreshCastleDetailIfOpen()
    {
        if (detailPanel != null && _selected != null)
            detailPanel.RefreshFromBound();
    }
}
