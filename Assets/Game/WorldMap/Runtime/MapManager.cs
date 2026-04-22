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

    WorldMapAutopilotSimulator _autopilot;

    Castle _selected;
    readonly Dictionary<string, Castle> _castleByMasterId = new Dictionary<string, Castle>(StringComparer.OrdinalIgnoreCase);
    Transform _marchesRoot;

    public Transform CastleParentOrSelf => castleParent != null ? castleParent : transform;

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

        if (WorldTimeManager.InstanceOrNull != null)
            WorldTimeManager.InstanceOrNull.OnNewDayTick += OnWorldSimulationDay;

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateTicked += RefreshAllCastleMapStatuses;
            dm.OnStateDataReady += RefreshAllCastleMapStatuses;
        }

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
            dm.OnStateTicked -= RefreshAllCastleMapStatuses;
            dm.OnStateDataReady -= RefreshAllCastleMapStatuses;
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

        if (!AreAdjacentByMasterId(from, to))
        {
            Debug.LogWarning("[MapManager] 인접하지 않은 성으로는 출정할 수 없습니다.");
            return;
        }

        if (from.CountryId == to.CountryId)
        {
            Debug.LogWarning("[MapManager] 동일 세력 성으로는 출정(공격)할 수 없습니다.");
            return;
        }

        if (WorldMapWarManager.InstanceOrNull != null &&
            (WorldMapWarManager.InstanceOrNull.IsCastleInAnyWar(from) ||
             WorldMapWarManager.InstanceOrNull.IsCastleInAnyWar(to)))
        {
            Debug.LogWarning("[MapManager] 전쟁(공성) 중인 성은 전쟁이 끝날 때까지 공격할 수 없습니다.");
            return;
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
        marker.Begin(from, to, from.GovernorName, troops, marchWorldUnitsPerSecond);

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
}
