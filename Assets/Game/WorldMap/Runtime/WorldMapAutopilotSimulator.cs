using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드맵 전용 자동 시뮬: 인접 타세력 교전, 재난, 호재 등. <see cref="DataManager"/>가 없어도 동작합니다.
/// </summary>
public class WorldMapAutopilotSimulator : MonoBehaviour
{
    [Header("확률 (0~1)")]
    [Tooltip("인접한 적 성 쌍마다, 병력이 더 많은 쪽이 상대를 향해 AI 출정(행군 마커)할 확률입니다.")]
    [SerializeField] float aiBorderMarchChance = 0.11f;
    [SerializeField] float globalDisasterChance = 0.38f;
    [SerializeField] float globalFavorableChance = 0.22f;
    [SerializeField] float postMarchRumorChance = 0.32f;

    [Header("지속 일수")]
    [SerializeField] int disasterFlagDays = 2;
    [SerializeField] int favorableFlagDays = 2;
    [SerializeField] int rumorFlagDays = 1;

    List<Castle> _castles = new List<Castle>(128);
    readonly Dictionary<string, Castle> _byMaster = new Dictionary<string, Castle>(StringComparer.OrdinalIgnoreCase);

    public void Run(int simulatedDay)
    {
        RebuildCastleCache();
        if (_castles.Count == 0) return;

        foreach (var c in _castles)
            c?.TickSimulationCounters();

        RunAiBorderMarches(simulatedDay);
        RunGlobalDisaster(simulatedDay);
        RunGlobalFavorable(simulatedDay);
    }

    void RebuildCastleCache()
    {
        _castles.Clear();
        _byMaster.Clear();
        var root = MapManager.InstanceOrNull != null
            ? MapManager.InstanceOrNull.CastleParentOrSelf
            : null;
        if (root == null) return;
        var arr = root.GetComponentsInChildren<Castle>(true);
        for (int i = 0; i < arr.Length; i++)
        {
            var c = arr[i];
            if (c == null) continue;
            _castles.Add(c);
            if (!string.IsNullOrEmpty(c.MasterId))
                _byMaster[c.MasterId] = c;
        }
    }

    /// <summary>인접 적성에 대해 병력이 우세한 쪽이 <see cref="MapManager.StartMarch"/>로 출정합니다.</summary>
    void RunAiBorderMarches(int day)
    {
        var map = MapManager.InstanceOrNull;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _castles.Count; i++)
        {
            var a = _castles[i];
            if (a == null || string.IsNullOrEmpty(a.MasterId))
                continue;

            if (!string.IsNullOrWhiteSpace(a.AdjacentIdsRaw))
            {
                ForEachAdjacentId(a.AdjacentIdsRaw, raw =>
                {
                    if (!_byMaster.TryGetValue(raw, out var b) || b == null) return;
                    TryAiMarchPair(day, map, seen, a, b);
                });
            }
            else if (map != null)
            {
                for (int j = 0; j < _castles.Count; j++)
                {
                    var b = _castles[j];
                    if (b == null || b == a || string.IsNullOrEmpty(b.MasterId)) continue;
                    if (a.CountryId == b.CountryId) continue;
                    if (!map.AreConnectedByRoads(a, b)) continue;
                    TryAiMarchPair(day, map, seen, a, b);
                }
            }
        }
    }

    void TryAiMarchPair(int day, MapManager map, HashSet<string> seen, Castle a, Castle b)
    {
        if (a == null || b == null || a.CountryId == b.CountryId) return;

        string k = PairKey(a.MasterId, b.MasterId);
        if (!seen.Add(k)) return;
        if (UnityEngine.Random.value > aiBorderMarchChance) return;

        Castle attacker;
        Castle defender;
        if (a.Army > b.Army)
        {
            attacker = a;
            defender = b;
        }
        else if (b.Army > a.Army)
        {
            attacker = b;
            defender = a;
        }
        else
        {
            if (UnityEngine.Random.value < 0.5f)
            {
                attacker = a;
                defender = b;
            }
            else
            {
                attacker = b;
                defender = a;
            }
        }

        if (attacker.Army < 1) return;
        if (map == null) return;

        map.StartMarch(attacker, defender);

        if (UnityEngine.Random.value < postMarchRumorChance)
            attacker.AddSimRumorDays(rumorFlagDays);

        Debug.Log(
            $"[Day {day}] [AI 출정] {attacker.DisplayCastleName}({attacker.CountryDisplayName}) → {defender.DisplayCastleName}({defender.CountryDisplayName})");
    }

    void RunGlobalDisaster(int day)
    {
        if (_castles.Count == 0 || UnityEngine.Random.value > globalDisasterChance) return;
        var c = _castles[UnityEngine.Random.Range(0, _castles.Count)];
        if (c == null) return;

        int popLoss = UnityEngine.Random.Range(3, 10);
        c.ApplyPopulationPercentLoss(popLoss);
        c.AddSentiment(UnityEngine.Random.Range(-14, -6));
        c.AddSimDisasterDays(disasterFlagDays);
        Debug.Log($"[Day {day}] [월드맵 재난] {c.DisplayCastleName} — 인구 {popLoss}% 감소, 민심 악화.");
    }

    void RunGlobalFavorable(int day)
    {
        if (_castles.Count == 0 || UnityEngine.Random.value > globalFavorableChance) return;
        var c = _castles[UnityEngine.Random.Range(0, _castles.Count)];
        if (c == null) return;

        c.AddSentiment(UnityEngine.Random.Range(4, 11));
        c.AddCastleValue(UnityEngine.Random.Range(120, 420));
        c.AddSimFavorableDays(favorableFlagDays);
        Debug.Log($"[Day {day}] [월드맵 호재] {c.DisplayCastleName} — 민심·성가치 상승.");
    }

    static void ForEachAdjacentId(string raw, System.Action<string> use)
    {
        if (string.IsNullOrWhiteSpace(raw) || use == null) return;
        var parts = raw.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var s = parts[i].Trim();
            if (!string.IsNullOrEmpty(s)) use(s);
        }
    }

    static string PairKey(string a, string b)
    {
        if (string.CompareOrdinal(a, b) < 0) return a + "|" + b;
        return b + "|" + a;
    }
}
