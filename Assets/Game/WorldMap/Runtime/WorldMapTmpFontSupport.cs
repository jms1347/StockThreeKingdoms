using TMPro;
using UnityEngine;

/// <summary>월드 공간 TMP에 한글 폰트가 비어 있을 때 기본 SDF를 주입합니다.</summary>
public static class WorldMapTmpFontSupport
{
    static TMP_FontAsset s_cachedFont;

    public static void Apply(TMP_Text tmp)
    {
        if (tmp == null) return;
        var font = GetKoreanFont();
        if (font != null)
            tmp.font = font;
    }

    static TMP_FontAsset GetKoreanFont()
    {
        if (s_cachedFont != null) return s_cachedFont;
        s_cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/esamanru Medium SDF");
        if (s_cachedFont == null)
            s_cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/PretendardVariable SDF");
        if (s_cachedFont == null && TMP_Settings.defaultFontAsset != null)
            s_cachedFont = TMP_Settings.defaultFontAsset;
        return s_cachedFont;
    }
}
