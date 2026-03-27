using UnityEngine;

/// <summary>
/// 레거시/버튼 OnClick용 — <see cref="WorldMarketFilterTabBar"/> 사용을 권장합니다.
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketCastleFilterChips : MonoBehaviour
{
    [Tooltip("비우면 부모에서 WorldMarketCastleVirtualList를 찾습니다.")]
    [SerializeField] WorldMarketCastleVirtualList castleList;

    public void SelectAll() => Apply(WorldMarketCastleListFilter.All);

    public void SelectMyHoldings() => Apply(WorldMarketCastleListFilter.MyHoldings);

    public void SelectWar() => Apply(WorldMarketCastleListFilter.War);

    public void SelectEvent() => Apply(WorldMarketCastleListFilter.Event);

    public void SelectPremium() => Apply(WorldMarketCastleListFilter.Premium);

    public void SelectAttention() => Apply(WorldMarketCastleListFilter.Attention);

    void Apply(WorldMarketCastleListFilter f)
    {
        if (castleList == null)
            castleList = GetComponentInParent<WorldMarketCastleVirtualList>();
        castleList?.SetFilter(f);
    }
}
