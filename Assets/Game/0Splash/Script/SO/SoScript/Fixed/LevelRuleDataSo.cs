using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelRuleData
{
    public int level;

    public double laborCost;
    public double laborValue;

    public double marketCost;
    public double marketValuePerSec;

    public double warehouseCost;
    public double warehouseMaxCapacity;

    public double logisticsCost;
    /// <summary>병사 유지비 할인율(%). 최종 유지비 = 기본 × (1 - 값/100).</summary>
    public double logisticsDiscountRate;
}
[CreateAssetMenu(fileName = "LevelRuleDataSo", menuName = "ScriptableObject/LevelRuleDataSo")]
public class LevelRuleDataSo : ScriptableObject
{
    public List<LevelRuleData> list = new List<LevelRuleData>();
}