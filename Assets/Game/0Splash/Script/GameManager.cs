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

    [Header("수거 대기 자본 (금고)")]
    public double accumulatedGold = 0;

    public Action<double> OnGoldChanged;
    public Action<double, double> OnVaultChanged;

    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "userData.json");
        LoadUserData();
        accumulatedGold = currentUser?.accumulatedGold ?? 0;
        RefreshAccumulatedGold();
    }

    // ---- 밸런스 계산 (레벨 → 비용/효과) ----
    public double GetClickPowerCost(int level) => balance.clickPowerBaseCost * Math.Pow(balance.clickPowerCostMult, level - 1);
    public double GetClickPowerValue(int level) => balance.clickPowerBaseValue + balance.clickPowerValuePerLevel * level;
    public double GetAutoIncomeCost(int level) => balance.autoIncomeBaseCost * Math.Pow(balance.autoIncomeCostMult, level);
    public double GetAutoIncomeValue(int level) => level <= 0 ? 0 : balance.autoIncomeBaseValue + balance.autoIncomeValuePerLevel * level;
    public double GetMaxStorageCapacity(int level) => GetAutoIncomeValue(level) * (balance.vaultHours * 3600);
    public double GetSoldierGradeCost(int level) => balance.soldierGradeBaseCost * Math.Pow(balance.soldierGradeCostMult, level - 1);
    public double GetSoldierGradeMultiplier(int level) => balance.soldierGradeBaseMult + balance.soldierGradeMultPerLevel * level;

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) RefreshAccumulatedGold();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            RefreshAccumulatedGold();
            if (currentUser != null) currentUser.accumulatedGold = accumulatedGold;
            SaveUserData();
        }
        else
        {
            accumulatedGold = currentUser?.accumulatedGold ?? 0;
            RefreshAccumulatedGold();
        }
    }

    void OnApplicationQuit()
    {
        RefreshAccumulatedGold();
        if (currentUser != null) currentUser.accumulatedGold = accumulatedGold;
        SaveUserData();
    }

    // ---- 타임스탬프 방식 AutoIncome ----
    // Accumulated = min((CurrentTime - LastCollectTime) * IncomePerSec, MaxCapacity)
    static double GetUnixTime()
    {
        return DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
    }

    public void RefreshAccumulatedGold()
    {
        if (currentUser == null) return;

        int lv = currentUser.marketLevel;
        double perSec = GetAutoIncomeValue(lv);
        if (perSec <= 0)
        {
            currentUser.lastCollectTime = GetUnixTime();
            return;
        }

        double now = GetUnixTime();
        double elapsed = now - currentUser.lastCollectTime;
        double income = perSec * elapsed;
        double cap = GetMaxStorageCapacity(lv);

        accumulatedGold = Math.Min(accumulatedGold + income, cap);
        currentUser.lastCollectTime = now;
        OnVaultChanged?.Invoke(accumulatedGold, cap);
    }

    /// <summary> 화면 표시용 - Text만 Time에 맞춰 연산. 성능 부하 거의 없음. </summary>
    public double GetDisplayAccumulatedGold()
    {
        if (currentUser == null) return accumulatedGold;

        double perSec = GetAutoIncomeValue(currentUser.marketLevel);
        if (perSec <= 0) return accumulatedGold;

        double now = GetUnixTime();
        double elapsed = now - currentUser.lastCollectTime;
        double cap = GetMaxStorageCapacity(currentUser.marketLevel);
        return Math.Min(accumulatedGold + perSec * elapsed, cap);
    }

    public double GetMaxStorageCapacity()
    {
        if (currentUser == null) return 0;
        return GetMaxStorageCapacity(currentUser.marketLevel);
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

    public void ClaimAccumulatedGold()
    {
        RefreshAccumulatedGold();
        if (accumulatedGold > 0)
        {
            AddGold(accumulatedGold);
            accumulatedGold = 0;
            currentUser.lastCollectTime = GetUnixTime();
            OnVaultChanged?.Invoke(0, GetMaxStorageCapacity());
        }
    }

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
