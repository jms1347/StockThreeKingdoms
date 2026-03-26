using UnityEngine;

/// <summary>
/// 필터 칩 버튼의 OnClick에 연결해 <see cref="WorldMarketCastleVirtualList.SetFilter"/>를 호출합니다.
/// </summary>
public class WorldMarketCastleFilterChips : MonoBehaviour
{
    [SerializeField] WorldMarketCastleVirtualList castleList;

    public void SelectAll() => Apply(WorldMarketCastleListFilter.All);

    public void SelectMyInvestments() => Apply(WorldMarketCastleListFilter.MyInvestments);

    public void SelectWarOrDisaster() => Apply(WorldMarketCastleListFilter.WarOrDisaster);

    public void SelectByGradeOnly() => Apply(WorldMarketCastleListFilter.ByGradeOnly);

    void Apply(WorldMarketCastleListFilter f)
    {
        if (castleList == null)
            castleList = GetComponentInParent<WorldMarketCastleVirtualList>();
        castleList?.SetFilter(f);
    }
}
