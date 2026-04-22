using UnityEngine;

/// <summary>월드맵 월드 좌표 범위. 시트 posX,posY(0~1000)는 <see cref="SheetMapToWorld"/>로 변환합니다.</summary>
public static class WorldMapLayout
{
    public const float MapWorldMinX = -11f;
    public const float MapWorldMaxX = 11f;
    public const float MapWorldMinY = -7f;
    public const float MapWorldMaxY = 6.5f;

    public static Vector2 SheetMapToWorld(float posX, float posY)
    {
        return new Vector2(
            Mathf.Lerp(MapWorldMinX, MapWorldMaxX, Mathf.Clamp01(posX / 1000f)),
            Mathf.Lerp(MapWorldMinY, MapWorldMaxY, Mathf.Clamp01(posY / 1000f)));
    }
}
