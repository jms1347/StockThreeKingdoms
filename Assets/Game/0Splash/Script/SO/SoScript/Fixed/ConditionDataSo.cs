using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ConditionData
{
    [Tooltip("시트 A열 condId")]
    public string conditionId;

    /// <summary>시트·기획 명칭과 동일 (<see cref="conditionId"/> 별칭).</summary>
    public string condId
    {
        get => conditionId;
        set => conditionId = value ?? "";
    }

    [Tooltip("시트 B열 targetAttr (예: might, gold, public_order)")]
    public string targetAttr;

    [Tooltip("시트 C열 op (==, !=, >, <, >=, <=)")]
    public string targetOp;

    [Tooltip("시트 D열 thresholdValue")]
    public float targetValue;

    /// <summary>시트 D열과 동일 (<see cref="targetValue"/> 별칭).</summary>
    public float thresholdValue
    {
        get => targetValue;
        set => targetValue = value;
    }

    [Tooltip("시트 E열 Description (기획 참고, 런타임 미사용)")]
    public string description;
}
/// <summary>
/// 조건 라이브러리(Condition 탭) 한 행. 시트 열: A condId, B targetAttr, C op, D thresholdValue, E Description.
/// targetAttr: gold(금화), food(식량), soldiers(병사), public_order(민심), population(백성수), might(무력), intel(지력), charm(매력) 등.
/// </summary>
[CreateAssetMenu(fileName = "ConditionDataSo", menuName = "ScriptableObject/ConditionDataSo")]
public class ConditionDataSo : ScriptableObject
{
    public List<ConditionData> list = new List<ConditionData>();
}
