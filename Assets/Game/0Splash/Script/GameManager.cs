using System;
using UnityEngine;
using System.IO;

[Serializable]
public class BalanceConfig
{
    [Header("노동력 (클릭)")]
    public double clickPowerBaseCost = 50;
    public double clickPowerCostMult = 1.15;
    public double clickPowerBaseValue = 10;
    public double clickPowerValuePerLevel = 5;

    [Header("시장 (자동수익)")]
    public double autoIncomeBaseCost = 100;
    public double autoIncomeCostMult = 1.2;
    public double autoIncomeBaseValue = 1;
    public double autoIncomeValuePerLevel = 1;
    public double vaultHours = 8;  // 금고 최대 = 초당수익 * (이 시간)

    [Header("병사등급")]
    public double soldierGradeBaseCost = 200;
    public double soldierGradeCostMult = 1.25;
    public double soldierGradeBaseMult = 1;   // 배율 시작값
    public double soldierGradeMultPerLevel = 0.1;
}

public class GameManager : Singleton<GameManager>
{
    [Header("밸런스 (유저 레벨 기반 계산)")]
    public BalanceConfig balance = new BalanceConfig();

    [Header("유저 데이터")]
    public UserData currentUser;

    public Action<long> OnGoldChanged;
    public Action<long> OnGrainChanged;
    /// <summary>만보기 stepsToday 갱신 시 (PedometerManager 등)</summary>
    public Action<int> OnStepsChanged;

    private string savePath;

    protected override void Awake()
    {
        TimeManager.EnsureCreated();
        base.Awake();  // Singleton: _instance 설정 + DontDestroyOnLoad (씬 전환 시 유지)
        savePath = Path.Combine(Application.persistentDataPath, "userData.json");
        LoadUserData();
    }

    // ---- 밸런스 계산 (레벨 → 비용/효과) ----
    public double GetClickPowerCost(int level) => balance.clickPowerBaseCost * Math.Pow(balance.clickPowerCostMult, level - 1);
    public double GetClickPowerValue(int level) => balance.clickPowerBaseValue + balance.clickPowerValuePerLevel * level;
    public double GetAutoIncomeCost(int level) => balance.autoIncomeBaseCost * Math.Pow(balance.autoIncomeCostMult, level);
    public double GetAutoIncomeValue(int level) => level <= 0 ? 0 : balance.autoIncomeBaseValue + balance.autoIncomeValuePerLevel * level;
    public double GetSoldierGradeCost(int level) => balance.soldierGradeBaseCost * Math.Pow(balance.soldierGradeCostMult, level - 1);
    public double GetSoldierGradeMultiplier(int level) => balance.soldierGradeBaseMult + balance.soldierGradeMultPerLevel * level;

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveUserData();
    }

    void OnApplicationQuit()
    {
        SaveUserData();
    }

    // ---- 글로벌 재화 (은행장) ----

    public long currentGold
    {
        get => currentUser != null ? currentUser.gold : 0;
        set
        {
            if (currentUser == null) return;
            currentUser.gold = Math.Max(0L, value);
            OnGoldChanged?.Invoke(currentUser.gold);
        }
    }

    public long currentGrain
    {
        get => currentUser != null ? currentUser.grain : 0;
        set
        {
            if (currentUser == null) return;
            currentUser.grain = Math.Max(0L, value);
            OnGrainChanged?.Invoke(currentUser.grain);
        }
    }

    /// <summary> 금화 추가 (수거 등) </summary>
    public void AddGold(long amount)
    {
        if (currentUser == null)
        {
            LoadUserData();
            if (currentUser == null) return;
        }
        currentGold += amount;
    }
    public void AddGold(double amount) => AddGold((long)amount);

    /// <summary> 금화 차감. 성공 시 true </summary>
    public bool UseGold(long amount)
    {
        if (currentUser == null || currentUser.gold < amount) return false;
        currentGold -= amount;
        return true;
    }

    /// <summary> 식량 추가 (수거 등) </summary>
    public void AddGrain(long amount) => currentGrain += amount;

    public int clickPowerLevel { get => currentUser?.laborLevel ?? 1; set { if (currentUser != null) currentUser.laborLevel = value; } }
    public int autoIncomeLevel { get => currentUser?.marketLevel ?? 0; set { if (currentUser != null) currentUser.marketLevel = value; } }
    public int soldierGradeLevel { get => currentUser?.soldierGradeLevel ?? 1; set { if (currentUser != null) currentUser.soldierGradeLevel = value; } }

    // ---- 저장/로드 ----

    public void SaveUserData()
    {
        if (currentUser == null) return;
        currentUser.dailyStepCount = currentUser.stepsToday;
        string json = JsonUtility.ToJson(currentUser, true);
        File.WriteAllText(savePath, json);
        Debug.Log("데이터 저장 완료: " + savePath);
    }

    public void LoadUserData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentUser = JsonUtility.FromJson<UserData>(json);
        }
        else
        {
            currentUser = new UserData();
        }

        if (currentUser.stepRewardsClaimed == null || currentUser.stepRewardsClaimed.Length != 4)
            currentUser.stepRewardsClaimed = new bool[4];

        if (currentUser.stepsToday <= 0 && currentUser.dailyStepCount > 0)
            currentUser.stepsToday = currentUser.dailyStepCount;

        long now = TimeManager.GetUnixNow();
        if (currentUser.lastMarketCollectTime <= 0)
            currentUser.lastMarketCollectTime = now;
        if (currentUser.lastFarmCollectTime <= 0)
            currentUser.lastFarmCollectTime = now;
    }
}
