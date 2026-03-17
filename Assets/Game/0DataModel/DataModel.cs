using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataModel : MonoBehaviour
{

}


public class CastleData {
    public int castleId;           // 성 고유 ID (낙양, 장안 등) [cite: 14]
    public string castleName;      // 성 이름 [cite: 14]
    public long population;        // 현재 인구수 (생산량의 기초) [cite: 18]
    public long goldStorage;       // 성 금고 보유 금화 [cite: 9, 18]
    public long grainStorage;      // 성 창고 보유 식량 [cite: 18]
    public float baseYieldRate;    // 기본 수익률 [cite: 14]
    public int governorId;         // 현재 태수(대주주) ID [cite: 10, 18]
}

public class UserInvestment {
    public int castleId;
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