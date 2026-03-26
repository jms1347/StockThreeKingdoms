using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼을 누르고 있으면 첫 1회 실행 후, 일정 간격으로 액션을 반복합니다.
/// 재화가 부족해져서 액션이 더 이상 효과가 없을 때까지 반복하는 용도(업그레이드·구매 등).
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonHoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("첫 반복까지 대기 (초)")]
    public float firstRepeatDelay = 0.35f;
    [Tooltip("이후 반복 간격 (초)")]
    public float repeatInterval = 0.06f;

    Action _tick;
    Coroutine _co;

    /// <summary>홀드 시마다 호출할 콜백 (메인 스레드)</summary>
    public void Configure(Action tick)
    {
        _tick = tick;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_tick == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _tick.Invoke();
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RepeatLoop());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopRepeat();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopRepeat();
    }

    void OnDisable()
    {
        StopRepeat();
    }

    void StopRepeat()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
    }

    IEnumerator RepeatLoop()
    {
        if (firstRepeatDelay > 0f)
            yield return new WaitForSecondsRealtime(firstRepeatDelay);

        var wait = new WaitForSecondsRealtime(Mathf.Max(0.02f, repeatInterval));
        while (true)
        {
            _tick?.Invoke();
            yield return wait;
        }
    }
}
