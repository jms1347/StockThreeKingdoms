using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 본영 탭 경제 코어. 시장 수익은 HUD가 아니라 주머니에만 쌓이고, 성벽 수거 시에만 HUD 금화로 합산된다.
/// </summary>
public class HomeController : MonoBehaviour
{
    public const float MaxMarketAccumulatedSec = 28800f; // 8h

    /// <summary>시장 레벨당 초당 주머니 축적 금화 계수 (기획식).</summary>
    public const double MarketGoldPerLevelPerSec = 15d;

    public event Action<string, string> VisitorEventRaised;

    float _lastVisitorRollUnscaledTime = -999f;
    const float VisitorRollCooldownSec = 0.75f;

    static long NowUnixSeconds() => TimeManager.GetUnixNow();

    /// <summary>주머니에 쌓인 시장 금화 (파생값).</summary>
    public double ComputePendingMarketGold()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return 0d;
        int lv = Mathf.Max(0, gm.currentUser.marketLevel);
        float acc = Mathf.Clamp(gm.currentUser.homeMarketAccumulatedSec, 0f, MaxMarketAccumulatedSec);
        return lv * MarketGoldPerLevelPerSec * acc;
    }

    /// <summary>레거시 UI 호환 (<see cref="VaultDisplay"/> 등).</summary>
    public double CurrentMarketAccumulated => ComputePendingMarketGold();

    /// <summary>8시간 만축 시 주머니 최대 금화.</summary>
    public double GetMarketMaxCapacity()
    {
        var gm = GameManager.InstanceOrNull;
        int lv = Mathf.Max(0, gm?.currentUser?.marketLevel ?? 0);
        return lv * MarketGoldPerLevelPerSec * MaxMarketAccumulatedSec;
    }

    public float AccumulatedTimeSec =>
        Mathf.Clamp(GameManager.InstanceOrNull?.currentUser?.homeMarketAccumulatedSec ?? 0f, 0f, MaxMarketAccumulatedSec);

    /// <summary>8시간을 1시간당 1단계 — 활성 더미 개수(0~8).</summary>
    public int GetGoldPileActiveCount()
    {
        float acc = AccumulatedTimeSec;
        int hours = Mathf.FloorToInt(acc / 3600f);
        return Mathf.Clamp(hours, 0, 8);
    }

    void Update()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        if (gm.currentUser.marketLevel <= 0) return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        float sentimentIncomeMul = 1f;
        var dm = DataManager.InstanceOrNull;
        if (dm != null && !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                       && dm.castleStateDataMap != null
                       && dm.castleStateDataMap.TryGetValue(dm.HomeCastleId.Trim(), out var homeSt)
                       && homeSt != null)
        {
            // 민심 100 = 1.0배, 0 → 0.75배, 200 → 1.25배 (UI에 숫자 노출 없음)
            sentimentIncomeMul = Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(homeSt.currentSentiment / 200f));
        }

        float next = gm.currentUser.homeMarketAccumulatedSec + dt * sentimentIncomeMul;
        if (next > MaxMarketAccumulatedSec)
            next = MaxMarketAccumulatedSec;
        if (!Mathf.Approximately(next, gm.currentUser.homeMarketAccumulatedSec))
        {
            gm.currentUser.homeMarketAccumulatedSec = next;
        }
    }

    /// <summary>성벽 터치: 노동 수익 + 주머니 합산.</summary>
    public void OnWallClicked(CollectionManager collectionManager = null)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        int laborLv = Mathf.Max(1, gm.currentUser.laborLevel);
        double laborGain = laborLv * 5d;
        double pocket = ComputePendingMarketGold();
        double totalGain = laborGain + pocket;

        if (totalGain > 0d)
            gm.AddGold(totalGain);

        gm.currentUser.homeMarketAccumulatedSec = 0f;
        gm.currentUser.lastMarketCollectTime = NowUnixSeconds();
        gm.SaveUserData();

        bool hadPocket = pocket > 0.01d;
        if (hadPocket && collectionManager != null)
            collectionManager.PlayPocketBurstThenHidePiles();

        if (collectionManager != null)
            collectionManager.PlayFloatingGainText((long)Math.Floor(totalGain));

        if (UnityEngine.Random.value < 0.01f)
            TryRaiseRandomVisitorEvent();

        GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
    }

    public void UpgradeLabor()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        double cost = UpgradeCost(HomeEconomyConfig.LaborBaseCost, gm.currentUser.laborLevel);
        if (gm.UseGold((long)cost))
            gm.currentUser.laborLevel++;
        gm.SaveUserData();
    }

    public void UpgradeMarket()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        int oldLevel = gm.currentUser.marketLevel;
        double cost = UpgradeCost(HomeEconomyConfig.MarketBaseCost, oldLevel);
        if (!gm.UseGold((long)cost)) return;
        gm.currentUser.marketLevel = oldLevel + 1;
        if (oldLevel <= 0)
        {
            gm.currentUser.homeMarketAccumulatedSec = 0f;
            gm.currentUser.lastMarketCollectTime = NowUnixSeconds();
        }
        gm.SaveUserData();
    }

    public void UpgradeWarehouse()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        int lv = gm.currentUser.warehouseLevel;
        double cost = GetWarehouseUpgradeGoldCost(lv);
        if (!gm.UseGold((long)cost)) return;
        gm.currentUser.warehouseLevel++;
        gm.SaveUserData();
    }

    public void UpgradeLogistics()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        int lv = gm.currentUser.farmLevel;
        double cost = GetLogisticsUpgradeGoldCost(lv);
        if (!gm.UseGold((long)cost)) return;
        gm.currentUser.farmLevel++;
        gm.SaveUserData();
        DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
        GlobalUIManager.InstanceOrNull?.RefreshMaintenanceHudFromEconomy();
    }

    public static double UpgradeCost(double baseCost, int level) =>
        baseCost * Math.Pow(HomeEconomyConfig.UpgradeCostMult, level);

    public static double GetLogisticsUpgradeGoldCost(int currentLogisticsLevel)
    {
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(currentLogisticsLevel + 1);
            if (d != null && d.logisticsCost > 0) return d.logisticsCost;
        }
        return UpgradeCost(HomeEconomyConfig.LogisticsBaseCost, currentLogisticsLevel);
    }

    public static double GetWarehouseUpgradeGoldCost(int currentWarehouseLevel)
    {
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(currentWarehouseLevel + 1);
            if (d != null && d.warehouseCost > 0) return d.warehouseCost;
        }
        return UpgradeCost(HomeEconomyConfig.WarehouseBaseCost, currentWarehouseLevel);
    }

    public bool ClaimStepReward(int milestoneIndex)
    {
        ReadStepMissionRows(out var missions, out var rewards);
        if (milestoneIndex < 0 || milestoneIndex >= missions.Length) return false;

        var gm = GameManager.InstanceOrNull;
        var u = gm?.currentUser;
        if (gm == null || u == null) return false;

        if (u.stepRewardsClaimed == null || u.stepRewardsClaimed.Length != missions.Length)
            u.stepRewardsClaimed = new bool[missions.Length];

        int need = missions[milestoneIndex];
        if (u.stepsToday < need) return false;
        if (u.stepRewardsClaimed[milestoneIndex]) return false;

        int reward = milestoneIndex < rewards.Length ? rewards[milestoneIndex] : 0;
        gm.AddMarchPoints(reward);
        u.stepRewardsClaimed[milestoneIndex] = true;
        gm.SaveUserData();
        return true;
    }

    public static void ReadStepMissionRows(out int[] milestones, out int[] rewards)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.IsReady && dm.stepMissionMap != null && dm.stepMissionMap.Count > 0)
        {
            var rows = new List<StepMissionData>(dm.stepMissionMap.Values);
            rows.Sort((a, b) =>
            {
                int sa = a != null ? a.step : 0;
                int sb = b != null ? b.step : 0;
                return sa.CompareTo(sb);
            });

            milestones = new int[rows.Count];
            rewards = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                milestones[i] = Mathf.Max(0, r != null ? r.targetSteps : 0);
                rewards[i] = Mathf.Max(0, r != null ? r.mpReward : 0);
            }
            return;
        }

        milestones = new[] { 2000, 5000, 7000, 10000 };
        rewards = new[] { 200, 500, 700, 1000 };
    }

    void TryRaiseRandomVisitorEvent()
    {
        if (Time.unscaledTime - _lastVisitorRollUnscaledTime < VisitorRollCooldownSec)
            return;
        _lastVisitorRollUnscaledTime = Time.unscaledTime;

        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsReady || dm.randomVisitorMap == null || dm.randomVisitorMap.Count == 0)
            return;

        float roll = UnityEngine.Random.Range(0f, 100f);
        float cursor = 0f;
        RandomVisitorData picked = null;
        foreach (var kv in dm.randomVisitorMap)
        {
            var row = kv.Value;
            if (row == null) continue;
            float p = Mathf.Max(0f, row.probability);
            if (p <= 0f) continue;
            cursor += p;
            if (roll <= cursor)
            {
                picked = row;
                break;
            }
        }

        if (picked == null) return;
        string body = ApplyVisitorEffectAndBuildMessage(picked);
        VisitorEventRaised?.Invoke(ResolveVisitorTitle(picked.visitorType), body);
    }

    string ApplyVisitorEffectAndBuildMessage(RandomVisitorData row)
    {
        string type = (row.visitorType ?? "").Trim();
        string rewardRaw = (row.effectReward ?? "").Trim();
        string lower = type.ToLowerInvariant();
        var gm = GameManager.InstanceOrNull;
        double value = ExtractFirstNumber(rewardRaw);

        if (lower.Contains("백성"))
        {
            string msg = string.IsNullOrWhiteSpace(rewardRaw) ? "민심 소문이 본영에 퍼졌습니다." : rewardRaw;
            NewsManager.InstanceOrNull?.AddNews(WorldNewsFeedKind.Rumor, "RV_CITIZEN", "", "백성 소문", msg, false);
            return msg;
        }

        if (lower.Contains("상인"))
        {
            long add = (long)Math.Max(0d, value > 0 ? value : 100d);
            if (add > 0) gm?.AddGold(add);
            return $"{(string.IsNullOrWhiteSpace(rewardRaw) ? "상단과 거래가 성사되었습니다." : rewardRaw)}\n보상: +{add:N0} Gold";
        }

        if (lower.Contains("장수"))
        {
            int mp = (int)Math.Max(0d, value > 0 ? value : 100d);
            if (mp > 0) gm?.AddMarchPoints(mp);
            NewsManager.InstanceOrNull?.AddNews(WorldNewsFeedKind.Breaking, "RV_GENERAL", "", "장수 방문",
                "장수가 본영을 방문해 사기를 북돋았습니다.", true);
            return $"{(string.IsNullOrWhiteSpace(rewardRaw) ? "장수의 방문으로 본영 분위기가 고조되었습니다." : rewardRaw)}\n보상: +{mp:N0} MP";
        }

        if (lower.Contains("도적"))
        {
            long loss = (long)Math.Max(0d, value > 0 ? value : 80d);
            if (loss > 0) gm?.AddGold(-loss);
            return $"{(string.IsNullOrWhiteSpace(rewardRaw) ? "도적이 출몰해 일부 재화가 소실되었습니다." : rewardRaw)}\n손실: -{loss:N0} Gold";
        }

        return string.IsNullOrWhiteSpace(rewardRaw) ? "이방인이 본영을 잠시 스쳐 지나갔습니다." : rewardRaw;
    }

    static string ResolveVisitorTitle(string visitorType)
    {
        string t = (visitorType ?? "").Trim();
        if (string.IsNullOrEmpty(t)) return "방문객 이벤트";
        return $"{t} 방문";
    }

    static double ExtractFirstNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0d;
        string s = raw.Trim();
        for (int i = 0; i < s.Length; i++)
        {
            if (!(char.IsDigit(s[i]) || s[i] == '.' || s[i] == '-')) continue;
            int j = i + 1;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
            string n = s.Substring(i, j - i);
            if (double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;
        }
        return 0d;
    }

}

/// <summary>본영 내정 업그레이드 비용 상수(기존 HomeController 밸런스와 동일 계열).</summary>
static class HomeEconomyConfig
{
    public const double UpgradeCostMult = 1.15;
    public const double LaborBaseCost = 50;
    public const double MarketBaseCost = 100;
    public const double WarehouseBaseCost = 90;
    public const double LogisticsBaseCost = 80;
}
