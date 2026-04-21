using System.Collections.Generic;
using UnityEngine;

/// <summary><c>Resources/UserPortraits/{id}</c>에서 스프라이트를 로드합니다. 없으면 <c>default</c>를 시도합니다.</summary>
public static class UserPortraitLoader
{
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite GetPortrait(string characterId)
    {
        string key = string.IsNullOrWhiteSpace(characterId) ? "default" : characterId.Trim();
        if (Cache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var s = Resources.Load<Sprite>($"UserPortraits/{key}");
        if (s == null && key != "default")
            s = Resources.Load<Sprite>("UserPortraits/default");
        Cache[key] = s;
        return s;
    }
}
