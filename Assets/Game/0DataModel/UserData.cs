using System;
using System.Collections.Generic;

[Serializable]
public class UserData
{
    // [기본 정보]
    public string userName;          // 유저 이름 [cite: 1]
    public int honorPoints;         // 명예 스탯 [cite: 21, 96]
    public string rankTitle;        // 작위 (평민, 현령 등) [cite: 21, 112]

    // [핵심 자원]
    public long gold;               // 보유 금화 [cite: 3, 44]
    public long grain;              // 보유 식량 [cite: 7, 47]
    public long soldierCount;       // 보유 병사 수 [cite: 6, 45]

    // [성장 레벨]
    public int laborLevel;          // 노동력 레벨 (클릭당 금화) [cite: 12, 149]
    public int marketLevel;         // 시장 레벨 (자동 수익) [cite: 12, 151]
    public int farmLevel;           // 농장 레벨 (식량 자동 생성용) [cite: 13, 41]
    public int soldierGradeLevel;   // 병사 등급 레벨 (투자 효율) [cite: 12]

    // [금고] AutoIncome 오프라인 적립용
    public double lastCollectTime;   // 마지막 수령/계산 시점 (Unix 초)
    public double accumulatedGold;   // 금고에 쌓인 미수령 금화

    // [M2E 데이터]
    public int dailyStepCount;      // 오늘 걸음 수 [cite: 13, 180]
    public float walkCurrency;      // 만보기 재화 [cite: 130]

    // [생성자: 초기값 설정]
    public UserData()
    {
        userName = "초보 군주";
        honorPoints = 0;
        rankTitle = "평민";
        gold = 1000;      // 초기 정착 자금 [cite: 4]
        grain = 500;
        soldierCount = 0;
        laborLevel = 1;
        marketLevel = 0;
        farmLevel = 0;
        soldierGradeLevel = 1;
        dailyStepCount = 0;
    }
}