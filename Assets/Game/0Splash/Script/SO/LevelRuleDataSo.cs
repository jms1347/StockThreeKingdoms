using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelRuleData
{
    // ¿¹: "Rule_Edu_Easy_1"
    public string ruleID;

    // ¿¹: 5 (È½¼ö)
    public int requiredCount;
}

[CreateAssetMenu(fileName = "LevelRuleDataSo", menuName = "ScriptableObject/LevelRuleDataSo")]
public class LevelRuleDataSo : ScriptableObject
{
    public List<LevelRuleData> list = new List<LevelRuleData>();
}