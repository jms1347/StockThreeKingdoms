using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>7일 인구(0~maxCapacity)·민심(0~100) 이중 라인 차트. 파란=인구, 노랑=민심.</summary>
public class UIPopSentiment7DayChart : MaskableGraphic
{
    [SerializeField] float lineThickness = 2f;
    [SerializeField] Color populationLineColor = new Color(0.35f, 0.62f, 0.95f, 0.92f);
    [SerializeField] Color sentimentLineColor = new Color(1f, 0.88f, 0.38f, 0.92f);

    float[] _popNorm;
    float[] _sentNorm;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetSeries(IReadOnlyList<float> population7, IReadOnlyList<float> sentiment7, int maxPopulationCapacity)
    {
        _popNorm = null;
        _sentNorm = null;
        int n = 7;
        if (population7 == null || sentiment7 == null || population7.Count < 2 || sentiment7.Count < 2)
        {
            SetVerticesDirty();
            return;
        }

        int c = Mathf.Min(7, Mathf.Min(population7.Count, sentiment7.Count));
        if (c < 2)
        {
            SetVerticesDirty();
            return;
        }

        float cap = Mathf.Max(1f, maxPopulationCapacity);
        _popNorm = new float[c];
        _sentNorm = new float[c];
        for (int i = 0; i < c; i++)
        {
            _popNorm[i] = Mathf.Clamp01(population7[i] / cap);
            _sentNorm[i] = Mathf.Clamp01(sentiment7[i] / 100f);
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float w = r.width;
        float h = r.height;
        if (w < 4f || h < 4f) return;

        float padX = 4f;
        float padY = 6f;
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
        var c32 = (Color32)col;
        for (int i = 0; i < n - 1; i++)
        {
            float t0 = i / (float)(n - 1);
            float t1 = (i + 1) / (float)(n - 1);
            float x0 = padX + t0 * innerW;
            float x1 = padX + t1 * innerW;
            float y0 = padY + Mathf.Clamp01(yNorm[i]) * innerH;
            float y1 = padY + Mathf.Clamp01(yNorm[i + 1]) * innerH;
            AddThickSegment(vh, new Vector2(x0, y0), new Vector2(x1, y1), lineThickness, c32);
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
}
