using UnityEngine;

/// <summary>작위 티어에 따른 뱃지·아바타 테두리 색.</summary>
public static class PlayerTitleVisuals
{
    public const int TierCount = 5;

    public struct TierStyle
    {
        public Color BadgeBackground;
        public Color BadgeText;
        public Color BadgeOutline;
        public float BadgeOutlineWidth;
        public Color AvatarOutline;
        public float AvatarOutlineWidth;
    }

    /// <summary>rankTitleId·표시 작위 문자열로 0(최하)~4(최상) 티어.</summary>
    public static int ResolveTier(string rankTitleId, string rankTitleDisplay)
    {
        string key = $"{rankTitleId} {rankTitleDisplay}".ToLowerInvariant();

        if (ContainsAny(key, "emperor", "황제", "皇帝")) return 4;
        if (ContainsAny(key, "king", "왕", "王", "제후")) return 3;
        if (ContainsAny(key, "merchant", "거상", "巨商")) return 2;
        if (ContainsAny(key, "lord", "군주", "태수", "현령")) return 1;
        return 0;
    }

    static bool ContainsAny(string hay, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (string.IsNullOrEmpty(needles[i])) continue;
            if (hay.Contains(needles[i])) return true;
        }
        return false;
    }

    public static TierStyle GetStyle(int tier)
    {
        tier = Mathf.Clamp(tier, 0, TierCount - 1);
        switch (tier)
        {
            case 4:
                return new TierStyle
                {
                    BadgeBackground = new Color(0.42f, 0.22f, 0.55f, 0.92f),
                    BadgeText = new Color(1f, 0.95f, 0.65f),
                    BadgeOutline = new Color(1f, 0.65f, 0.15f, 0.95f),
                    BadgeOutlineWidth = 2.2f,
                    AvatarOutline = new Color(1f, 0.55f, 0.2f, 0.95f),
                    AvatarOutlineWidth = 3f
                };
            case 3:
                return new TierStyle
                {
                    BadgeBackground = new Color(0.28f, 0.22f, 0.12f, 0.92f),
                    BadgeText = new Color(1f, 0.9f, 0.55f),
                    BadgeOutline = new Color(0.95f, 0.75f, 0.25f, 0.95f),
                    BadgeOutlineWidth = 1.8f,
                    AvatarOutline = new Color(0.95f, 0.75f, 0.2f, 0.95f),
                    AvatarOutlineWidth = 2.4f
                };
            case 2:
                return new TierStyle
                {
                    BadgeBackground = new Color(0.18f, 0.24f, 0.32f, 0.92f),
                    BadgeText = new Color(0.85f, 0.95f, 1f),
                    BadgeOutline = new Color(0.55f, 0.75f, 0.95f, 0.9f),
                    BadgeOutlineWidth = 1.4f,
                    AvatarOutline = new Color(0.5f, 0.7f, 0.95f, 0.9f),
                    AvatarOutlineWidth = 2f
                };
            case 1:
                return new TierStyle
                {
                    BadgeBackground = new Color(0.16f, 0.22f, 0.18f, 0.9f),
                    BadgeText = new Color(0.85f, 1f, 0.9f),
                    BadgeOutline = new Color(0.35f, 0.75f, 0.45f, 0.85f),
                    BadgeOutlineWidth = 1.2f,
                    AvatarOutline = new Color(0.35f, 0.65f, 0.45f, 0.85f),
                    AvatarOutlineWidth = 1.8f
                };
            default:
                return new TierStyle
                {
                    BadgeBackground = new Color(0.22f, 0.24f, 0.28f, 0.88f),
                    BadgeText = new Color(0.9f, 0.9f, 0.92f),
                    BadgeOutline = new Color(0.45f, 0.48f, 0.52f, 0.75f),
                    BadgeOutlineWidth = 1f,
                    AvatarOutline = new Color(0.4f, 0.45f, 0.5f, 0.75f),
                    AvatarOutlineWidth = 1.4f
                };
        }
    }
}
