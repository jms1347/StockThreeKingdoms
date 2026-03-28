using System;
using System.Collections.Generic;

public enum Faction
{
    NONE = 0,
    WEI = 1,
    SHU = 2,
    WU = 3,
    OTHERS = 4
}

/// <summary>CastleStateData 전수 기준 세력별 성 점유 비율 (합계 1.0 근사).</summary>
[Serializable]
public struct FactionCastleShare
{
    public float wei;
    public float shu;
    public float wu;
    public float others;
}

[Serializable]
public class CastleStateData
{
    public string id; // CastleMasterData.id

    // 실시간 수치
    public float currentSentiment = 100f; // 0~200, 100=기본
    public int currentPopulation;

    // 점령/인사 상태
    public Faction currentLord;
    public string currentGovernorId;
    /// <summary>태수 일일 버프 마지막 적용 시각(Unix 초). <see cref="TimeManager.GetUnixNow"/> 기준.</summary>
    public long lastDailyBuffTime;
    /// <summary>일일 버프 쿨다운이 묶인 태수 id. <see cref="currentGovernorId"/>와 다르면 쿨다운을 새 태수 기준으로 리셋.</summary>
    public string lastDailyBuffGovernorId;

    // 상태/히스토리
    public bool isWar;
    /// <summary>재해·특수 이벤트 등 (리스트 정렬 상단용 플래그).</summary>
    public bool isDisaster;
    /// <summary>호재(풍년 등) — 이벤트 탭 필터용. 데이터·연출 붙이면 갱신.</summary>
    public bool isFavorableEvent;
    public List<float> sentimentHistory = new List<float>(); // 최근 7~10개
    /// <summary>미니 스파크라인용 인구 이력 (최근 7~10개).</summary>
    public List<int> populationHistory = new List<int>();

    /// <summary>7일 일간 스냅샷(인구). 차트 X는 과거→현재.</summary>
    public List<float> historyPopulation7Day = new List<float>();
    /// <summary>7일 일간 스냅샷(민심 0~200, 100 기준).</summary>
    public List<float> historySentiment7Day = new List<float>();
    /// <summary>전일 종가에 가까운 매수가 앵커 — <see cref="DataManager.CalculateChangeRate24h"/>용.</summary>
    public float buyPricePrevDayClose;

    // 매수가/매도가 분리
    public float currentBuyPrice;
    public float currentSellPrice;

    // 유저 투자 (천하 탭)
    public int userDeployedTroops;
    /// <summary>병력 1단위당 평균 매수(진입) 가격. 수익률 = 현재가 대비.</summary>
    public float averagePurchasePrice;

    public bool IsUserInvested => userDeployedTroops > 0;
}

[Serializable]
public class WorldNewsItem
{
    public long unixTime;
    public string text;
}

[Serializable]
public class CastleStateSavePayload
{
    public List<CastleStateData> castles = new List<CastleStateData>();
    public List<WorldNewsItem> news = new List<WorldNewsItem>();
}

