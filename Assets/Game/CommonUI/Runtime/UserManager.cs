using UnityEngine;

/// <summary>
/// 로그인 유저(세이브) 데이터 접근. 실제 소스는 <see cref="GameManager.currentUser"/> 입니다.
/// </summary>
public static class UserManager
{
    public static UserData Current => GameManager.InstanceOrNull != null ? GameManager.InstanceOrNull.currentUser : null;

    public static string GetNickname()
    {
        var u = Current;
        if (u == null || string.IsNullOrWhiteSpace(u.userName)) return "—";
        return u.userName.Trim();
    }

    /// <summary>작위 ID(비어 있으면 rankTitle 문자열을 ID처럼 사용).</summary>
    public static string GetRankTitleId()
    {
        var u = Current;
        if (u == null) return "";
        if (!string.IsNullOrWhiteSpace(u.rankTitleId)) return u.rankTitleId.Trim();
        return u.rankTitle != null ? u.rankTitle.Trim() : "";
    }

    public static string GetRankTitleDisplay()
    {
        var u = Current;
        if (u == null) return "—";
        if (!string.IsNullOrWhiteSpace(u.rankTitle)) return u.rankTitle.Trim();
        return "—";
    }

    public static string GetEquippedCharacterId()
    {
        var u = Current;
        if (u == null) return "";
        return u.equippedCharacterId != null ? u.equippedCharacterId.Trim() : "";
    }
}
