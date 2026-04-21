using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>천하 지도 ScrollRect — 마우스 휠·핀치로 콘텐츠 크기(줌) 조절. <see cref="RectTransform.sizeDelta"/>만 변경합니다.</summary>
[DisallowMultipleComponent]
public class WorldMarketMapScrollZoom : MonoBehaviour, IScrollHandler
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform zoomTarget;
    [SerializeField] float minScale = 0.45f;
    [SerializeField] float maxScale = 2.75f;
    [SerializeField] float wheelZoomSensitivity = 0.11f;

    Vector2 _baseSize;
    float _zoom = 1f;
    float _lastPinchDist = -1f;

    void Awake()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (zoomTarget == null && scrollRect != null)
            zoomTarget = scrollRect.content;
        if (zoomTarget != null)
            _baseSize = zoomTarget.sizeDelta;
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
                float ratio = dist / _lastPinchDist;
                _lastPinchDist = dist;
                ApplyZoomFactor(ratio, (a + b) * 0.5f);
            }
        }
        else
            _lastPinchDist = -1f;
    }

    public void OnScroll(PointerEventData eventData)
    {
        float delta = eventData.scrollDelta.y;
        if (Mathf.Abs(delta) < 0.01f)
            return;
        float factor = 1f + delta * wheelZoomSensitivity * 0.08f;
        ApplyZoomFactor(factor, eventData.position);
    }

    void ApplyZoomFactor(float factor, Vector2 screenPoint)
    {
        if (zoomTarget == null || scrollRect == null || scrollRect.viewport == null)
            return;
        if (_baseSize.sqrMagnitude < 1f)
            _baseSize = zoomTarget.sizeDelta;

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
}
