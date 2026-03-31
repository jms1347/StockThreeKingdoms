using UnityEngine;

/// <summary>
/// 플레이어 본영(<see cref="DataManager.HomeCastleId"/>)과 이벤트 성 사이 지도 거리에 따른 뉴스 도달·확실도(임시 스펙).
/// 거리 구간은 이주 UI의 발자국 티어와 동일한 스케일(180 / 420)을 사용합니다.
/// </summary>
public static class WorldNewsReach
{
    const float NearMap = 180f;
    const float MidMap = 420f;

    /// <summary>지도 좌표 거리 → 1 근접, 2 중간, 3 먼 지역. 거리 미계산 시 0.</summary>
    public static int GetDistanceTier(float mapDistance)
    {
        if (mapDistance < 0f || float.IsNaN(mapDistance)) return 0;
        if (mapDistance < NearMap) return 1;
        if (mapDistance < MidMap) return 2;
        return 3;
    }

    /// <summary>본영~이벤트 성 거리. 본영 미설정·마스터 없음이면 -1.</summary>
    public static float GetHqToEventCastleDistance(DataManager dm, string eventCastleId)
    {
        if (dm == null) return -1f;
        string hq = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(hq)) return -1f;
        return dm.GetDistance(hq, eventCastleId);
    }

    /// <summary>소문: 먼 지역이면 부가 문구만 붙입니다(내용은 그대로 피드에 올라감).</summary>
    public static void ApplyDistanceTagToRumor(DataManager dm, WorldNewsItem item, string primaryCastleId)
    {
        if (dm == null || item == null) return;
        float d = GetHqToEventCastleDistance(dm, primaryCastleId);
        int tier = GetDistanceTier(d);
        if (tier <= 1) return;

        string tag = tier == 2 ? "멀리서 들린 풍문입니다." : "아주 먼 지역의 미확인 풍문입니다.";
        if (string.IsNullOrWhiteSpace(item.detailSubline))
            item.detailSubline = tag;
        else if (item.detailSubline.IndexOf(tag, System.StringComparison.Ordinal) < 0)
            item.detailSubline = $"{item.detailSubline.Trim()} · {tag}";
    }

    /// <summary>
    /// 속보·사실 확인: 멀수록 <see cref="WorldNewsItem.isVerifiedFact"/>가 꺼질 임시 확률.
    /// 실패 시 <see cref="WorldNewsItem.impactRangeText"/>에 안내 문구를 덧붙입니다.
    /// </summary>
    public static void ApplyDistanceIntelToBreakingNews(DataManager dm, WorldNewsItem item, string primaryCastleId,
        float chanceTier1, float chanceTier2, float chanceTier3)
    {
        if (dm == null || item == null) return;
        if (!item.isVerifiedFact || item.isDebunked) return;

        float d = GetHqToEventCastleDistance(dm, primaryCastleId);
        float p;
        if (d < 0f)
            p = 1f;
        else if (d < NearMap)
            p = Mathf.Clamp01(chanceTier1);
        else if (d < MidMap)
            p = Mathf.Clamp01(chanceTier2);
        else
            p = Mathf.Clamp01(chanceTier3);

        if (Random.value <= p)
            return;

        item.isVerifiedFact = false;
        const string note = "먼 거리로 정확한 확인은 어렵습니다. 현지 정보가 들어오면 갱신될 수 있습니다.";
        if (string.IsNullOrWhiteSpace(item.impactRangeText))
            item.impactRangeText = note;
        else if (item.impactRangeText.IndexOf(note, System.StringComparison.Ordinal) < 0)
            item.impactRangeText = $"{item.impactRangeText.Trim()}\n{note}";
    }
}
