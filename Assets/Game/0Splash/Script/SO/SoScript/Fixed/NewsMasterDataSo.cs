using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기사 마스터(도서관). <see cref="NewsMasterData.newsCode"/>는 <see cref="EventMasterData.rumorNewsCodes"/>·<see cref="EventMasterData.breakingNewsCodes"/>에서 참조되어
/// 소문·속보 문구로 쓰입니다. 월드 탭 분류는 <see cref="WorldNewsFeedKind"/>, 스펙 분류는 <see cref="NewsType"/>입니다.
/// </summary>
public enum NewsType
{
    None = 0,
    Rumor = 1,
    Breaking = 2,
    FactCheck = 3,
    System = 4
}

/// <summary>
/// 개별 기사 원본(도서관 1행). 런타임은 <c>newsCode</c> 키 딕셔너리(<see cref="FixedSoDataManager.newsMasterDataMap"/>).
/// </summary>
[Serializable]
public class NewsMasterData
{
    public string newsCode;
    public NewsType newsType;
    [TextArea(1, 3)] public string headline;
    [TextArea(2, 8)] public string script;
}

/// <summary><see cref="NewsMasterData"/> 리스트 보관. 런타임 맵은 <see cref="FixedSoDataManager.ApplyParsedNewsMaster"/>로 빌드.</summary>
[CreateAssetMenu(fileName = "NewsMasterDataSo", menuName = "ScriptableObject/NewsMasterDataSo")]
public class NewsMasterDataSo : ScriptableObject
{
    public List<NewsMasterData> list = new List<NewsMasterData>();
}
