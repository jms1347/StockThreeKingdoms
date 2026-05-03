/// <summary>
/// 주간 배당 동기화 진입점. Sync 시점 호출을 한 곳으로 고정해 정산 규칙 교체를 쉽게 합니다.
/// </summary>
public static class SettlementManager
{
    public static void Tick(DataManager dm, float unscaledTime)
    {
        DividendManager.Tick(dm, unscaledTime);
        dm?.TickGradeSpeculation(unscaledTime);
        dm?.TickAiCastleStrategy(unscaledTime);
    }

    /// <summary>요구 스펙: Sync 시점 지분 배당 지급 처리.</summary>
    public static void TrySyncWeeklyDividendPayout(DataManager dm)
    {
        DividendManager.TryProcessWeeklyDividend(dm);
    }
}
