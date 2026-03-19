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
    public double marketMaxCapacity;

    public double farmCost;
    public double farmValuePerSec;
    public double farmMaxCapacity;
}
[CreateAssetMenu(fileName = "LevelRuleDataSo", menuName = "ScriptableObject/LevelRuleDataSo")]
public class LevelRuleDataSo : ScriptableObject
{
    public List<LevelRuleData> list = new List<LevelRuleData>();
}