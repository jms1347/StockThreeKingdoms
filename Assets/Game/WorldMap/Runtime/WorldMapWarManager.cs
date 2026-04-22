using System.Collections.Generic;
using UnityEngine;

/// <summary>월드맵 인접 공격으로 시작된 공성 전. <see cref="WorldTimeManager"/> 기준 게임 내 1시간마다 양측 병력이 줄고, 한쪽이 0이 되면 종료합니다.</summary>
public class WorldMapWarManager : MonoBehaviour
{
    public static WorldMapWarManager InstanceOrNull { get; private set; }

    [Tooltip("매 틱마다 각 풀에서 빠지는 병력 비율 하한(0~1).")]
    [SerializeField] float attritionRateMin = 0.04f;

    [Tooltip("매 틱마다 각 풀에서 빠지는 병력 비율 상한(0~1).")]
    [SerializeField] float attritionRateMax = 0.11f;

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
        float step = GetRealSecondsPerGameHour();
        _attritionAccum += Time.deltaTime;
        while (_attritionAccum >= step)
        {
            _attritionAccum -= step;
            TickAllWarsAttrition();
        }
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
        if (IsCastleInAnyWar(attacker) || IsCastleInAnyWar(defender))
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
            MarchMarker = marchMarker
        };
        _wars.Add(w);

        defender.SetArmy(defPool);

        Debug.Log(
            $"[공성 시작] {attacker.DisplayCastleName} → {defender.DisplayCastleName} | 공격 {invadingTroops:N0} vs 방어 {defPool:N0} (게임 시간 1시간 ≈ 현실 {GetRealSecondsPerGameHour():N2}초마다 병력 감소)");

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
            EndWar(w, "양측 소모 — 공격군 전멸, 수비 병력 소진");
            return;
        }

        if (defDead)
        {
            w.Defender.SetArmy(0);
            w.Attacker.AddArmy(w.AttackerTroops);
            EndWar(w, $"공격측 승리 — {w.Attacker.DisplayCastleName}이(가) {w.Defender.DisplayCastleName} 함락");
            return;
        }

        w.Defender.SetArmy(w.DefenderTroops);
        EndWar(w, $"수비측 승리 — {w.Defender.DisplayCastleName} 방어 성공");
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
    }
}
