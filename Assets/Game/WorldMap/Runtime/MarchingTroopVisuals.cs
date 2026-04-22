using UnityEngine;

/// <summary>행군 마커용 원형 스프라이트(런타임 생성).</summary>
public static class MarchingTroopVisuals
{
    const int TexSize = 36;
    const int Radius = 15;

    static Texture2D s_tex;
    static Sprite s_sprite;

    public static Sprite GetOrCreateDotSprite()
    {
        if (s_sprite != null)
            return s_sprite;

        s_tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        s_tex.wrapMode = TextureWrapMode.Clamp;
        s_tex.filterMode = FilterMode.Bilinear;

        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = Color.white;
        float r2 = Radius * Radius;
        int cx = TexSize / 2;
        int cy = TexSize / 2;

        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                s_tex.SetPixel(x, y, dx * dx + dy * dy <= r2 ? fill : clear);
            }
        }

        s_tex.Apply(false, true);
        s_sprite = Sprite.Create(
            s_tex,
            new Rect(0, 0, TexSize, TexSize),
            new Vector2(0.5f, 0.5f),
            64f);
        return s_sprite;
    }
}
