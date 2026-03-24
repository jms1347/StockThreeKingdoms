using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GeneralMasterData
{
    public string id;
    public string name;
    public Grade grade;
    public int power;
    public int intel;
    public int charm;
    public string buffId; // S급 이상만 존재, 나머지는 null 또는 empty

}

[CreateAssetMenu(fileName = "GeneralMasterDataSo", menuName = "ScriptableObject/GeneralMasterDataSo")]
public class GeneralMasterDataSo : ScriptableObject
{
    public List<GeneralMasterData> list = new List<GeneralMasterData>();

}
