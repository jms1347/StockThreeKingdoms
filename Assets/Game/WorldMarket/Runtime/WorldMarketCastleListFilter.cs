/// <summary>
/// 천하 탭 성 카드 리스트 필터. 칩 UI 연결 시 <see cref="WorldMarketCastleVirtualList.SetFilter"/>로 전환.
/// </summary>
public enum WorldMarketCastleListFilter
{
    /// <summary>전체 — 위기 → 내 투자 → 등급 → ID</summary>
    All = 0,
    /// <summary>내 투자 성만 (동일 정렬 규칙 적용)</summary>
    MyInvestments = 1,
    /// <summary>전쟁·재해 성만</summary>
    WarOrDisaster = 2,
    /// <summary>등급(SS→D) 후 ID — 위기/투자 우선순위 없음</summary>
    ByGradeOnly = 3,
}
