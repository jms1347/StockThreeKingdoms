using UnityEngine;

/// <summary>
/// Home 탭 Logic Controller.
/// UI를 직접 조작하지 않고, 버튼 클릭 시 수치 연산과 조건 검사만 담당.
/// </summary>
public class HomeController : MonoBehaviour
{
    private HomeUserData _data;

    public HomeUserData Data => _data;

    void Awake()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentUser != null)
            _data = new HomeUserData(GameManager.Instance.currentUser);
        else
            _data = new HomeUserData(new UserData());
    }

    void Start()
    {
        _data?.NotifyAll();
    }

    /// <summary> 대문 터치: Gold에 GoldPerClick만큼 증가 </summary>
    public void OnGateClick()
    {
        if (_data == null) return;
        _data.Gold += (long)_data.GoldPerClick;
    }

    /// <summary> 노동력 업그레이드. Gold >= UpgradeCost 확인 후 차감 & 레벨+1 </summary>
    public void UpgradeLabor()
    {
        if (_data == null) return;
        double cost = HomeUserData.UpgradeCost(HomeUserData.LaborBaseCost, _data.LaborLevel);
        if (_data.Gold >= cost)
        {
            _data.Gold -= (long)cost;
            _data.LaborLevel++;
        }
    }

    /// <summary> 시장 업그레이드 </summary>
    public void UpgradeMarket()
    {
        if (_data == null) return;
        double cost = HomeUserData.UpgradeCost(HomeUserData.MarketBaseCost, _data.MarketLevel);
        if (_data.Gold >= cost)
        {
            _data.Gold -= (long)cost;
            _data.MarketLevel++;
        }
    }

    /// <summary> 농장 업그레이드 </summary>
    public void UpgradeFarm()
    {
        if (_data == null) return;
        double cost = HomeUserData.UpgradeCost(HomeUserData.FarmBaseCost, _data.FarmLevel);
        if (_data.Gold >= cost)
        {
            _data.Gold -= (long)cost;
            _data.FarmLevel++;
        }
    }

    /// <summary> 병사 고용: 100 Gold -> 1 Soldier. 현재 Gold 기반 최대 수량만큼 적용. </summary>
    public void HireSoldiers(int count)
    {
        if (_data == null || count <= 0) return;
        int maxAfford = (int)(_data.Gold / HomeUserData.SoldierCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0)
        {
            _data.Gold -= (long)(actual * HomeUserData.SoldierCost);
            _data.Soldiers += actual;
        }
    }

    /// <summary> 식량 구매: 2 Gold -> 1 Grain. 현재 Gold 기반 최대 수량만큼 적용. </summary>
    public void BuyGrain(int count)
    {
        if (_data == null || count <= 0) return;
        int maxAfford = (int)(_data.Gold / HomeUserData.GrainCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0)
        {
            _data.Gold -= (long)(actual * HomeUserData.GrainCost);
            _data.Grain += actual;
        }
    }

    /// <summary> 현재 Gold로 병사 최대 고용 가능 수 </summary>
    public int GetMaxAffordableSoldiers()
    {
        return _data != null ? (int)(_data.Gold / HomeUserData.SoldierCost) : 0;
    }

    /// <summary> 현재 Gold로 식량 최대 구매 가능 수 </summary>
    public int GetMaxAffordableGrain()
    {
        return _data != null ? (int)(_data.Gold / HomeUserData.GrainCost) : 0;
    }
}
