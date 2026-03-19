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

    public Action<double> OnGoldChanged;

    /// <summary> 시장 창고 누적량 (현재 / MAX) 변동 시 </summary>
    public Action<double, double> OnAccumulatedMarketChanged;
    /// <summary> 농장 창고 누적량 (현재 / MAX) 변동 시 </summary>
    public Action<double, double> OnAccumulatedFarmChanged;

    private string savePath;

    void Awake()
    {
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
        if (paused)
        {
            if (currentUser != null)
                currentUser.lastCollectTime = GetUnixTime();
            SaveUserData();
        }
    }

    void OnApplicationQuit()
    {
        if (currentUser != null)
            currentUser.lastCollectTime = GetUnixTime();
        SaveUserData();
    }

    // ---- 창고 누적 (시장/농장) ----
    public double GetAccumulatedMarketGold() => currentUser?.accumulatedMarketGold ?? 0;
    public double GetAccumulatedFarmGrain() => currentUser?.accumulatedFarmGrain ?? 0;

    /// <summary> 현재 시장 레벨 기준 창고 MAX치. DataManager 준비 시 시트값, 아니면 formula 사용 </summary>
    public double GetMarketMaxCapacity()
    {
        if (currentUser == null) return 0;
        int lv = currentUser.marketLevel;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(lv);
            if (d != null && d.marketMaxCapacity > 0) return d.marketMaxCapacity;
        }
        return GetAutoIncomeValue(lv) * (balance.vaultHours * 3600);
    }

    /// <summary> 현재 농장 레벨 기준 창고 MAX치 </summary>
    public double GetFarmMaxCapacity()
    {
        if (currentUser == null) return 0;
        int lv = currentUser.farmLevel;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(lv);
            if (d != null && d.farmMaxCapacity > 0) return d.farmMaxCapacity;
        }
        double perSec = GetAutoIncomeValue(lv);
        return perSec > 0 ? perSec * (balance.vaultHours * 3600) : 0;
    }

    public void RaiseAccumulatedMarketChanged()
    {
        OnAccumulatedMarketChanged?.Invoke(GetAccumulatedMarketGold(), GetMarketMaxCapacity());
    }

    public void RaiseAccumulatedFarmChanged()
    {
        OnAccumulatedFarmChanged?.Invoke(GetAccumulatedFarmGrain(), GetFarmMaxCapacity());
    }

    // ---- 자본 ----

    public double currentGold
    {
        get => currentUser != null ? currentUser.gold : 0;
        set
        {
            if (currentUser == null) return;
            currentUser.gold = (long)Math.Max(0, value);
            OnGoldChanged?.Invoke((double)currentUser.gold);
        }
    }

    public double currentGrain
    {
        get => currentUser != null ? currentUser.grain : 0;
        set { if (currentUser != null) currentUser.grain = (long)Math.Max(0, value); }
    }

    public int clickPowerLevel { get => currentUser?.laborLevel ?? 1; set { if (currentUser != null) currentUser.laborLevel = value; } }
    public int autoIncomeLevel { get => currentUser?.marketLevel ?? 0; set { if (currentUser != null) currentUser.marketLevel = value; } }
    public int soldierGradeLevel { get => currentUser?.soldierGradeLevel ?? 1; set { if (currentUser != null) currentUser.soldierGradeLevel = value; } }

    public void AddGold(double amount)
    {
        currentGold += amount;
    }

    static double GetUnixTime() => DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

    // ---- 저장/로드 ----

    public void SaveUserData()
    {
        if (currentUser == null) return;
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
        if (currentUser.lastCollectTime <= 0)
            currentUser.lastCollectTime = GetUnixTime();
    }
}
