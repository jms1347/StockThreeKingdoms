using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StepMissionData
{
    public int step;
    public int targetSteps;
    public int mpReward;
    public string remarks;
}

[CreateAssetMenu(fileName = "StepMissionDataSo", menuName = "ScriptableObject/StepMissionDataSo")]
public class StepMissionDataSo : ScriptableObject
{
    public List<StepMissionData> list = new List<StepMissionData>();
}
