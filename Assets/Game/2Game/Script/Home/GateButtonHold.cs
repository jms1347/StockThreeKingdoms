using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대문 버튼: 누르는 동안 GoldPerClick을 초당 비율로 지급 (첫 프레임은 OnGateClick으로 1회 탭 처리).
/// </summary>
[RequireComponent(typeof(Button))]
public class GateButtonHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public HomeController controller;

    Coroutine _holdCoroutine;
    float _holdStartTime;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controller == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        controller.OnGateClick();
        _holdStartTime = Time.time;
        if (_holdCoroutine != null) StopCoroutine(_holdCoroutine);
        _holdCoroutine = StartCoroutine(HoldLoop());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopHold();
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
        // 첫 탭은 OnPointerDown에서 이미 처리됨 → 이후 프레임부터 누른 시간에 따라 가속
        while (true)
        {
            yield return null;
            float holdDuration = Time.time - _holdStartTime;
            controller?.OnGateHoldFrame(holdDuration);
        }
    }
}
