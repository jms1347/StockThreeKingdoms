using System.Collections.Generic;
using UnityEngine;
// 1. 성 마스터 데이터 (구글 시트 연동용 고정 데이터)
public enum Grade
{
    SS = 0,
    S = 1,
    A = 2,
    B = 3,
    C = 4,
    D = 5
}
[System.Serializable]
public class CastleMasterData
{
    // 필드(데이터 조각)들은 소문자로 시작 (camelCase)
    public string id;
    public string region;
    public string name;
    public Grade grade;
    public float baseValue;
    public int maxGarrison;
    public int initPopulation;
    public Faction initialLord;
}

[CreateAssetMenu(fileName = "CastleMasterDataSo", menuName = "ScriptableObject/CastleMasterDataSo")]
public class CastleMasterDataSo : ScriptableObject
{
    public List<CastleMasterData> list = new List<CastleMasterData>();

}
