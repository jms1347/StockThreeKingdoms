using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StringData
{
    public string key;
    public string kor;
    public string eng;
}

[CreateAssetMenu(fileName = "StringDataSo", menuName = "ScriptableObject/StringDataSo")]
public class StringDataSo : ScriptableObject
{
    public List<StringData> stringDataList = new List<StringData>();

}
