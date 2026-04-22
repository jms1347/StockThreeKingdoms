using UnityEngine;

/// <summary><see cref="WorldMapLayout"/> 안에서 카메라 시야가 벗어나지 않도록 제한합니다.</summary>
public static class WorldMapCameraBounds
{
    public static void ClampCamera(Camera cam)
    {
        if (cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float minCx = WorldMapLayout.MapWorldMinX + halfW;
        float maxCx = WorldMapLayout.MapWorldMaxX - halfW;
        float minCy = WorldMapLayout.MapWorldMinY + halfH;
        float maxCy = WorldMapLayout.MapWorldMaxY - halfH;

        float cx = cam.transform.position.x;
        float cy = cam.transform.position.y;
        if (minCx > maxCx) cx = (WorldMapLayout.MapWorldMinX + WorldMapLayout.MapWorldMaxX) * 0.5f;
        else cx = Mathf.Clamp(cx, minCx, maxCx);
        if (minCy > maxCy) cy = (WorldMapLayout.MapWorldMinY + WorldMapLayout.MapWorldMaxY) * 0.5f;
        else cy = Mathf.Clamp(cy, minCy, maxCy);

        var p = cam.transform.position;
        cam.transform.position = new Vector3(cx, cy, p.z);
    }

    /// <summary>맵 레이아웃 안에 전체가 들어가도록 할 수 있는 최대 직교 시야(반높이) 상한.</summary>
    public static float GetMaxOrthographicSizeForLayout(Camera cam)
    {
        if (cam == null || !cam.orthographic) return 32f;
        float mapHalfW = (WorldMapLayout.MapWorldMaxX - WorldMapLayout.MapWorldMinX) * 0.5f;
        float mapHalfH = (WorldMapLayout.MapWorldMaxY - WorldMapLayout.MapWorldMinY) * 0.5f;
        float byWidth = mapHalfW / Mathf.Max(0.001f, cam.aspect);
        return Mathf.Max(0.01f, Mathf.Min(byWidth, mapHalfH));
    }

    /// <summary>줌 레벨을 [minSize, min(maxSize, 레이아웃 상한)]로 제한합니다.</summary>
    public static void ClampOrthographicSize(Camera cam, float minSize, float maxSize)
    {
        if (cam == null || !cam.orthographic) return;
        float layoutCap = GetMaxOrthographicSizeForLayout(cam);
        float hi = Mathf.Max(minSize, Mathf.Min(maxSize, layoutCap));
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minSize, hi);
    }
}
