using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelRuleData
{
    public int level;

    public double clickPowerCost;
    public double clickPowerValue;

    public double autoIncomeCost;
    public double autoIncomeValue;
    public double maxStorageCapacity;  // 금고 최대 용량 (8시간 기준: autoIncomeValue * 28800)

    public double soldierGradeCost;
    public double soldierGradeValue;
}
[CreateAssetMenu(fileName = "LevelRuleDataSo", menuName = "ScriptableObject/LevelRuleDataSo")]
public class LevelRuleDataSo : ScriptableObject
{
    public List<LevelRuleData> list = new List<LevelRuleData>();
}