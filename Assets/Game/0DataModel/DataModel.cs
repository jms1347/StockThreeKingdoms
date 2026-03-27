using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataModel : MonoBehaviour
{

}



/// <summary>월드/저장용 장수 배치 — <see cref="GeneralMasterData.initialNationId"/>, <see cref="GeneralMasterData.initialCastleId"/>와 동일 의미.</summary>
[Serializable]
public class GeneralPlacementRecord
{
    public string generalMasterId;
    public string initialNationId;
    public string initialCastleId;
}

[Serializable]
public class UserInvestment {
    /// <summary><see cref="CastleMasterData.id"/> (예: C01)</summary>
    public string castleId;
    /// <summary>해당 성에 임명된 장수 <see cref="GeneralMasterData.id"/> (없으면 빈 문자열).</summary>
    public string governorGeneralId;
    public long investedSoldiers;  // 투입된 병사 수 (자본) [cite: 6, 15]
    public int soldierRank;        // 투입된 병사의 등급 [cite: 12, 18]
    public double sharePercentage; // 해당 성에서의 지분율 [cite: 8, 15]
    public long lastDividendStep;  // 마지막 배당 수령 시점
}

public static class GlobalEconomy {
    public static long totalServerSoldiers; // 서버 전체 병사 총합 [cite: 7, 160]
    public static float grainPriceIndex;    // 실시간 식량 가격 지수 (인플레이션 반영) [cite: 7, 49, 160]
    public const float Threshold = 1000000f; // 인플레이션 임계점 [cite: 50, 161]
}