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
    public float currentSentiment = 100f; // 0~100 권장
    public int currentPopulation;

    // 점령/인사 상태
    public Faction currentLord;
    public string currentGovernorId;

    // 상태/히스토리
    public bool isWar;
    /// <summary>재해·특수 이벤트 등 (리스트 정렬 상단용 플래그).</summary>
    public bool isDisaster;
    public List<float> sentimentHistory = new List<float>(); // 최근 7개

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

