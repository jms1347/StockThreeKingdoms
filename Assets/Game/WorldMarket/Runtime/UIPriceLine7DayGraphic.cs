using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>최근 7일 시세용 단일 라인 + 하단 그라데이션 영역(MTS 상세 차트). 선택적으로 지지/저항 가격대 수평선.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIPriceLine7DayGraphic : MaskableGraphic, IPointerClickHandler
{
    [SerializeField] float lineThickness = 2f;
    [SerializeField, FormerlySerializedAs("lineColor")]
    Color lineColorSerialized = new Color(0.45f, 0.78f, 1f, 0.95f);
    [SerializeField, FormerlySerializedAs("fillTopColor")]
    Color fillTopColorSerialized = new Color(0.2f, 0.45f, 0.72f, 0.35f);
    [SerializeField, FormerlySerializedAs("fillBottomColor")]
    Color fillBottomColorSerialized = new Color(0.06f, 0.08f, 0.12f, 0.15f);

    /// <summary>코드에서 트렌드색 등 동적 변경 시 사용.</summary>
    public Color lineColor
    {
        get => lineColorSerialized;
        set { lineColorSerialized = value; SetVerticesDirty(); }
    }

    public Color fillTopColor
    {
        get => fillTopColorSerialized;
        set { fillTopColorSerialized = value; SetVerticesDirty(); }
    }

    public Color fillBottomColor
    {
        get => fillBottomColorSerialized;
        set { fillBottomColorSerialized = value; SetVerticesDirty(); }
    }

    float[] _norm;
    float[] _values;
    float _minV;
    float _rangeV;
    float _wallSuY = -1f;
    float _wallReY = -1f;

    /// <summary>Y축(차트 영역) 클릭 시 해당 높이의 가격(G). 입성료 표시 등에 사용.</summary>
    public event Action<float> PriceClicked;

    static readonly Color32 WallSupportColor = new Color32(90, 200, 255, 130);
    static readonly Color32 WallResistColor = new Color32(255, 170, 70, 130);

    protected override void Awake()
    {
        base.Awake();
        if (GetComponent<CanvasRenderer>() == null)
            gameObject.AddComponent<CanvasRenderer>();
        raycastTarget = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_norm == null || _norm.Length < 2 || _rangeV < 1e-6f)
            return;
        var r = rectTransform.rect;
        float h = r.height;
        float padY = 6f;
        float innerH = h - padY * 2f;
        if (innerH < 2f)
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out var local))
            return;
        float vn = Mathf.InverseLerp(padY, padY + innerH, local.y);
        float price = _minV + Mathf.Clamp01(vn) * _rangeV;
        PriceClicked?.Invoke(Mathf.Max(1f, price));
    }

    public void SetPrices(IReadOnlyList<float> sevenDailyCloses)
    {
        _values = null;
        _norm = null;
        _wallSuY = _wallReY = -1f;
        _minV = 0f;
        _rangeV = 1f;

        if (sevenDailyCloses == null || sevenDailyCloses.Count < 1)
        {
            SetVerticesDirty();
            return;
        }

        int rawCount = Mathf.Min(7, sevenDailyCloses.Count);
        var buf = new float[rawCount];
        for (int i = 0; i < rawCount; i++)
            buf[i] = Mathf.Max(1f, sevenDailyCloses[i]);

        if (rawCount == 1)
        {
            float x = buf[0];
            buf = new[] { x * 0.995f, x };
            rawCount = 2;
        }

        int n = rawCount;
        _values = new float[n];
        for (int i = 0; i < n; i++)
            _values[i] = buf[i];

        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            if (_values[i] < min) min = _values[i];
            if (_values[i] > max) max = _values[i];
        }

        float range = Mathf.Max(max * 0.002f, max - min);
        _minV = min;
        _rangeV = range;
        _norm = new float[n];
        for (int i = 0; i < n; i++)
            _norm[i] = (_values[i] - min) / range;

        SetVerticesDirty();
    }

    /// <summary>차트 Y축과 동일 스케일로 지지(아래)·저항(위) 가격대를 반투명 수평선으로 표시합니다.</summary>
    public void SetPsychologicalWallPrices(float supportPrice, float resistPrice)
    {
        if (_norm == null || _norm.Length < 2 || _rangeV < 1e-6f)
        {
            _wallSuY = _wallReY = -1f;
            SetVerticesDirty();
            return;
        }

        _wallSuY = Mathf.Clamp01((supportPrice - _minV) / _rangeV);
        _wallReY = Mathf.Clamp01((resistPrice - _minV) / _rangeV);
        SetVerticesDirty();
    }

    public void ClearPsychologicalWalls()
    {
        _wallSuY = _wallReY = -1f;
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

        var fillBot = (Color32)fillBottomColorSerialized;
        var fillTopBlend = (Color32)Color.Lerp(fillBottomColorSerialized, fillTopColorSerialized, 0.65f);

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

            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, baseY, 0f), fillBot, Vector2.zero);
            vh.AddVert(new Vector3(x1, baseY, 0f), fillBot, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1, 0f), fillTopBlend, Vector2.zero);
            vh.AddVert(new Vector3(x0, y0, 0f), fillTopBlend, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }

        if (_wallSuY >= 0f)
        {
            float y = padY + _wallSuY * innerH;
            AddThinHorizontal(vh, padX, padX + innerW, y, 1.35f, WallSupportColor);
        }

        if (_wallReY >= 0f)
        {
            float y = padY + _wallReY * innerH;
            AddThinHorizontal(vh, padX, padX + innerW, y, 1.35f, WallResistColor);
        }

        var lc = (Color32)lineColorSerialized;
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

    static void AddThinHorizontal(VertexHelper vh, float x0, float x1, float y, float thickness, Color32 col)
    {
        float half = thickness * 0.5f;
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y - half, 0f), col, Vector2.zero);
        vh.AddVert(new Vector3(x0, y + half, 0f), col, Vector2.zero);
        vh.AddVert(new Vector3(x1, y + half, 0f), col, Vector2.zero);
        vh.AddVert(new Vector3(x1, y - half, 0f), col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
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
