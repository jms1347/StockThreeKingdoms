using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>구글 시트 G~L열 등에서 채우는 문자열 템플릿(런타임 <see cref="DataManager"/>).</summary>
[Serializable]
public class NewsTemplateSheetRow
{
    public string rumorHeadline;
    public string rumorBody;
    public string breakingHeadline;
    public string breakingBody;
    public string reporterScript;
    /// <summary>L열 여분(아이콘 경로 등). 현재 런타임 미사용 가능.</summary>
    public string columnL;
}

/// <summary>이벤트 ID별 소문/속보 문구·리포터 UI. 시트 문자열 + SO 스프라이트 병합.</summary>
[Serializable]
public class NewsTemplateEntry
{
    [Tooltip("EventMasterData.id 와 동일")]
    public string id;
    [TextArea(1, 3)] public string rumorHeadline;
    [TextArea(2, 6)] public string rumorBody;
    [TextArea(1, 3)] public string breakingHeadline;
    [TextArea(2, 8)] public string breakingBody;
    [TextArea(1, 4)] public string reporterScript;
    public Sprite reporterIcon;
}

[CreateAssetMenu(fileName = "NewsTemplateSo", menuName = "ScriptableObject/NewsTemplateSo")]
public class NewsTemplateSo : ScriptableObject
{
    public List<NewsTemplateEntry> entries = new List<NewsTemplateEntry>();

    NewsTemplateEntry FindLocal(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || entries == null) return null;
        string key = eventId.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && string.Equals(e.id, key, StringComparison.Ordinal))
                return e;
        }

        return null;
    }

    /// <summary>SO <see cref="entries"/>만 조회(구글 시트 병합 없음). <see cref="TryGet"/>와 달리 DataManager 시트를 섞지 않습니다.</summary>
    public bool TryGetSoEntryOnly(string eventId, out NewsTemplateEntry entry)
    {
        entry = FindLocal(eventId);
        return entry != null;
    }

    /// <summary>시트 + SO 병합 조회. 문자열은 시트 우선(비어 있지 않을 때).</summary>
    public bool TryGet(string eventId, out NewsTemplateEntry merged)
    {
        merged = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        string key = eventId.Trim();
        var local = FindLocal(key);
        NewsTemplateSheetRow sheet = null;
        DataManager.InstanceOrNull?.TryGetNewsTemplateSheetRow(key, out sheet);

        if (local == null && sheet == null) return false;

        merged = new NewsTemplateEntry
        {
            id = key,
            rumorHeadline = PickNonEmpty(sheet?.rumorHeadline, local?.rumorHeadline),
            rumorBody = PickNonEmpty(sheet?.rumorBody, local?.rumorBody),
            breakingHeadline = PickNonEmpty(sheet?.breakingHeadline, local?.breakingHeadline),
            breakingBody = PickNonEmpty(sheet?.breakingBody, local?.breakingBody),
            reporterScript = PickNonEmpty(sheet?.reporterScript, local?.reporterScript),
            reporterIcon = local?.reporterIcon
        };
        return true;
    }

    static string PickNonEmpty(string a, string b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        return b?.Trim() ?? "";
    }

    /// <summary>레거시 진입점 — <see cref="NewsFormatter.FormatNews"/>와 동일(버프 플레이스홀더 없음).</summary>
    public static string GetFormattedContent(string raw, string castleId, DataManager dm) =>
        NewsFormatter.FormatNews(raw, castleId, dm, null);
}
