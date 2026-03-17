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
    public int laborLevel;          // 노동력 레벨 [cite: 12, 149]
    public int marketLevel;         // 시장 레벨 [cite: 12, 151]
    public int farmLevel;           // 농장 레벨 (식량 자동 생성용) [cite: 13, 41]

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
        dailyStepCount = 0;
    }
}