using System.Collections.Generic;
using UnityEngine;

/// <summary>월드맵 인접 공격으로 시작된 공성 전. 병력 소모 틱마다 양측이 줄고, 한쪽이 0이 되면 종료합니다.</summary>
public class WorldMapWarManager : MonoBehaviour
{
    public static WorldMapWarManager InstanceOrNull { get; private set; }

    [Header("공성 교전 틱")]
    [Tooltip(
        "공성 병력 소모를 돌리는 현실 시간 간격(초). 0 이하면 달력과 동기(게임 내 1시간 = WorldTimeManager의 하루 길이 ÷ 24). " +
        "하루를 10초로 두면 그 값은 약 0.42초라 전쟁이 순식간에 끝나므로, 기본은 공성만 느리게 틱합니다.")]
    [SerializeField] float siegeAttritionTickRealSeconds = 3f;

    [Tooltip("매 틱마다 각 풀에서 빠지는 병력 비율 하한(0~1).")]
    [SerializeField] float attritionRateMin = 0.01f;

    [Tooltip("매 틱마다 각 풀에서 빠지는 병력 비율 상한(0~1).")]
    [SerializeField] float attritionRateMax = 0.03f;

    [Tooltip("같은 성이 도로로 연결된 적 성에 동시에 벌일 수 있는 공성(공격) 수.")]
    [SerializeField] int maxConcurrentAttacksPerCastle = 2;

    readonly List<ActiveSiegeWar> _wars = new List<ActiveSiegeWar>(16);
    float _attritionAccum;

    void Awake()
    {
        InstanceOrNull = this;
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
    }

    void Update()
    {
        float step = GetSiegeAttritionStepSeconds();
        _attritionAccum += Time.deltaTime;
        while (_attritionAccum >= step)
        {
            _attritionAccum -= step;
            TickAllWarsAttrition();
        }
    }

    /// <summary>공성 소모 틱 간격. <see cref="siegeAttritionTickRealSeconds"/>가 양수면 그 값, 아니면 달력 1게임시간.</summary>
    float GetSiegeAttritionStepSeconds()
    {
        if (siegeAttritionTickRealSeconds > 0f)
            return Mathf.Max(0.05f, siegeAttritionTickRealSeconds);
        return GetRealSecondsPerGameHour();
    }

    /// <summary>게임 시간 1시간 = (하루에 해당하는 현실 초) / 24.</summary>
    public static float GetRealSecondsPerGameHour()
    {
        var t = WorldTimeManager.InstanceOrNull;
        if (t != null)
            return Mathf.Max(0.01f, t.RealSecondsPerGameHour);
        return 10f / 24f;
    }

