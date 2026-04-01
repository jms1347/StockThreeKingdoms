using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7일 인구·민심 차트용 폴리라인(캔버스 오버레이에서도 <see cref="LineRenderer"/> 대신 확실히 보이도록 UI 메시로 그림).
/// </summary>
[DisallowMultipleComponent]
public class UIPopSentiment7DaySeriesLines : MaskableGraphic
{
    Vector2[] _seriesA;
    Vector2[] _seriesB;
    Color32 _colorA;
    Color32 _colorB;
    float _thicknessPx = 3f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetTwoSeries(Vector2[] lineA, Color colorA, Vector2[] lineB, Color colorB, float thicknessPixels)
    {
        _seriesA = lineA;
        _seriesB = lineB;
        _colorA = colorA;
        _colorB = colorB;
        _thicknessPx = Mathf.Clamp(thicknessPixels, 1f, 10f);
        SetVerticesDirty();
    }

    public void ClearSeries()
    {
        _seriesA = null;
        _seriesB = null;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_seriesA != null && _seriesA.Length >= 2)
            DrawPolyline(vh, _seriesA, _colorA);
        if (_seriesB != null && _seriesB.Length >= 2)
            DrawPolyline(vh, _seriesB, _colorB);
    }

    void DrawPolyline(VertexHelper vh, Vector2[] pts, Color32 col)
    {
        for (int i = 0; i < pts.Length - 1; i++)
            AddThickSegment(vh, pts[i], pts[i + 1], _thicknessPx, col);
    }

    static void AddThickSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 c)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Vector2 dir = d / len;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert((Vector3)(a - perp), c, Vector2.zero);
        vh.AddVert((Vector3)(a + perp), c, Vector2.zero);
        vh.AddVert((Vector3)(b + perp), c, Vector2.zero);
        vh.AddVert((Vector3)(b - perp), c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
