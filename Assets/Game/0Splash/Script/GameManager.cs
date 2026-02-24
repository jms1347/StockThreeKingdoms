using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [Header("Economy Data")]
    // 외부에서 읽을 수는 있지만, 수정은 메서드를 통해서만 가능하게 캡슐화
    [SerializeField] private long _money = 0;
    public long Money => _money;

    // 점수/돈이 변경될 때 UI에 알리기 위한 델리게이트 (옵저버 패턴의 기초)
    public System.Action<long> OnMoneyChanged;

    private void Start()
    {
        // 게임 시작 시 초기화 (저장된 데이터 로드 등은 여기서 호출)
        UpdateMoneyUI();
    }

    /// <summary>
    /// 돈을 추가합니다.
    /// </summary>
    public void AddMoney(long amount)
    {
        _money += amount;
        UpdateMoneyUI();
    }

    /// <summary>
    /// 돈을 소비합니다. 잔액이 부족하면 false를 반환합니다.
    /// </summary>
    public bool SpendMoney(long amount)
    {
        if (_money >= amount)
        {
            _money -= amount;
            UpdateMoneyUI();
            // 여기서 Save()를 호출하는 것이 안전합니다.
            return true;
        }
        return false;
    }

    private void UpdateMoneyUI()
    {
        // 이벤트가 연결되어 있다면 호출
        OnMoneyChanged?.Invoke(_money);
    }

    // 추후 구현할 데이터 저장용 메서드 예시
    /*
    public void SaveData() { ... }
    public void LoadData() { ... }
    */
}