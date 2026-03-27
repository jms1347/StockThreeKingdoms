using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>최근 7일 시세용 단일 라인 + 하단 그라데이션 영역(MTS 상세 차트).</summary>
public class UIPriceLine7DayGraphic : MaskableGraphic
{
    [SerializeField] float lineThickness = 2f;
    [SerializeField] Color lineColor = new Color(0.45f, 0.78f, 1f, 0.95f);
    [SerializeField] Color fillTopColor = new Color(0.2f, 0.45f, 0.72f, 0.35f);
    [SerializeField] Color fillBottomColor = new Color(0.06f, 0.08f, 0.12f, 0.15f);

    float[] _norm;
    float[] _values;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetPrices(IReadOnlyList<float> sevenDailyCloses)
    {
        _values = null;
        _norm = null;
        if (sevenDailyCloses == null || sevenDailyCloses.Count < 2)
        {
            SetVerticesDirty();
            return;
        }

        int n = Mathf.Min(7, sevenDailyCloses.Count);
        _values = new float[n];
        for (int i = 0; i < n; i++)
            _values[i] = Mathf.Max(1f, sevenDailyCloses[i]);

        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            if (_values[i] < min) min = _values[i];
            if (_values[i] > max) max = _values[i];
        }

        float range = Mathf.Max(max * 0.002f, max - min);
        _norm = new float[n];
        for (int i = 0; i < n; i++)
            _norm[i] = (_values[i] - min) / range;
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

        if (_norm == null || _norm.Length < 2)
            return;

        int n = _norm.Length;
        float baseY = padY;

        for (int i = 0; i < n - 1; i++)
        {
            float t0 = i / (float)(n - 1);
            float t1 = (i + 1) / (float)(n - 1);
            float x0 = padX + t0 * innerW;
            float x1 = padX + t1 * innerW;
            float yn0 = Mathf.Clamp01(_norm[i]);
            float yn1 = Mathf.Clamp01(_norm[i + 1]);
            float y0 = padY + yn0 * innerH;
            float y1 = padY + yn1 * innerH;

            var cTop = (Color32)Color.Lerp(fillBottomColor, fillTopColor, 0.65f);
            var cBot = (Color32)fillBottomColor;
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, baseY, 0f), cBot, Vector2.zero);
            vh.AddVert(new Vector3(x1, baseY, 0f), cBot, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1, 0f), cTop, Vector2.zero);
            vh.AddVert(new Vector3(x0, y0, 0f), cTop, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }

        var lc = (Color32)lineColor;
        for (int i = 0; i < n - 1; i++)
        {
            float t0 = i / (float)(n - 1);
            float t1 = (i + 1) / (float)(n - 1);
            float x0 = padX + t0 * innerW;
            float x1 = padX + t1 * innerW;
            float y0 = padY + Mathf.Clamp01(_norm[i]) * innerH;
            float y1 = padY + Mathf.Clamp01(_norm[i + 1]) * innerH;
            AddThickSegment(vh, new Vector2(x0, y0), new Vector2(x1, y1), lineThickness, lc);
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
