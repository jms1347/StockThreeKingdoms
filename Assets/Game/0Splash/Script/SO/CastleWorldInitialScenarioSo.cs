using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마스터로 성 상태를 만든 뒤(태수 배정까지), 지정한 성만 덮어써 "초기 천하/AI 상황"을 SO로 설계합니다.
/// 디스크에 <c>castle_state.json</c>이 있으면 이 시나리오는 적용되지 않습니다.
/// </summary>
[CreateAssetMenu(fileName = "CastleWorldInitialScenarioSo", menuName = "ScriptableObject/CastleWorldInitialScenarioSo")]
public class CastleWorldInitialScenarioSo : ScriptableObject
{
    [Tooltip("켜면 BuildStateDataFromMaster 직후에만 entries 적용")]
    public bool enabled = true;

    public List<CastleWorldInitialEntry> entries = new List<CastleWorldInitialEntry>();
}

[Serializable]
public class CastleWorldInitialEntry
{
    public string castleId;

    public bool overrideLord;
    public Faction currentLord;

    public bool overridePopulation;
    public int currentPopulation;

    public bool overrideSentiment;
    [Range(0f, 200f)] public float currentSentiment = 100f;

    public bool overrideGovernor;
    public string currentGovernorId;

    public bool overrideWar;
    public bool isWar;

    public bool overrideDisaster;
    public bool isDisaster;

    public bool overrideFavorableEvent;
    public bool isFavorableEvent;

    public void ApplyTo(CastleStateData s)
    {
        if (s == null) return;
        if (overrideLord) s.currentLord = currentLord;
        if (overridePopulation) s.currentPopulation = Mathf.Max(0, currentPopulation);
        if (overrideSentiment)
        {
            s.currentSentiment = Mathf.Clamp(currentSentiment, 0f, 200f);
            if (s.sentimentHistory == null) s.sentimentHistory = new List<float>();
            if (s.sentimentHistory.Count == 0) s.sentimentHistory.Add(s.currentSentiment);
            else s.sentimentHistory[s.sentimentHistory.Count - 1] = s.currentSentiment;
        }

        if (overrideGovernor)
        {
            s.currentGovernorId = currentGovernorId ?? "";
            s.lastDailyBuffGovernorId = "";
            s.lastDailyBuffTime = 0;
        }

        if (overrideWar) s.isWar = isWar;
        if (overrideDisaster)
        {
            s.isDisaster = isDisaster;
            if (isDisaster) s.isFavorableEvent = false;
        }

        if (overrideFavorableEvent)
        {
            s.isFavorableEvent = isFavorableEvent;
            if (isFavorableEvent) s.isDisaster = false;
        }

        if (overridePopulation && s.populationHistory != null && s.populationHistory.Count > 0)
            s.populationHistory[s.populationHistory.Count - 1] = s.currentPopulation;
    }
}
