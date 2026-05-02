using System;
using System.Collections.Generic;

[Serializable]
public class UserData
{
    // [기본 정보]
    public string userName;          // 유저 이름 [cite: 1]
    public int honorPoints;         // 명예 스탯 [cite: 21, 96]
    public string rankTitle;        // 작위 (평민, 현령 등) [cite: 21, 112]
    /// <summary>작위 식별자(뱃지 스타일용). 비어 있으면 rankTitle 문자열로 티어를 추정합니다.</summary>
    public string rankTitleId;
    /// <summary>장착 중인 캐릭터 ID. <c>Resources/UserPortraits/{id}</c> 스프라이트 로드.</summary>
    public string equippedCharacterId;
    /// <summary>행군 포인트(MP). 상단 자원 행에 표시.</summary>
    public int marchPoints;

    // [핵심 자원]
    public double gold;             // 보유 금화 (음수 = 부채)
    public long soldierCount;       // 레거시/폴백 병력 수 (천하 미로드 시)

    // [성장 레벨]
    public int laborLevel;          // 노동력 레벨 (클릭당 금화) [cite: 12, 149]
    public int marketLevel;         // 시장 레벨 (자동 수익) [cite: 12, 151]
    public int warehouseLevel;      // 창고 레벨 (시장 창고 최대 저장량)
    public int farmLevel;           // 병참 레벨 (일일 병사 유지비 할인). JSON 호환용 필드명 유지.
    public int soldierGradeLevel;   // 병사 등급 레벨 (투자 효율) [cite: 12]

    // [창고] Timestamp 기반 - 마지막 수거 시점 (UTC Unix 초, 정수)
    public long lastMarketCollectTime;
    public long lastFarmCollectTime;

    /// <summary>본영 시장 주머니: 마지막 성벽 수거 이후 누적 시간(초). 최대 28,800(8시간). HUD 금화와 별도.</summary>
    public float homeMarketAccumulatedSec;

    // [M2E 데이터]
    public int dailyStepCount;      // 레거시 호환 (구 세이브)
    public int stepsToday;          // 오늘 걸음 수 (만보기 UI/보상)
    public bool[] stepRewardsClaimed = new bool[4]; // 2k/5k/7k/10k 보급 수령 여부
    public float walkCurrency;      // 만보기 재화 [cite: 130]

    /// <summary>OS 누적 걸음(앱/기기 기준)에서 오늘분을 빼기 위한 기준값. PedometerManager가 관리.</summary>
    public int baselineSteps;
    /// <summary>만보기 일자 추적용 로컬 달력 키 (yyyy-MM-dd).</summary>
    public string stepCalendarDate;
    /// <summary>baselineSteps가 현재 일자에 대해 유효하게 설정되었는지.</summary>
    public bool pedometerBaselineInitialized;

    // [생성자: 초기값 설정]
    public UserData()
    {
        userName = "초보 군주";
        honorPoints = 0;
        rankTitle = "평민";
        rankTitleId = "";
        equippedCharacterId = "";
        marchPoints = 0;
        gold = 1000d;
        soldierCount = 0;
        laborLevel = 1;
        marketLevel = 0;
        warehouseLevel = 0;
        farmLevel = 0;
        soldierGradeLevel = 1;
        dailyStepCount = 0;
        stepsToday = 0;
        stepRewardsClaimed = new bool[4];
        lastMarketCollectTime = 0;
        lastFarmCollectTime = 0;
        homeMarketAccumulatedSec = 0f;
    }
}