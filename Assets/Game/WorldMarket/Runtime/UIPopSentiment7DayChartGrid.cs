using UnityEngine;
using UnityEngine.UI;

/// <summary>차트 플롯 영역용 얇은 그리드(세로·가로).</summary>
[DisallowMultipleComponent]
public class UIPopSentiment7DayChartGrid : MaskableGraphic
{
    [SerializeField] Color gridColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] int verticalLineCount = 7;
    [SerializeField] int horizontalLineCount = 5;
    [SerializeField] float lineThicknessPx = 1.75f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void RequestRedraw() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        if (rect.width < 2f || rect.height < 2f) return;

        Color32 c = gridColor;
        float left = rect.xMin;
        float right = rect.xMax;
        float bottom = rect.yMin;
        float top = rect.yMax;

        int vv = Mathf.Max(2, verticalLineCount);
        for (int i = 0; i < vv; i++)
        {
            float t = i / (float)(vv - 1);
            float x = Mathf.Lerp(left, right, t);
            AddThickLine(vh, new Vector2(x, bottom), new Vector2(x, top), lineThicknessPx, c);
        }

        int hh = Mathf.Max(2, horizontalLineCount);
        for (int j = 0; j < hh; j++)
        {
            float t = j / (float)(hh - 1);
            float y = Mathf.Lerp(bottom, top, t);
            AddThickLine(vh, new Vector2(left, y), new Vector2(right, y), lineThicknessPx, c);
        }
    }

    static void AddThickLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
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
