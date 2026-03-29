using System;
using System.Collections.Generic;
using UnityEngine;

public enum EventScope
{
    Region = 0,
    Castle = 1,
}

[Serializable]
public class EventMasterData
{
    public string id;
    public string name;
    public EventScope scope;
    public int minDays;
    public int maxDays;
    public List<string> buffCodes = new List<string>();
}

[CreateAssetMenu(fileName = "EventMasterDataSo", menuName = "ScriptableObject/EventMasterDataSo")]
public class EventMasterDataSo : ScriptableObject
{
    public List<EventMasterData> list = new List<EventMasterData>();
}
