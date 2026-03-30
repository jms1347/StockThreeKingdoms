using System;
using UnityEngine;

/// <summary>뉴스 타입 — UI 필터용. <see cref="WorldNewsItem"/>에 바이트로 저장할 때 동일 값 사용.</summary>
public enum NewsType : byte
{
    All = 0,
    War = 1,
    Breaking = 2,
    Rumor = 3,
    Headquarters = 4
}

/// <summary>월드 뉴스 피드. <see cref="DataManager.AddNewsItem"/>와 연동.</summary>
public class NewsManager : Singleton<NewsManager>
{
    public event Action<NewsType, string> OnNewsAdded;

    static string TypeTag(NewsType t)
    {
        switch (t)
        {
            case NewsType.War: return "[전쟁]";
            case NewsType.Breaking: return "[속보]";
            case NewsType.Rumor: return "[소문]";
            case NewsType.Headquarters: return "[본영]";
            default: return "";
        }
    }

    public void AddNews(NewsType type, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        string body = message.Trim();
        string tag = TypeTag(type);
        string title = string.IsNullOrEmpty(tag) ? body : $"{tag} {body}";

        var item = new WorldNewsItem
        {
            unixTime = TimeManager.GetUnixNow(),
            text = title,
            detailTitle = body,
            detailBody = body,
            debuffIconsHint = tag
        };

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.AddNewsItem(item);

        OnNewsAdded?.Invoke(type, body);
    }

    public static void EnsureCreated()
    {
        if (InstanceOrNull != null) return;
        var go = new GameObject(nameof(NewsManager));
        go.AddComponent<NewsManager>();
    }
}
