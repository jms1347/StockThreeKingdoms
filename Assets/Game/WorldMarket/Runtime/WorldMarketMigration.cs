using UnityEngine;

/// <summary>천하 탭 본영 이주 진입점. 씬의 WorldMarketRoot 아래 HUD를 확보한 뒤 이동을 시작합니다.</summary>
public static class WorldMarketMigration
{
    /// <summary>지정 성으로 본영 이주 예약·행군을 시작합니다. UI 계층에서 WorldMarketRoot를 찾을 때는 <paramref name="searchFrom"/>를 넘기세요.</summary>
    public static bool StartMigration(string castleId, Transform searchFrom = null)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return false;
        var dm = DataManager.InstanceOrNull;
        if (dm == null)
        {
            Debug.LogWarning("[WorldMarketMigration] DataManager가 준비되지 않았습니다.");
            return false;
        }

        if (!dm.TryValidateHqMove(castleId.Trim(), out _, out var err))
        {
            Debug.LogWarning("[WorldMarketMigration] " + err);
            return false;
        }

        Transform root = GameObject.Find("WorldMarketRoot")?.transform;
        if (root == null && searchFrom != null)
        {
            for (Transform t = searchFrom; t != null; t = t.parent)
            {
                if (t.name == "WorldMarketRoot")
                {
                    root = t;
                    break;
                }
            }
        }

        if (root != null)
            WorldHqTravelHud.EnsureUnderWorldMarketRoot(root);

        if (WorldHqTravelHud.InstanceOrNull == null)
        {
            Debug.LogWarning("[WorldMarketMigration] WorldHqTravelHud가 없습니다.");
            return false;
        }

        WorldHqTravelHud.InstanceOrNull.TryBeginTravelTo(castleId.Trim());
        return true;
    }
}
