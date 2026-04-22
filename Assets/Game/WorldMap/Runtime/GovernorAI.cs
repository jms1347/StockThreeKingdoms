using UnityEngine;

/// <summary>성채 단위 일일 AI. <see cref="WorldTimeManager.OnNewDayTick"/>에 반응합니다.</summary>
[RequireComponent(typeof(Castle))]
public class GovernorAI : MonoBehaviour
{
    const int RecruitArmyDelta = 50;
    const int RecruitValueCost = 200;
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

    void OnDay(int day)
    {
        if (_castle == null) return;

        int a0 = _castle.Army;
        int s0 = _castle.PublicSentiment;
        int v0 = _castle.CastleValue;

        if (a0 < 500)
        {
            _castle.AddArmy(RecruitArmyDelta);
            _castle.AddCastleValue(-RecruitValueCost);
            Log(day, "Recruiting", $"Army {a0}→{_castle.Army} (+{RecruitArmyDelta}), Value {v0}→{_castle.CastleValue} (-{RecruitValueCost})");
            return;
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
