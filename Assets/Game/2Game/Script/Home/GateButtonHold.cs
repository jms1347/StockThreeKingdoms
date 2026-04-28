using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대문 버튼: 누르는 동안 GoldPerClick을 초당 비율로 지급 (첫 프레임은 OnGateClick으로 1회 탭 처리).
/// </summary>
[RequireComponent(typeof(Button))]
public class GateButtonHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
{
    public HomeController controller;
    [Tooltip("대문 터치 시 창고 더미가 있으면 비행 수거")]
    public CollectionManager collectionManager;

    Coroutine _holdCoroutine;
    float _holdStartTime;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controller == null) return;

        controller.OnGateClick();
        collectionManager?.PlayGateTapFlyFeedback(transform as RectTransform);
        collectionManager?.TryCollectFromGate();
        _holdStartTime = Time.time;
        if (_holdCoroutine != null) StopCoroutine(_holdCoroutine);
        _holdCoroutine = StartCoroutine(HoldLoop());
    }

    /// <summary>
    /// 일부 환경에서 OnPointerDown이 누락될 때 Button onClick으로 1회 탭·창고 수거를 보강합니다.
    /// (PointerDown이 이미 돌아가면 홀드 코루틴이 있으므로 중복하지 않음)
    /// </summary>
    public void OnGateTapFromButton()
    {
        if (controller == null) return;
        if (_holdCoroutine != null) return;
        controller.OnGateClick();
        collectionManager?.PlayGateTapFlyFeedback(transform as RectTransform);
        collectionManager?.TryCollectFromGate();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 모바일에서 손가락 미세 이동만으로도 드래그로 간주되어 EventSystem이
        // PointerUp을 강제로 보냅니다. 실제로 손가락/버튼이 떨어졌을 때만 홀드를 끕니다.
        if (IsPhysicalPointerStillPressed(eventData))
            return;
        StopHold();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (eventData is PointerEventData ped && IsPhysicalPointerStillPressed(ped))
            return;
        StopHold();
    }

    /// <summary>
    /// UI 모듈이 보낸 PointerUp/Cancel과 무관하게, 하드웨어 입력이 아직 눌린 상태인지 확인합니다.
    /// </summary>
    static bool IsPhysicalPointerStillPressed(PointerEventData e)
    {
        if (e.pointerId >= 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId != e.pointerId)
                    continue;
                return t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }

            return false;
        }

        return Input.GetMouseButton(0);
    }

    void StopHold()
    {
        if (_holdCoroutine != null)
        {
            StopCoroutine(_holdCoroutine);
            _holdCoroutine = null;
        }
        controller?.OnGateHoldEnd();
    }

    IEnumerator HoldLoop()
    {
        // 짧은 탭은 OnPointerDown의 1회만; 길게 누를 때만 연속 지급(탭+즉시 홀드 중복 완화)
        const float holdRepeatDelaySec = 0.12f;
        while (true)
        {
            yield return null;
            float holdDuration = Time.time - _holdStartTime;
            if (holdDuration < holdRepeatDelaySec)
                continue;
            controller?.OnGateHoldFrame(holdDuration);
        }
    }
}
