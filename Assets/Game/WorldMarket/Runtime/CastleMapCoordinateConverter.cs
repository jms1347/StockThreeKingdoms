using UnityEngine;

/// <summary>
/// 에디터 <see cref="CastleWorldMapEditorWindow"/>와 동일한 0~1000 정규화 좌표를
/// 런타임 맵 콘텐츠(RectTransform, pivot·앵커 하단-좌 기준)의 앵커드 위치로 변환합니다.
/// </summary>
public static class CastleMapCoordinateConverter
{
    /// <param name="posX">마스터 posX (0~1000)</param>
    /// <param name="posY">마스터 posY (0~1000, 북쪽이 큰 값)</param>
    /// <param name="worldMax">정규화 상한(기본 1000)</param>
    /// <param name="contentWidth">맵 콘텐츠 내부 폭(px)</param>
    /// <param name="contentHeight">맵 콘텐츠 내부 높이(px)</param>
    /// <param name="margin">가장자리 여백(px)</param>
    public static Vector2 NormalizedWorldToAnchoredPosition(
        float posX,
        float posY,
        float worldMax,
        float contentWidth,
        float contentHeight,
        float margin)
    {
        worldMax = Mathf.Max(1f, worldMax);
        float innerW = Mathf.Max(1f, contentWidth - 2f * margin);
        float innerH = Mathf.Max(1f, contentHeight - 2f * margin);
        float x = margin + Mathf.Clamp01(posX / worldMax) * innerW;
        float y = margin + Mathf.Clamp01(posY / worldMax) * innerH;
        return new Vector2(x, y);
    }
}
