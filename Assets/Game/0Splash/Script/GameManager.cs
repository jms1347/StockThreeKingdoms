using System;
using UnityEngine;
using System.IO;

public class GameManager : Singleton<GameManager>
{
    [Header("유저 데이터")]
    public UserData currentUser;

    [Header("수거 대기 자본 (창고)")]
    public double accumulatedGold = 0;

    public Action<double> OnGoldChanged;

    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "userData.json");
        LoadUserData();
    }

    void Update()
    {
        if (!DataManager.Instance.IsReady || currentUser == null) return;
        var data = DataManager.Instance.GetLevelData(currentUser.marketLevel);
        double perSec = data != null ? data.autoIncomeValue : 0;
        if (perSec > 0)
            accumulatedGold += perSec * Time.deltaTime;
    }

    private void OnApplicationQuit()
    {
        SaveUserData();
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
        if (accumulatedGold > 0)
        {
            AddGold(accumulatedGold);
            accumulatedGold = 0;
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
    }
}
