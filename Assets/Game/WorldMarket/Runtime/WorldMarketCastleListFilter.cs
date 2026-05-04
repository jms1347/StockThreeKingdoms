/// <summary>
/// 천하 탭 성 카드 리스트 필터(상단 탭). <see cref="WorldMarketCastleVirtualList.SetFilter"/>, <see cref="WorldMarketFilterTabBar"/>.
/// 목록 순서는 필터 적용 후 <see cref="DataManager.GetOrderedWorldCastleIds"/> — 본영 고정 → 거리순.
/// </summary>
public enum WorldMarketCastleListFilter
{
    /// <summary>전체</summary>
    All = 0,
    /// <summary>보유 — 병력 투입 성만</summary>
    MyHoldings = 1,
    /// <summary>전쟁 중</summary>
    War = 2,
    /// <summary>이벤트 — 재해·호재</summary>
    Event = 3,
    /// <summary>우량 — SS/S/A</summary>
    Premium = 4,
    /// <summary>요주의 — B·C·D 등급 성만 (스펙/저평가)</summary>
    Attention = 5,
}
