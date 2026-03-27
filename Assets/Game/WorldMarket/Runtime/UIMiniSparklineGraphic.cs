using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인구·민심 히스토리용 얇은 2중 라인 (LineRenderer 대신 UI 메시, 스크롤 리스트에 가볍게).
/// </summary>
public class UIMiniSparklineGraphic : MaskableGraphic
{
    [SerializeField] float lineThickness = 1.25f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }
    [SerializeField] Color populationLineColor = new Color(0.38f, 0.92f, 0.58f, 0.45f);
    [SerializeField] Color sentimentLineColor = new Color(1f, 0.88f, 0.38f, 0.45f);

    float[] _popNorm;
    float[] _sentNorm;

    public void SetHistories(IReadOnlyList<int> population, IReadOnlyList<float> sentiment)
    {
        _popNorm = NormalizeInts(population);
        _sentNorm = NormalizeFloats(sentiment);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float w = r.width;
        float h = r.height;
        if (w < 4f || h < 4f) return;

        float padX = 2f;
        float padY = h * 0.08f;
        float innerW = w - padX * 2f;
        float innerH = h - padY * 2f;
        if (innerW < 2f || innerH < 2f) return;

        if (_popNorm != null && _popNorm.Length >= 2)
            DrawSeries(vh, _popNorm, populationLineColor, padX, padY, innerW, innerH);
        if (_sentNorm != null && _sentNorm.Length >= 2)
            DrawSeries(vh, _sentNorm, sentimentLineColor, padX, padY, innerW, innerH);
    }

    void DrawSeries(VertexHelper vh, float[] yNorm, Color col, float padX, float padY, float innerW, float innerH)
    {
        int n = yNorm.Length;
        if (n < 2) return;
        var c32 = (Color32)col;
        for (int i = 0; i < n - 1; i++)
        {
            float t0 = n == 1 ? 0f : i / (float)(n - 1);
            float t1 = n == 1 ? 1f : (i + 1) / (float)(n - 1);
            float x0 = padX + t0 * innerW;
            float x1 = padX + t1 * innerW;
            float yn0 = Mathf.Clamp01(yNorm[i]);
            float yn1 = Mathf.Clamp01(yNorm[i + 1]);
            float y0 = padY + yn0 * innerH;
            float y1 = padY + yn1 * innerH;
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            AddThickSegment(vh, a, b, lineThickness, c32);
        }
    }

    static void AddThickSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Vector2 dir = d / len;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(a - perp, col, Vector2.zero);
        vh.AddVert(a + perp, col, Vector2.zero);
        vh.AddVert(b + perp, col, Vector2.zero);
        vh.AddVert(b - perp, col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    static float[] NormalizeInts(IReadOnlyList<int> data)
    {
        if (data == null || data.Count < 2) return null;
        int n = data.Count;
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            int v = data[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        float range = Mathf.Max(1, max - min);
        var o = new float[n];
        for (int i = 0; i < n; i++)
            o[i] = (data[i] - min) / range;
        return o;
    }

    static float[] NormalizeFloats(IReadOnlyList<float> data)
    {
        if (data == null || data.Count < 2) return null;
        int n = data.Count;
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            float v = data[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        float range = Mathf.Max(0.0001f, max - min);
        var o = new float[n];
        for (int i = 0; i < n; i++)
            o[i] = (data[i] - min) / range;
        return o;
    }
}
