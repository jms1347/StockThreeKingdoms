using System;
using UnityEngine;
using UnityEngine.Serialization;
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

/// <summary>TimeManager(-200)보다 먼저 Awake되어 <see cref="RealSecondsPerGameDay"/>를 씁니다.</summary>
[DefaultExecutionOrder(-250)]
public class GameManager : Singleton<GameManager>
{
    [Header("시간")]
    [Tooltip("비어 있지 않으면 TimeManager에 등록되어 시뮬레이션 속도·모드의 단일 소스가 됩니다(아래 인스펙터 테스트 토글은 무시).")]
    [SerializeField] TimeConfig timeConfig;
    [Tooltip("체크하면 아래 ‘테스트용 현실 초/게임일’만 사용합니다. 끄면 ‘현실 초/게임일’ 값을 씁니다.")]
    [SerializeField] bool testModeGameDay;
    [Tooltip("테스트 모드일 때만 적용. 기본 60 = 현실 1분에 게임 하루(일 경계·이벤트 일 틱).")]
    [SerializeField] float testRealSecondsPerGameDay = 60f;

    [Tooltip("일반 플레이: 현실 몇 초가 지나면 게임 내 하루(86400초)가 지난 것으로 취급되는지.\n• 86400 = 현실 24시간에 게임 1일\n• 테스트 모드가 켜져 있으면 이 값은 무시됩니다.")]
    [SerializeField, FormerlySerializedAs("minutesPerGameDay")]
    float realSecondsPerGameDay = 86400f;

    /// <summary>게임 하루당 현실 초(최소 0.001). <see cref="TimeConfig"/>가 있으면 그 값과 동기화.</summary>
    public float RealSecondsPerGameDay
    {
        get
        {
            if (timeConfig != null)
                return Mathf.Max(0.001f, timeConfig.ResolveSecondsPerDay());
            float raw = testModeGameDay ? testRealSecondsPerGameDay : realSecondsPerGameDay;
            return Mathf.Max(0.001f, raw);
        }
    }

    [Header("밸런스 (유저 레벨 기반 계산)")]
    public BalanceConfig balance = new BalanceConfig();

    [Header("SO 기본값 (선택)")]
    [Tooltip("비어 있지 않으면 Awake 시 balance에 복사됩니다. 런타임에 SO 에셋은 수정되지 않습니다.")]
    [SerializeField] BalanceConfigSo balanceConfigSo;
    [Tooltip("세이브가 없을 때만 UserData 초기값으로 사용됩니다.")]
    [SerializeField] UserDataDefaultsSo userDataDefaultsSo;

    [Header("유저 데이터")]
    public UserData currentUser;

    public Action<long> OnGoldChanged;
    public Action<long> OnGrainChanged;
    /// <summary>만보기 stepsToday 갱신 시 (PedometerManager 등)</summary>
    public Action<int> OnStepsChanged;

    private string savePath;

    protected override void Awake()
    {
        if (timeConfig != null)
            TimeManager.RegisterTimeConfig(timeConfig);
        TimeManager.EnsureCreated();
        base.Awake();  // Singleton: _instance 설정 + DontDestroyOnLoad (씬 전환 시 유지)
        savePath = Path.Combine(Application.persistentDataPath, "userData.json");
        if (balanceConfigSo != null)
            balance = balanceConfigSo.CreateRuntimeCopy();
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
            if (userDataDefaultsSo != null)
                currentUser = userDataDefaultsSo.CreateRuntimeCopy();
            else
                currentUser = new UserData();
        }

        if (currentUser.stepRewardsClaimed == null || currentUser.stepRewardsClaimed.Length != 4)
            currentUser.stepRewardsClaimed = new bool[4];

        if (currentUser.stepsToday <= 0 && currentUser.dailyStepCount > 0)
            currentUser.stepsToday = currentUser.dailyStepCount;

        long now = TimeManager.GetUnixNow();
        // 생산 중인 창고만 기준 시각이 없을 때 현재 시각으로 초기화 (레벨 0이면 두지 않음 → 경과/주머니 0)
        if (currentUser.marketLevel > 0 && currentUser.lastMarketCollectTime <= 0)
            currentUser.lastMarketCollectTime = now;
        if (currentUser.farmLevel > 0 && currentUser.lastFarmCollectTime <= 0)
            currentUser.lastFarmCollectTime = now;
        if (FixWarehouseTimestampsIfBehindClock(now))
            SaveUserData();
    }

    /// <summary>시장/농장이 가동 중인데 lastCollect가 0이면 지금 시각으로 보정 (구세이브 호환).</summary>
    public void EnsureWarehouseBaselines()
    {
        if (currentUser == null) return;
        long now = TimeManager.GetUnixNow();
        bool dirty = false;
        if (currentUser.marketLevel > 0 && currentUser.lastMarketCollectTime <= 0)
        {
            currentUser.lastMarketCollectTime = now;
            dirty = true;
        }
        if (currentUser.farmLevel > 0 && currentUser.lastFarmCollectTime <= 0)
        {
            currentUser.lastFarmCollectTime = now;
            dirty = true;
        }

        if (FixWarehouseTimestampsIfBehindClock(now))
            dirty = true;

        if (dirty) SaveUserData();
    }

    /// <summary>
    /// 가상 시각이 뒤로 돌아가거나 세이브의 last가 미래인 경우 경과가 0으로 고정되는 문제를 방지합니다.
    /// </summary>
    bool FixWarehouseTimestampsIfBehindClock(long nowUnix)
    {
        bool changed = false;
        if (currentUser.marketLevel > 0 && currentUser.lastMarketCollectTime > nowUnix)
        {
            currentUser.lastMarketCollectTime = nowUnix;
            changed = true;
        }

        if (currentUser.farmLevel > 0 && currentUser.lastFarmCollectTime > nowUnix)
        {
            currentUser.lastFarmCollectTime = nowUnix;
            changed = true;
        }

        return changed;
    }
}
