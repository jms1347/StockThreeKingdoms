using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>천하 지도 ScrollRect — 마우스 휠·핀치로 콘텐츠 크기(줌) 조절. <see cref="RectTransform.sizeDelta"/>만 변경합니다.</summary>
[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
public class WorldMarketMapScrollZoom : MonoBehaviour, IScrollHandler
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform zoomTarget;
    [Tooltip("최소 배율(너무 축소되면 핀이 모두 한 덩어리로 보임).")]
    [SerializeField] float minScale = 0.55f;
    [Tooltip("최대 배율(성·길을 자세히 보려면 높게).")]
    [SerializeField] float maxScale = 6.5f;
    [SerializeField] float wheelZoomSensitivity = 0.14f;
    [Tooltip("처음 열릴 때 확대 비율(1이면 위저드 기본 맵 크기 그대로 — 요약 느낌). 1.6~2 권장.")]
    [SerializeField] float defaultZoom = 1.75f;
    [Tooltip("레거시 Input Manager 휠 — ScrollRect가 스크롤을 가로채도 확대가 되도록 Update에서 보조.")]
    [SerializeField] bool useLegacyMouseWheelZoom = true;
    [SerializeField] float legacyWheelMultiplier = 0.42f;

    [Header("UI +/- 버튼 (선택)")]
    [Tooltip("지도 중앙 기준 한 단계 확대·축소 배율.")]
    [SerializeField] float buttonZoomStepFactor = 1.12f;

    Vector2 _baseSize;
    float _zoom = 1f;
    float _lastPinchDist = -1f;
    bool _appliedInitialZoom;

    void Awake()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (zoomTarget == null && scrollRect != null)
            zoomTarget = scrollRect.content;
        RefreshBaseSize();
    }

    void Start()
    {
        RefreshBaseSize();
        ApplyInitialZoom();
    }

    void RefreshBaseSize()
    {
        if (zoomTarget == null) return;
        if (_zoom > 1e-4f)
            _baseSize = zoomTarget.sizeDelta / Mathf.Max(_zoom, 0.001f);
        else
            _baseSize = zoomTarget.sizeDelta;
    }

    void ApplyInitialZoom()
    {
        if (_appliedInitialZoom || zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        RefreshBaseSize();
        if (_baseSize.sqrMagnitude < 1f)
            return;

        float targetZoom = Mathf.Clamp(defaultZoom, minScale, maxScale);
        if (Mathf.Abs(targetZoom - 1f) < 0.02f)
        {
            _appliedInitialZoom = true;
            return;
        }

        var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        ApplyZoomFactorAbsolute(targetZoom, screenCenter);
        _appliedInitialZoom = true;
    }

    void LateUpdate()
    {
        if (zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            Vector2 a = t0.position;
            Vector2 b = t1.position;
            float dist = Vector2.Distance(a, b);
            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                _lastPinchDist = dist;
                return;
            }

            if (_lastPinchDist > 1f && dist > 1f)
            {
                float ratio = Mathf.Clamp(dist / _lastPinchDist, 0.92f, 1.08f);
                _lastPinchDist = dist;
                ApplyZoomFactor(ratio, (a + b) * 0.5f);
            }
        }
        else
            _lastPinchDist = -1f;
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        if (!useLegacyMouseWheelZoom || zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) < 1e-5f)
            return;
        float factor = 1f + wheel * legacyWheelMultiplier;
        ApplyZoomFactor(factor, Input.mousePosition);
    }
#endif

    public void OnScroll(PointerEventData eventData)
    {
        float delta = eventData.scrollDelta.y;
        if (Mathf.Abs(delta) < 0.01f)
            return;
        float factor = 1f + Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta) * wheelZoomSensitivity * 0.12f, 0.35f);
        ApplyZoomFactor(factor, eventData.position);
    }

    /// <summary>뷰포트 중심 기준 확대(모바일 지도 오른쪽 상단 + 버튼).</summary>
    public void ZoomInStep()
    {
        float f = Mathf.Clamp(buttonZoomStepFactor, 1.02f, 1.5f);
        ApplyZoomFactor(f, GetViewportCenterScreenPoint());
    }

    /// <summary>뷰포트 중심 기준 축소(모바일 지도 오른쪽 상단 − 버튼).</summary>
    public void ZoomOutStep()
    {
        float f = Mathf.Clamp(buttonZoomStepFactor, 1.02f, 1.5f);
        ApplyZoomFactor(1f / f, GetViewportCenterScreenPoint());
    }

    Vector2 GetViewportCenterScreenPoint()
    {
        if (scrollRect == null || scrollRect.viewport == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        var vp = scrollRect.viewport;
        Vector3 world = vp.TransformPoint(vp.rect.center);
        Canvas canvas = scrollRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        return RectTransformUtility.WorldToScreenPoint(cam, world);
    }

    void ApplyZoomFactor(float factor, Vector2 screenPoint)
    {
        if (zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        if (_baseSize.sqrMagnitude < 1f)
            RefreshBaseSize();

        float newZoom = Mathf.Clamp(_zoom * factor, minScale, maxScale);
        if (Mathf.Abs(newZoom - _zoom) < 1e-4f)
            return;

        Camera cam = null;
        var canvas = scrollRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollRect.viewport, screenPoint, cam, out Vector2 before);

        _zoom = newZoom;
        zoomTarget.sizeDelta = _baseSize * _zoom;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollRect.viewport, screenPoint, cam, out Vector2 after);

        zoomTarget.anchoredPosition += (after - before);
    }

    void ApplyZoomFactorAbsolute(float absoluteZoom, Vector2 screenPoint)
    {
        if (zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        if (_baseSize.sqrMagnitude < 1f)
            RefreshBaseSize();

        float newZoom = Mathf.Clamp(absoluteZoom, minScale, maxScale);
        if (Mathf.Abs(newZoom - _zoom) < 1e-4f)
            return;

        Camera cam = null;
        var canvas = scrollRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollRect.viewport, screenPoint, cam, out Vector2 before);

        _zoom = newZoom;
        zoomTarget.sizeDelta = _baseSize * _zoom;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollRect.viewport, screenPoint, cam, out Vector2 after);

        zoomTarget.anchoredPosition += (after - before);
    }
}
