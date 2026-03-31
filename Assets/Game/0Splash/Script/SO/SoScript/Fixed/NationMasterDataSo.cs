using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NationMasterData
{
    public string id;          // WEI, SHU, WU, OTHERS, NONE
    public string name;        // 위, 촉, 오, 군웅, 공백
    public string colorCode;   // UI 및 차트에 사용할 헥사코드 (예: #338CFF)
    public string capitalId;   // 초기 수도 성 ID (예: C01 - 낙양)
    public string description; // 세력 설명
}

[CreateAssetMenu(fileName = "NationMasterDataSo", menuName = "ScriptableObject/NationMasterDataSo")]
public class NationMasterDataSo : ScriptableObject
{
    public List<NationMasterData> list = new List<NationMasterData>();
}