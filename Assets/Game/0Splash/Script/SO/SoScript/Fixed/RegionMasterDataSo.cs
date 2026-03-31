using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>지역(섹터) 마스터 — 시트: 지역코드, 섹터명, 특징(투자 테마), 배정 성 예시(이름(Cxx) 나열).</summary>
[Serializable]
public class RegionMasterData
{
    public string id; // R01, R02 …
    public string sectorName;
    public string features;
    public List<string> castleIds = new List<string>();
}

[CreateAssetMenu(fileName = "RegionMasterDataSo", menuName = "ScriptableObject/RegionMasterDataSo")]
public class RegionMasterDataSo : ScriptableObject
{
    public List<RegionMasterData> list = new List<RegionMasterData>();
}
