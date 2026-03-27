using System;
using System.Collections.Generic;
using System.Linq;
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

[Serializable]
public class CastleMasterData
{
    [Header("기본 정보")]
    public string id;                // 성 고유 ID (C01, C02...)
    public string name;              // 성 이름 (낙양, 허창...)
    public string regionId;          // 지역 코드 (R01~R12)
    public Grade grade;              // 성 등급 (SS, S, A, B, C, D)
    public string initialNationId;   // 초기 점령 국가 (WEI, SHU, WU...)

    [Header("경제/군사 스탯")]
    public float baseValue;          // 초기 액면가
    public int maxTroops;            // 최대 군대 수용량
    public int initPopulation;       // 초기 백성 수

    [Header("지도 및 연결 데이터")]
    public float posX;               // 지도상의 X 좌표 (0 ~ 1000)
    public float posY;               // 지도상의 Y 좌표 (0 ~ 1000)

    [TextArea(2, 5)]
    public string adjacentIdsRaw;    // 인접 성 ID 리스트 (쉼표 구분: "C02,C05,C10")

    // --- Helper Properties ---

    /// <summary>
    /// 쉼표로 구분된 인접성 데이터를 리스트로 변환하여 반환합니다.
    /// </summary>
    public List<string> GetAdjacentIds()
    {
        if (string.IsNullOrWhiteSpace(adjacentIdsRaw)) return new List<string>();
        return adjacentIdsRaw.Split(',')
                             .Select(x => x.Trim())
                             .Where(x => !string.IsNullOrEmpty(x))
                             .ToList();
    }

    /// <summary>
    /// 계산 편의를 위해 좌표를 Vector2로 반환합니다.
    /// </summary>
    public Vector2 GetPosition() => new Vector2(posX, posY);

    /// <summary><see cref="CastleStateData.currentLord"/> 초기화용. <see cref="initialNationId"/> → WEI/SHU 등.</summary>
    public Faction GetInitialLordFaction()
    {
        if (string.IsNullOrWhiteSpace(initialNationId)) return Faction.NONE;
        string raw = initialNationId.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(Faction), n))
            return (Faction)n;
        if (Enum.TryParse(raw, true, out Faction f))
            return f;
        return Faction.NONE;
    }
}

[CreateAssetMenu(fileName = "CastleMasterDataSo", menuName = "ScriptableObject/CastleMasterDataSo")]
public class CastleMasterDataSo : ScriptableObject
{
    public List<CastleMasterData> list = new List<CastleMasterData>();

}
