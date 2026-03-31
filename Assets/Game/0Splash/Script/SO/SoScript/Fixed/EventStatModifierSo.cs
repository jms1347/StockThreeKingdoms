using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EventStatModifier 시트 한 행. <see cref="EventMasterData.id"/>와 1:1(eventId)로 매칭됩니다.
/// 일일 이벤트 당첨·소문 확정 재주사위 가중치에 태수 스탯과 합산됩니다(<see cref="WorldEventCenter.CalculateFinalWeight"/>).
/// 시트 열 예: A eventId, B flatProbBonus, C perMight, D perIntel, E perCharm, F perInfamy
/// </summary>
[Serializable]
public class EventStatModifierData
{
    /// <summary>이벤트 마스터 ID (<see cref="EventMasterData.id"/>와 동일).</summary>
    public string id;

    public float flatProbBonus;
    public float perMight;
    public float perIntel;
    public float perCharm;
    public float perInfamy;
}

[CreateAssetMenu(fileName = "EventStatModifierSo", menuName = "ScriptableObject/EventStatModifierSo")]
public class EventStatModifierSo : ScriptableObject
{
    public List<EventStatModifierData> list = new List<EventStatModifierData>();
}
