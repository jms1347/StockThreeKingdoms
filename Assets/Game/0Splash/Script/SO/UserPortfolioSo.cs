using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유저 천하 투자 스냅샷(성별 병력·평단) + 총 금화 캐시. <see cref="GameManager.currentUser.gold"/>와 동기화.
/// </summary>
[CreateAssetMenu(fileName = "UserPortfolioSo", menuName = "ScriptableObject/Live/UserPortfolioSo")]
public class UserPortfolioSo : ScriptableObject
{
    /// <summary>성별 유저 보유(천하 탭·<see cref="holdings"/> 한 줄).</summary>
    [Serializable]
    public class UserCastleStock
    {
        public string castleId;
        public int troopCount;
        public float averagePurchasePrice;
    }

    public long totalGold;
    public List<UserCastleStock> holdings = new List<UserCastleStock>();

    [Header("본영·이동 게이지 (라이브)")]
    public string homeCastleId = "";
    public float travelGaugePoints;
    /// <summary>만보기 동기화 기준 누적 걸음(델타 계산용).</summary>
    public int currentStepCount;
}
