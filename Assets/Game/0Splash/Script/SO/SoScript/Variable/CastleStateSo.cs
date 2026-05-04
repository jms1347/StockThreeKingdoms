using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임 <see cref="CastleStateData"/> 미러(천하 탭·에디터 점검용). 플레이 중 갱신 시 에디터에서는 SetDirty + 디바운스된 SaveAssets.
/// </summary>
[CreateAssetMenu(fileName = "CastleStateSo", menuName = "ScriptableObject/Live/CastleStateSo")]
public class CastleStateSo : ScriptableObject
{
    [Serializable]
    public class CastleLiveStateEntry
    {
        public string castleId;
        public int currentPopulation;
        public float currentSentiment;
        public bool isWar;
        public bool isDisaster;
        public bool isFavorableEvent;
        public string currentGovernorId;
        public Faction currentLord;
        public float currentBuyPrice;
        public float castleTaxRatePercent;
        public int userDeployedTroops;
        public float averagePurchasePrice;
        public int maxGarrison;
        public int currentAiGarrison;
        public long goldReserve;
        public double constantK;
        public long accumulatedDividendPool;
        public List<float> historyPopulation7Day = new List<float>();
        public List<float> historySentiment7Day = new List<float>();
        public List<float> historyPrice7Day = new List<float>();
        public float buyPricePrevDayClose;
        public float recruitmentFee;
    }

    public List<CastleLiveStateEntry> castles = new List<CastleLiveStateEntry>();
}
