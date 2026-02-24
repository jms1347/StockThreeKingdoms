using System.Collections.Generic;
using UnityEngine;

public enum ResourceType
{
    None = 0,
    Image,
    Prefab,
    Sound,
    Data
}

[System.Serializable]
public class ResourceData
{
    public string key;
    public ResourceType type;
    [TextArea] public string description;
}

[CreateAssetMenu(fileName = "ResourceDataSo", menuName = "ScriptableObject/ResourceDataSo")]
public class ResourceDataSo : ScriptableObject
{
    // 리스트이므로 인스펙터에서 바로 보이고 수정 가능합니다.
    public List<ResourceData> resourceDataList = new List<ResourceData>();
}