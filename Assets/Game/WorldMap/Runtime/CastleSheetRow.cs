using System;
using UnityEngine;

/// <summary>구글 시트 한 행에 대응하는 성 정적 정의 + 맵 배치.</summary>
[Serializable]
public class CastleSheetRow
{
    public int castleId;
    public string castleName;
    public CountryId countryId;
    public string governorName;
    /// <summary>태수 장수 <see cref="GeneralMasterData.id"/>. 시트 동기화 시 채움.</summary>
    public string governorGeneralId;
    public int army;
    public int population;
    [Range(0, 100)] public int publicSentiment;
    public int castleValue;
    public Vector2 mapPosition;

    /// <summary>성 등급(시트 D열). 월드맵 마커 크기에 사용.</summary>
    public Grade grade = Grade.D;

    /// <summary>마스터 성 ID (예: C01). 시트 인접 열과 도로 매칭에 사용.</summary>
    public string masterId;

    /// <summary>인접 성 ID, 쉼표 구분. <see cref="CastleMasterData.adjacentIdsRaw"/>와 동일.</summary>
    [TextArea(1, 4)]
    public string adjacentIdsRaw;
}