    public bool IsCastleInAnyWar(Castle c)
    {
        if (c == null) return false;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && (w.Attacker == c || w.Defender == c))
                return true;
        }

        return false;
    }

    /// <summary>월드맵 HUD의 「전쟁중」은 수비 성에만 표시합니다.</summary>
    public bool IsCastleSiegeDefender(Castle c)
    {
        if (c == null) return false;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && w.Defender == c)
                return true;
        }

        return false;
    }

    public int MaxConcurrentAttacksPerCastle => Mathf.Max(1, maxConcurrentAttacksPerCastle);

    public int CountActiveSiegesWhereAttacker(Castle attacker)
    {
        if (attacker == null) return 0;
        int n = 0;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && w.Attacker == attacker)
                n++;
        }

        return n;
    }

    public bool TryGetWarForMarch(MarchingTroopMarker marker, out ActiveSiegeWar war)
    {
        war = null;
        if (marker == null) return false;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && w.MarchMarker == marker)
            {
                war = w;
                return true;
            }
        }

        return false;
    }

    /// <summary>행군 도착 시 호출. 어느 한 성이라도 이미 전쟁 중이면 false.</summary>
    public bool TryBeginSiege(Castle attacker, Castle defender, int invadingTroops, MarchingTroopMarker marchMarker)
    {
        if (attacker == null || defender == null || marchMarker == null)
            return false;
        if (invadingTroops < 1)
            return false;
        if (IsCastleSiegeDefender(attacker))
            return false;
        if (CountActiveSiegesWhereAttacker(attacker) >= MaxConcurrentAttacksPerCastle)
            return false;
        if (IsCastleInAnyWar(defender))
            return false;

        int defPool = Mathf.Max(0, defender.Army);
        if (defPool < 1)
            return false;

        var w = new ActiveSiegeWar
        {
            Attacker = attacker,
            Defender = defender,
            AttackerTroops = invadingTroops,
            DefenderTroops = defPool,
            GeneralName = attacker.GovernorName,
            MarchMarker = marchMarker,
            AttackerGovernorGeneralId = WorldMapGeneralRoster.ResolveMarchingGovernorId(attacker),
            DefenderGovernorGeneralId = defender.GovernorGeneralId
        };
        _wars.Add(w);

        defender.SetArmy(defPool);

        if (!string.IsNullOrWhiteSpace(w.AttackerGovernorGeneralId))
            WorldMapGeneralRoster.NotifySiegeEngaged(w.AttackerGovernorGeneralId, defender);

        Debug.Log(
            $"[공성 시작] {attacker.DisplayCastleName} → {defender.DisplayCastleName} | 공격 {invadingTroops:N0} vs 방어 {defPool:N0} (교전 틱 ≈ 현실 {GetSiegeAttritionStepSeconds():N2}초마다 병력 감소)");

        MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
        return true;
    }

    void TickAllWarsAttrition()
    {
        for (int i = _wars.Count - 1; i >= 0; i--)
        {
            var w = _wars[i];
            if (w == null || w.Attacker == null || w.Defender == null)
            {
                _wars.RemoveAt(i);
                continue;
            }

            ApplyOneAttrition(w);
            if (_wars.Contains(w))
                TryResolveWarOutcome(w);
        }
    }

    void ApplyOneAttrition(ActiveSiegeWar w)
    {
        if (w.AttackerTroops <= 0 || w.DefenderTroops <= 0)
            return;

        float rA = Random.Range(attritionRateMin, attritionRateMax);
        float rD = Random.Range(attritionRateMin, attritionRateMax);
        int lossA = Mathf.Max(1, Mathf.RoundToInt(w.AttackerTroops * rA));
        int lossD = Mathf.Max(1, Mathf.RoundToInt(w.DefenderTroops * rD));

        w.AttackerTroops = Mathf.Max(0, w.AttackerTroops - lossA);
        w.DefenderTroops = Mathf.Max(0, w.DefenderTroops - lossD);

        w.Defender.SetArmy(w.DefenderTroops);

        Debug.Log(
            $"[공성 교전] {w.Attacker.DisplayCastleName} vs {w.Defender.DisplayCastleName} — " +
            $"공격군 -{lossA:N0} (잔여 {w.AttackerTroops:N0}), 수비 -{lossD:N0} (잔여 {w.DefenderTroops:N0})");
    }

    void TryResolveWarOutcome(ActiveSiegeWar w)
    {
        bool attDead = w.AttackerTroops <= 0;
        bool defDead = w.DefenderTroops <= 0;

        if (!attDead && !defDead)
            return;

        if (attDead && defDead)
        {
            w.Defender.SetArmy(0);
            FinalizeSiegeRosterMutualDestruction(w);
            EndWar(w, "양측 소모 — 공격군 전멸, 수비 병력 소진");
            return;
        }

        if (defDead)
        {
            w.Defender.SetArmy(0);
            w.Attacker.AddArmy(w.AttackerTroops);
            ApplySiegeConquestVisualAndData(w);
            EndWar(w, $"공격측 승리 — {w.Attacker.DisplayCastleName}이(가) {w.Defender.DisplayCastleName} 함락");
            return;
        }

        w.Defender.SetArmy(w.DefenderTroops);
        FinalizeSiegeRosterDefenderVictory(w);
        EndWar(w, $"수비측 승리 — {w.Defender.DisplayCastleName} 방어 성공");
    }

    void ApplySiegeConquestVisualAndData(ActiveSiegeWar w)
    {
        if (w?.Defender == null || w.Attacker == null) return;

        string oldDefGov = w.Defender.GovernorGeneralId;

        var colors = MapManager.InstanceOrNull != null ? MapManager.InstanceOrNull.CountryColorsOrNull : null;
        if (colors == null)
            colors = UnityEngine.Object.FindFirstObjectByType<CountryColorProvider>();

        w.Defender.ApplyConquestFromAttacker(w.Attacker, colors);

        var dm = DataManager.InstanceOrNull;
        if (dm != null && !string.IsNullOrWhiteSpace(w.Defender.MasterId))
        {
            var lord = WorldMapRowFactory.CountryIdToFaction(w.Attacker.CountryId);
            dm.ApplyWorldMapSiegeConquestLord(w.Defender.MasterId.Trim(), lord);
            string newGov = w.Attacker.GovernorGeneralId;
            if (string.IsNullOrWhiteSpace(newGov))
                newGov = WorldMapGeneralRoster.ResolveMarchingGovernorId(w.Attacker);
            dm.ApplyWorldMapSiegeConquestGovernor(w.Defender.MasterId.Trim(), newGov);
            WorldMapGeneralRoster.RebuildFromDataManager(dm);
            if (!string.IsNullOrWhiteSpace(oldDefGov))
                WorldMapGeneralRoster.NotifyDefenderGovernorCaptured(oldDefGov, w.Attacker);
        }
        else if (!string.IsNullOrWhiteSpace(oldDefGov))
        {
            WorldMapGeneralRoster.NotifyDefenderGovernorCaptured(oldDefGov, w.Attacker);
            if (!string.IsNullOrWhiteSpace(w.AttackerGovernorGeneralId))
                WorldMapGeneralRoster.NotifySiegeEndAttackerVictory(w.AttackerGovernorGeneralId, w.Defender);
        }
    }

    void FinalizeSiegeRosterDefenderVictory(ActiveSiegeWar w)
    {
        if (w?.Attacker == null) return;
        if (!string.IsNullOrWhiteSpace(w.AttackerGovernorGeneralId))
            WorldMapGeneralRoster.NotifySiegeEndDefenderVictory(w.AttackerGovernorGeneralId, w.Attacker);
    }

    void FinalizeSiegeRosterMutualDestruction(ActiveSiegeWar w)
    {
        if (w?.Attacker == null) return;
        if (!string.IsNullOrWhiteSpace(w.AttackerGovernorGeneralId))
            WorldMapGeneralRoster.NotifyMarchReturnedHome(w.AttackerGovernorGeneralId, w.Attacker);
    }

    /// <summary>인접 동료 성이 수비 중인 공성에 병력을 더합니다.</summary>
    public bool TrySendNeighborReinforcement(Castle supporterCastle, Castle besiegedDefender, int troopAmount)
    {
        if (supporterCastle == null || besiegedDefender == null || troopAmount < 1) return false;
        if (supporterCastle.CountryId != besiegedDefender.CountryId) return false;
        var mm = MapManager.InstanceOrNull;
        if (mm == null || !mm.AreConnectedByRoads(supporterCastle, besiegedDefender)) return false;

        ActiveSiegeWar war = null;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w?.Defender == besiegedDefender)
            {
                war = w;
                break;
            }
        }

        if (war == null) return false;

        troopAmount = Mathf.Min(troopAmount, supporterCastle.Army);
        if (troopAmount < 1) return false;

        supporterCastle.AddArmy(-troopAmount);
        war.DefenderTroops += troopAmount;
        besiegedDefender.SetArmy(war.DefenderTroops);

        Debug.Log(
            $"[공성 지원] {supporterCastle.DisplayCastleName} → {besiegedDefender.DisplayCastleName}에 +{troopAmount:N0} (수비 풀 {war.DefenderTroops:N0})");

        MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
        MapManager.InstanceOrNull?.RefreshCastleDetailIfOpen();
        return true;
    }

    /// <summary>인접 동료 성이 공격 중인 공성에 공격 병력을 더합니다.</summary>
    public bool TrySendNeighborAttackReinforcement(Castle supporterCastle, Castle attackerAlly, int troopAmount)
    {
        if (supporterCastle == null || attackerAlly == null || troopAmount < 1) return false;
        if (supporterCastle.CountryId != attackerAlly.CountryId) return false;
        var mm = MapManager.InstanceOrNull;
        if (mm == null || !mm.AreConnectedByRoads(supporterCastle, attackerAlly)) return false;

        ActiveSiegeWar war = null;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w?.Attacker == attackerAlly)
            {
                war = w;
                break;
            }
        }

        if (war == null) return false;

        troopAmount = Mathf.Min(troopAmount, supporterCastle.Army);
        if (troopAmount < 1) return false;

        supporterCastle.AddArmy(-troopAmount);
        war.AttackerTroops += troopAmount;

        Debug.Log(
            $"[공성 공격 지원] {supporterCastle.DisplayCastleName} → {attackerAlly.DisplayCastleName} 공격측 +{troopAmount:N0} (공격 풀 {war.AttackerTroops:N0})");

        MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
        MapManager.InstanceOrNull?.RefreshCastleDetailIfOpen();
        return true;
    }

    public bool TryFindWarDefendingCastle(Castle defender, out ActiveSiegeWar war)
    {
        war = null;
        if (defender == null) return false;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && w.Defender == defender)
            {
                war = w;
                return true;
            }
        }

        return false;
    }

    public bool TryFindWarAttackingCastle(Castle attacker, out ActiveSiegeWar war)
    {
        war = null;
        if (attacker == null) return false;
        for (int i = 0; i < _wars.Count; i++)
        {
            var w = _wars[i];
            if (w != null && w.Attacker == attacker)
            {
                war = w;
                return true;
            }
        }

        return false;
    }

    void EndWar(ActiveSiegeWar w, string reason)
    {
        if (w.MarchMarker != null)
            Destroy(w.MarchMarker.gameObject);

        _wars.Remove(w);

        Debug.Log($"[공성 종료] {reason}");
        MapManager.InstanceOrNull?.RefreshAllCastleMapStatuses();
        MapManager.InstanceOrNull?.NotifySiegeEndedForUi();
    }

    public class ActiveSiegeWar
    {
        public Castle Attacker;
        public Castle Defender;
        public int AttackerTroops;
        public int DefenderTroops;
        public string GeneralName;
        public MarchingTroopMarker MarchMarker;
        public string AttackerGovernorGeneralId;
        public string DefenderGovernorGeneralId;
    }
}
