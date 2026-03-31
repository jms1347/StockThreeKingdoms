using System;
using UnityEngine;

/// <summary>
/// <see cref="ConditionData"/>·<see cref="EventMasterData.conditionIds"/> 평가.
/// 시트 B열은 <see cref="ConditionTypeSheetParser"/>로 <see cref="ConditionType"/>에 매핑됩니다.
/// </summary>
public static class EventConditionEvaluator
{
    const float FloatEpsilon = 0.0001f;

    /// <summary>ConditionMaster의 단일 condId가 성 상태·태수 스탯과 맞는지.</summary>
    public static bool IsConditionMet(DataManager dm, CastleStateData castle, string condId)
    {
        if (string.IsNullOrWhiteSpace(condId) || dm?.conditionDataMap == null)
            return false;
        if (!dm.conditionDataMap.TryGetValue(condId.Trim(), out var c) || c == null)
            return false;
        return EvaluateSingle(dm, castle, c);
    }

    public static bool IsAllSatisfiedFromLibrary(DataManager dm, CastleStateData castle, EventMasterData ev)
    {
        if (ev?.conditionIds == null || ev.conditionIds.Count == 0)
            return true;
        if (dm?.conditionDataMap == null)
            return false;

        for (int i = 0; i < ev.conditionIds.Count; i++)
        {
            string cid = ev.conditionIds[i]?.Trim();
            if (string.IsNullOrEmpty(cid)) continue;
            if (!IsConditionMet(dm, castle, cid))
                return false;
        }

        return true;
    }

    public static bool EvaluateSingle(DataManager dm, CastleStateData castle, ConditionData c)
    {
        if (c == null)
            return false;
        if (c.conditionType == ConditionType.None)
            return true;

        var gov = ResolveGovernor(dm, castle);
        if (!TryGetAttributeValue(dm, castle, gov, c.conditionType, out float actual))
            return false;
        return Compare(c.conditionOperator, actual, c.targetValue);
    }

    static GeneralMasterData ResolveGovernor(DataManager dm, CastleStateData castle)
    {
        if (dm == null || castle == null || string.IsNullOrWhiteSpace(castle.currentGovernorId))
            return null;
        return dm.GetGeneralMasterData(castle.currentGovernorId);
    }

    static bool TryGetAttributeValue(DataManager dm, CastleStateData castle, GeneralMasterData gov, ConditionType attr,
        out float value)
    {
        value = 0f;
        switch (attr)
        {
            case ConditionType.Might:
                if (gov == null) return false;
                value = gov.power;
                return true;
            case ConditionType.Intel:
                if (gov == null) return false;
                value = gov.intel;
                return true;
            case ConditionType.Charm:
                if (gov == null) return false;
                value = gov.charm;
                return true;
            case ConditionType.Notoriety:
                if (gov == null) return false;
                value = gov.infamy;
                return true;
            case ConditionType.PublicOrder:
                if (castle == null) return false;
                value = castle.currentSentiment;
                return true;
            case ConditionType.Soldiers:
            case ConditionType.Population:
                if (castle == null) return false;
                value = castle.currentPopulation;
                return true;
            case ConditionType.Gold:
                value = GetPlayerGoldF();
                return true;
            case ConditionType.Food:
                value = GetPlayerGrainF();
                return true;
            default:
                return false;
        }
    }

    static float GetPlayerGoldF()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return 0f;
        return gm.currentGold > (long)int.MaxValue ? int.MaxValue : (float)gm.currentGold;
    }

    static float GetPlayerGrainF()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return 0f;
        return gm.currentGrain > (long)int.MaxValue ? int.MaxValue : (float)gm.currentGrain;
    }

    public static bool Compare(ConditionOperator op, float actual, float threshold)
    {
        switch (op)
        {
            case ConditionOperator.Equal:
                return Mathf.Abs(actual - threshold) < FloatEpsilon;
            case ConditionOperator.NotEqual:
                return Mathf.Abs(actual - threshold) >= FloatEpsilon;
            case ConditionOperator.GreaterThan:
                return actual > threshold;
            case ConditionOperator.LessThan:
                return actual < threshold;
            case ConditionOperator.GreaterOrEqual:
                return actual >= threshold - FloatEpsilon;
            case ConditionOperator.LessOrEqual:
                return actual <= threshold + FloatEpsilon;
            default:
                return false;
        }
    }

    /// <summary>레거시·시트 문자열 경로용. 신규 코드는 <see cref="Compare(ConditionOperator, float, float)"/> 권장.</summary>
    public static bool Compare(EventConditionOp op, float actual, float threshold)
    {
        switch (op)
        {
            case EventConditionOp.Eq:
                return Mathf.Abs(actual - threshold) < FloatEpsilon;
            case EventConditionOp.Ne:
                return Mathf.Abs(actual - threshold) >= FloatEpsilon;
            case EventConditionOp.Gt:
                return actual > threshold;
            case EventConditionOp.Lt:
                return actual < threshold;
            case EventConditionOp.Ge:
                return actual >= threshold - FloatEpsilon;
            case EventConditionOp.Le:
                return actual <= threshold + FloatEpsilon;
            default:
                return false;
        }
    }
}
