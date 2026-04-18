using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 이벤트용 조건 라이브러리(시트 Condition 탭). 각 행의 <see cref="ConditionData.conditionId"/>가
/// <see cref="EventMasterData.conditionIds"/>에 들어가며, <b>한 이벤트 행 안에서는 모두 만족(AND)</b>해야 해당 행이 후보가 됩니다.
/// 구글 시트에서 내려받을 때 B·C열 문자열은 <see cref="ConditionTypeSheetParser"/>·<see cref="ConditionOperatorSheetParser"/>로 enum에 매핑됩니다.
/// 런타임 평가는 <see cref="EventConditionEvaluator"/>·<see cref="WorldEventCenter.SelectRandomSatisfiedRow"/>를 참고하세요.
/// </summary>
public enum ConditionType
{
    None = 0,
    PublicOrder = 1,    // 민심 (public_order)
    Gold = 2,           // 금화 (gold)
    Food = 3,           // 레거시 키(식량) — 평가 시 플레이어 금화로 처리
    Population = 4,     // 백성수 (population)
    Might = 5,          // 무력 (might)
    Intel = 6,          // 지력 (intel)
    Charm = 7,          // 매력 (charm)
    Soldiers = 8,        // 병사수 (soldiers)
    Notoriety = 9       // 악명 (추가됨)
}

/// <summary>
/// 조건 비교 연산자
/// </summary>
public enum ConditionOperator
{
    LessThan = 0,           // <
    LessOrEqual = 1,        // <=
    GreaterThan = 2,        // >
    GreaterOrEqual = 3,     // >=
    Equal = 4,              // Equal
    NotEqual = 5            // !=
}

/// <summary>구글 시트 B열 targetAttr 문자열 → <see cref="ConditionType"/>.</summary>
public static class ConditionTypeSheetParser
{
    public static bool TryParse(string raw, out ConditionType type)
    {
        type = ConditionType.None;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string t = raw.Trim();
        if (Enum.TryParse(t, true, out type) && Enum.IsDefined(typeof(ConditionType), type) && type != ConditionType.None)
            return true;

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
            Enum.IsDefined(typeof(ConditionType), n))
        {
            type = (ConditionType)n;
            return type != ConditionType.None;
        }

        string k = NormalizeKey(t);
        switch (k)
        {
            case "public_order":
            case "publicorder":
            case "public_sentiment":
            case "sentiment":
            case "order":
                type = ConditionType.PublicOrder;
                return true;
            case "gold":
                type = ConditionType.Gold;
                return true;
            case "food":
            case "grain":
                type = ConditionType.Food;
                return true;
            case "population":
            case "pop":
                type = ConditionType.Population;
                return true;
            case "might":
            case "power":
                type = ConditionType.Might;
                return true;
            case "intel":
                type = ConditionType.Intel;
                return true;
            case "charm":
                type = ConditionType.Charm;
                return true;
            case "soldiers":
            case "soldier":
            case "troops":
                type = ConditionType.Soldiers;
                return true;
            case "notoriety":
            case "infamy":
                type = ConditionType.Notoriety;
                return true;
        }

        return TryParseKoreanLabel(t, out type);
    }

    static bool TryParseKoreanLabel(string t, out ConditionType type)
    {
        type = ConditionType.None;
        t = t.Trim().Replace('\u3000', ' ');
        if (t == "\uBBFC\uC2EC") { type = ConditionType.PublicOrder; return true; }
        if (t == "\uAE08\uD654") { type = ConditionType.Gold; return true; }
        if (t == "\uC2DD\uB7C9") { type = ConditionType.Food; return true; }
        if (t == "\uBC31\uC131\uC218") { type = ConditionType.Population; return true; }
        if (t == "\uBB34\uB825") { type = ConditionType.Might; return true; }
        if (t == "\uC9C0\uB825") { type = ConditionType.Intel; return true; }
        if (t == "\uB9E4\uB825") { type = ConditionType.Charm; return true; }
        if (t == "\uBCD1\uC0AC" || t == "\uBCD1\uC0AC\uC218") { type = ConditionType.Soldiers; return true; }
        if (t == "\uC545\uBA85") { type = ConditionType.Notoriety; return true; }
        return false;
    }

    static string NormalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().ToLowerInvariant().Replace('-', '_').Replace(" ", "");
    }
}

/// <summary>구글 시트 C열 op 문자열 → <see cref="ConditionOperator"/>.</summary>
public static class ConditionOperatorSheetParser
{
    public static bool TryParse(string raw, out ConditionOperator op)
    {
        op = ConditionOperator.Equal;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string t = raw.Trim();
        if (Enum.TryParse(t, true, out op) && Enum.IsDefined(typeof(ConditionOperator), op))
            return true;

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
            Enum.IsDefined(typeof(ConditionOperator), n))
        {
            op = (ConditionOperator)n;
            return true;
        }

        string s = t.ToLowerInvariant().Replace(" ", "").Replace("＝", "=").Replace("＜", "<").Replace("＞", ">");

        switch (s)
        {
            case "<":
            case "lt":
            case "lessthan":
                op = ConditionOperator.LessThan;
                return true;
            case "<=":
            case "le":
            case "lessorequal":
            case "lessthanorequal":
                op = ConditionOperator.LessOrEqual;
                return true;
            case ">":
            case "gt":
            case "greaterthan":
                op = ConditionOperator.GreaterThan;
                return true;
            case ">=":
            case "ge":
            case "greaterorequal":
            case "greaterthanorequal":
                op = ConditionOperator.GreaterOrEqual;
                return true;
            case "=":
            case "==":
            case "eq":
            case "equal":
            case "equals":
                op = ConditionOperator.Equal;
                return true;
            case "!=":
            case "ne":
            case "notequal":
            case "notequals":
                op = ConditionOperator.NotEqual;
                return true;
            default:
                return false;
        }
    }
}

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
    public ConditionType conditionType;

    [Tooltip("시트 C열 op (==, !=, >, <, >=, <=)")]
    public ConditionOperator conditionOperator;

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
/// B·C열 문자열은 <see cref="ConditionTypeSheetParser"/>·<see cref="ConditionOperatorSheetParser"/>로 enum에 매핑됩니다.
/// </summary>
[CreateAssetMenu(fileName = "ConditionDataSo", menuName = "ScriptableObject/ConditionDataSo")]
public class ConditionDataSo : ScriptableObject
{
    public List<ConditionData> list = new List<ConditionData>();
}
