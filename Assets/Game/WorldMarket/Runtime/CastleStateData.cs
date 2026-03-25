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
    public List<float> sentimentHistory = new List<float>(); // 최근 7개

    // 매수가/매도가 분리
    public float currentBuyPrice;
    public float currentSellPrice;
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

