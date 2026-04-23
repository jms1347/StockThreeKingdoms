using UnityEngine;

/// <summary>성채 단위 일일 AI. <see cref="WorldTimeManager.OnNewDayTick"/>에 반응합니다.</summary>
[RequireComponent(typeof(Castle))]
public class GovernorAI : MonoBehaviour
{
    [Header("태수 성향 (징병 확률)")]
    [Tooltip("기본 징병 시도 확률. 태수 능력으로 가산됩니다.")]
    [SerializeField] [Range(0.05f, 0.95f)] float baseRecruitChance = 0.44f;

    const int AlmsSentimentDelta = 10;
    const int AlmsValueCost = 100;
    const int InvestValueDelta = 300;

    Castle _castle;
    WorldTimeManager _time;

    void Awake() => _castle = GetComponent<Castle>();

    void Start()
    {
        _time = WorldTimeManager.InstanceOrNull;
        if (_time != null)
            _time.OnNewDayTick += OnDay;
    }

    void OnDestroy()
    {
        if (_time != null)
            _time.OnNewDayTick -= OnDay;
    }

    static GeneralMasterData ResolveGovernor(DataManager dm, Castle castle)
    {
        if (dm == null || castle == null) return null;
        if (!string.IsNullOrWhiteSpace(castle.GovernorGeneralId))
            return dm.GetGeneralMasterData(castle.GovernorGeneralId);
        if (!string.IsNullOrWhiteSpace(castle.MasterId) &&
            dm.castleStateDataMap != null &&
            dm.castleStateDataMap.TryGetValue(castle.MasterId.Trim(), out var st) &&
            st != null &&
            !string.IsNullOrWhiteSpace(st.currentGovernorId))
            return dm.GetGeneralMasterData(st.currentGovernorId.Trim());
        return null;
    }

    void OnDay(int day)
    {
        if (_castle == null) return;

        int a0 = _castle.Army;
        int s0 = _castle.PublicSentiment;
        int v0 = _castle.CastleValue;
        int p0 = _castle.Population;

        var dm = DataManager.InstanceOrNull;
        var gov = ResolveGovernor(dm, _castle);

        float pRecruit = baseRecruitChance;
        if (gov != null)
            pRecruit += (gov.charm * 0.72f + gov.intel * 0.38f + gov.power * 0.18f) / 1200f;
        pRecruit = Mathf.Clamp01(pRecruit);
        if (a0 > 4000)
            pRecruit *= 0.38f;
        if (s0 < 26)
            pRecruit *= 0.42f;
        if (p0 < 200)
            pRecruit = 0f;

        if (Random.value < pRecruit)
        {
            var ledger = WorldMapRecruitCalculator.ComputeRecruitLedger(_castle, gov);
            if (ledger.Recruit > 0)
            {
                _castle.AddArmy(ledger.Recruit);
                _castle.AddCastleValue(-ledger.ValueCost);
                _castle.AddPopulation(-ledger.PopulationLoss);
                _castle.AddSentiment(-ledger.SentimentLoss);
                if (dm != null && !string.IsNullOrWhiteSpace(_castle.MasterId))
                    dm.ApplyWorldMapPopulationDelta(_castle.MasterId, -ledger.PopulationLoss);

                Log(day, "Recruiting",
                    $"Army {a0}→{_castle.Army} (+{ledger.Recruit}), 가치금 {v0}→{_castle.CastleValue} (-{ledger.ValueCost}), " +
                    $"인구 {p0}→{_castle.Population} (-{ledger.PopulationLoss}), 민심 {s0}→{_castle.PublicSentiment} (-{ledger.SentimentLoss})");
                return;
            }
        }

        if (s0 < 50)
        {
            _castle.AddSentiment(AlmsSentimentDelta);
            _castle.AddCastleValue(-AlmsValueCost);
            Log(day, "Giving Alms", $"Sentiment {s0}→{_castle.PublicSentiment} (+{AlmsSentimentDelta}), Value {v0}→{_castle.CastleValue} (-{AlmsValueCost})");
            return;
        }

        if (a0 > 1000 && s0 > 80)
        {
            _castle.AddCastleValue(InvestValueDelta);
            Log(day, "Investing", $"Value {v0}→{_castle.CastleValue} (+{InvestValueDelta})");
        }
    }

    void Log(int day, string action, string statChanges)
    {
        var colors = UnityEngine.Object.FindFirstObjectByType<CountryColorProvider>();
        string country = colors != null ? colors.GetCountryDisplayName(_castle.CountryId) : _castle.CountryId.ToString();
        Debug.Log($"[Day {day}] [{country}/{_castle.DisplayCastleName}] Governor {_castle.GovernorName} performed {action}. Results: {statChanges}");
    }
}
