using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>빈 곳을 드래그해 직교 카메라를 이동하고, 휠·핀치로 줌합니다. 성(<see cref="Castle"/>) 또는 UI 위에서 시작하면 팬하지 않습니다.</summary>
[RequireComponent(typeof(Camera))]
public class WorldMapCameraPanController : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] bool enableTouch = true;

    [Header("줌 (직교 시야)")]
    [Tooltip("가장 확대(가까이)했을 때 orthographicSize 하한.")]
    [SerializeField] float orthographicSizeMin = 0.9f;
    [Tooltip("가장 축소했을 때 상한. 맵 밖이 보이지 않도록 레이아웃에서 추가로 잘립니다.")]
    [SerializeField] float orthographicSizeMax = 12f;
    [Tooltip("마우스 휠 한 노치당 orthographicSize 변화량(스크롤 업이 확대).")]
    [SerializeField] float zoomScrollSensitivity = 0.65f;
    [Tooltip("두 손가락 거리 변화 1픽셀당 orthographicSize 변화.")]
    [SerializeField] float pinchZoomSensitivity = 0.012f;

    Vector3 _lastScreenPos;
    bool _panning;
    bool _blockedBecauseCastle;
    float _lastPinchScreenDist = -1f;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.orthographic) return;

        HandlePointer();
        HandleZoom();
        WorldMapCameraBounds.ClampOrthographicSize(targetCamera, orthographicSizeMin, orthographicSizeMax);
        WorldMapCameraBounds.ClampCamera(targetCamera);
    }

    void HandlePointer()
    {
        if (Input.touchSupported && enableTouch && Input.touchCount >= 2)
        {
            _panning = false;
            return;
        }

        if (Input.touchSupported && enableTouch && Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                _lastScreenPos = t.position;
                bool overUi = EventSystem.current != null &&
                               EventSystem.current.IsPointerOverGameObject(t.fingerId);
                _blockedBecauseCastle = HitCastle(t.position) || overUi;
                _panning = !_blockedBecauseCastle;
            }
            else if (t.phase == TouchPhase.Moved && _panning)
            {
                PanByScreenDelta(t.position - (Vector2)_lastScreenPos);
                _lastScreenPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                _panning = false;
                _blockedBecauseCastle = false;
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            _lastScreenPos = Input.mousePosition;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
            _blockedBecauseCastle = HitCastle(Input.mousePosition) || overUi;
            _panning = !_blockedBecauseCastle;
        }
        else if (Input.GetMouseButton(0) && _panning)
        {
            var now = Input.mousePosition;
            PanByScreenDelta(now - _lastScreenPos);
            _lastScreenPos = now;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _panning = false;
            _blockedBecauseCastle = false;
        }
    }

    void HandleZoom()
    {
        if (targetCamera == null || !targetCamera.orthographic) return;

        if (Input.touchSupported && enableTouch && Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            var p0 = (Vector2)t0.position;
            var p1 = (Vector2)t1.position;
            float dist = Vector2.Distance(p0, p1);
            var mid = (p0 + p1) * 0.5f;

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || _lastPinchScreenDist < 1f)
                _lastPinchScreenDist = dist;
            else
            {
                float delta = dist - _lastPinchScreenDist;
                _lastPinchScreenDist = dist;
                if (Mathf.Abs(delta) > 0.5f)
                    ApplyZoomAtScreenPoint(-delta * pinchZoomSensitivity, new Vector3(mid.x, mid.y, 0f));
            }

            return;
        }

        _lastPinchScreenDist = -1f;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1))
            return;

        ApplyZoomAtScreenPoint(-scroll * zoomScrollSensitivity, Input.mousePosition);
    }

    void ApplyZoomAtScreenPoint(float orthoDelta, Vector3 screenPoint)
    {
        if (targetCamera == null) return;

        float zDist = -targetCamera.transform.position.z;
        var before = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDist));

        float next = Mathf.Max(0.01f, targetCamera.orthographicSize + orthoDelta);
        targetCamera.orthographicSize = next;

        WorldMapCameraBounds.ClampOrthographicSize(targetCamera, orthographicSizeMin, orthographicSizeMax);

        var after = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDist));
        var d = before - after;
        var p = targetCamera.transform.position;
        targetCamera.transform.position = new Vector3(p.x + d.x, p.y + d.y, p.z);
    }

    void PanByScreenDelta(Vector3 screenDelta)
    {
        float zDist = -targetCamera.transform.position.z;
        var w0 = targetCamera.ScreenToWorldPoint(new Vector3(0f, 0f, zDist));
        var w1 = targetCamera.ScreenToWorldPoint(new Vector3(screenDelta.x, screenDelta.y, zDist));
        var worldDelta = w1 - w0;
        var p = targetCamera.transform.position;
        targetCamera.transform.position = new Vector3(p.x - worldDelta.x, p.y - worldDelta.y, p.z);
    }

    bool HitCastle(Vector3 screenPos)
    {
        if (targetCamera == null) return false;
        float z = -targetCamera.transform.position.z;
        var w = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        var hit = Physics2D.OverlapPoint(w);
        return hit != null && hit.GetComponentInParent<Castle>() != null;
    }
}
