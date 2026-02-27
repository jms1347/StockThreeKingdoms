using UnityEngine;

public class CapitalManager : Singleton<CapitalManager>
{
    [Header("보유 자본 (재화)")]
    public double currentGold = 0;
    public double currentGrain = 0;

    [Header("수거 대기중인 자본 (창고)")]
    public double accumulatedGold = 0;

    [Header("내정 인프라 레벨")]
    public int clickPowerLevel = 1;
    public int autoIncomeLevel = 1;  // 🔥 Market -> AutoIncome (자동 수익)으로 변경!
    public int soldierGradeLevel = 1;

    protected override void Awake()
    {
        base.Awake();
    }

    void Update()
    {
        // 자동 수익 레벨에 따른 금화가 창고(accumulatedGold)에 쌓입니다.
        accumulatedGold += GetAutoGoldPerSecond() * Time.deltaTime;
    }

    public void AddGold(double amount)
    {
        currentGold += amount;
    }

    // 유저가 '수거' 버튼을 누르면 창고의 돈을 지갑으로 옮기는 함수
    public void ClaimAccumulatedGold()
    {
        if (accumulatedGold > 0)
        {
            AddGold(accumulatedGold);
            accumulatedGold = 0;
        }
    }

    // 수익 계산식 
    public double GetClickPowerGold() { return 10 + (clickPowerLevel * 5); }
    // 🔥 계산식 변수도 autoIncomeLevel로 변경
    public double GetAutoGoldPerSecond() { return (autoIncomeLevel - 1) * 2.5; }
}