using System;
using UnityEngine;

/// <summary>
/// <see cref="ConditionData"/>·<see cref="EventMasterData.conditionIds"/> 평가.
/// targetAttr(시트 B열, 대소문자 무시): gold 금화, food 식량, soldiers 병사, public_order 민심, population 백성수, might 무력, intel 지력, charm 매력.
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
        if (c == null || string.IsNullOrWhiteSpace(c.targetAttr))
            return true;
        if (!GoogleSheetManager.TryParseEventConditionOp(c.targetOp, out var op))
            return false;
        var gov = ResolveGovernor(dm, castle);
        if (!TryGetAttributeValue(dm, castle, gov, c.targetAttr, out float actual))
            return false;
        return Compare(op, actual, c.targetValue);
    }

    static GeneralMasterData ResolveGovernor(DataManager dm, CastleStateData castle)
    {
        if (dm == null || castle == null || string.IsNullOrWhiteSpace(castle.currentGovernorId))
            return null;
        return dm.GetGeneralMasterData(castle.currentGovernorId);
    }

    static bool TryGetAttributeValue(DataManager dm, CastleStateData castle, GeneralMasterData gov, string rawKey,
        out float value)
    {
        value = 0f;
        string key = NormalizeAttrKey(rawKey);
        if (string.IsNullOrEmpty(key))
            return false;

        switch (key)
        {
            case "might":
            case "power":
            case "무력":
                if (gov == null) return false;
                value = gov.power;
                return true;
            case "intel":
            case "지력":
                if (gov == null) return false;
                value = gov.intel;
                return true;
            case "charm":
            case "매력":
                if (gov == null) return false;
                value = gov.charm;
                return true;
            case "infamy":
            case "악명":
                if (gov == null) return false;
                value = gov.infamy;
                return true;
            case "public_sentiment":
            case "public_order":
            case "sentiment":
            case "민심":
                if (castle == null) return false;
                value = castle.currentSentiment;
                return true;
            case "soldiers":
            case "soldier":
            case "병사":
                if (castle == null) return false;
                value = castle.currentPopulation;
                return true;
            case "population":
            case "백성":
            case "백성수":
                if (castle == null) return false;
                value = castle.currentPopulation;
                return true;
            case "gold":
            case "금화":
                value = GetPlayerGoldF();
                return true;
            case "food":
            case "grain":
            case "식량":
                value = GetPlayerGrainF();
                return true;
            default:
                return false;
        }
    }

    static string NormalizeAttrKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().ToLowerInvariant().Replace('-', '_').Replace(" ", "_");
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
