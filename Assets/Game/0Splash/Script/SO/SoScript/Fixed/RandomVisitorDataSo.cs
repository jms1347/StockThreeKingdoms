using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RandomVisitorData
{
    public string id;
    public string visitorType;
    public float probability;
    public string effectReward;
}

[CreateAssetMenu(fileName = "RandomVisitorDataSo", menuName = "ScriptableObject/RandomVisitorDataSo")]
public class RandomVisitorDataSo : ScriptableObject
{
    public List<RandomVisitorData> list = new List<RandomVisitorData>();
}
