using System;
using UnityEngine;

/// <summary>월드 뉴스 피드 탭 분류. <see cref="WorldNewsItem.newsKind"/>에 바이트로 저장. 기사 마스터 <see cref="NewsType"/>과 별개.</summary>
public enum WorldNewsFeedKind : byte
{
    All = 0,
    War = 1,
    Breaking = 2,
    Rumor = 3,
    Headquarters = 4,
    /// <summary>[INIT]/[LOAD] 등 — 일반 뉴스 탭에서 제외.</summary>
    System = 5
}

/// <summary>월드 뉴스 피드. <see cref="DataManager.AddNewsItem"/>와 연동.</summary>
public class NewsManager : Singleton<NewsManager>
{
    public event Action<WorldNewsFeedKind, string> OnNewsAdded;

    /// <summary>리스트·필터용 태그 문자열.</summary>
    public static string GetWorldNewsFeedTag(WorldNewsFeedKind t)
    {
        switch (t)
        {
            case WorldNewsFeedKind.War: return "[전쟁]";
            case WorldNewsFeedKind.Breaking: return "[속보]";
            case WorldNewsFeedKind.Rumor: return "[소문]";
            case WorldNewsFeedKind.Headquarters: return "[본영]";
            default: return "";
        }
    }

    /// <summary>구조화된 뉴스 추가(권장).</summary>
    public void AddNews(WorldNewsFeedKind type, string eventId, string targetCastleId, string headline, string bodyContent,
        bool isConfirmed)
    {
        AddNewsAndReturn(type, eventId, targetCastleId, headline, bodyContent, isConfirmed);
    }

    /// <summary>추가된 <see cref="WorldNewsItem"/> 참조가 필요할 때(파이프라인 연동).</summary>
    public WorldNewsItem AddNewsAndReturn(WorldNewsFeedKind type, string eventId, string targetCastleId, string headline,
        string bodyContent, bool isConfirmed)
    {
        if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(bodyContent))
            return null;

        string hl = string.IsNullOrWhiteSpace(headline) ? bodyContent.Trim() : headline.Trim();
        string body = string.IsNullOrWhiteSpace(bodyContent) ? hl : bodyContent.Trim();

        var item = BuildWorldNewsItem(type, eventId, targetCastleId, hl, body, isConfirmed);

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.AddNewsItem(item);

        OnNewsAdded?.Invoke(type, hl);
        return item;
    }

    /// <summary>레거시: 메시지 한 줄만(이벤트 ID·성 없음).</summary>
    public void AddNews(WorldNewsFeedKind type, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string body = message.Trim();
        AddNews(type, "", "", body, body, true);
    }

    public static WorldNewsItem BuildWorldNewsItem(WorldNewsFeedKind type, string eventId, string targetCastleId, string headline,
        string bodyContent, bool isConfirmed)
    {
        string tag = GetWorldNewsFeedTag(type);
        string displayHeadline = headline?.Trim() ?? "";
        string textLine = string.IsNullOrEmpty(tag) ? displayHeadline : $"{tag} {displayHeadline}";
        bool rumor = type == WorldNewsFeedKind.Rumor;
        bool factTag = type == WorldNewsFeedKind.Breaking || type == WorldNewsFeedKind.Headquarters;
        string tid = (targetCastleId ?? "").Trim();
        return new WorldNewsItem
        {
            unixTime = TimeManager.GetUnixNow(),
            newsKind = (byte)type,
            eventId = eventId ?? "",
            targetCastleId = tid,
            headline = displayHeadline,
            bodyContent = bodyContent?.Trim() ?? "",
            detailTitle = displayHeadline,
            detailBody = bodyContent?.Trim() ?? "",
            text = textLine,
            relatedCastleIdsRaw = string.IsNullOrEmpty(tid) ? "" : tid,
            debuffIconsHint = tag,
            isVerifiedFact = factTag && !rumor,
            isRumorContent = rumor,
            isConfirmed = isConfirmed
        };
    }

    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(NewsManager));
        go.AddComponent<NewsManager>();
    }
}
