using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 반투명 배경(딤) 탭 시에만 닫기. 루트를 <see cref="UnityEngine.UI.Button"/>으로 두면
/// 자식 Slider/Button과 이벤트가 꼬여 드래그 중 창이 닫히는 경우가 있어 분리합니다.
/// </summary>
public sealed class SupplyDialogDimClose : MonoBehaviour, IPointerClickHandler
{
    RectTransform _panelRect;
    Action _onDimClick;

    public void Configure(RectTransform dialogPanel, Action onDimClick)
    {
        _panelRect = dialogPanel;
        _onDimClick = onDimClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_panelRect == null || _onDimClick == null) return;
        Camera cam = eventData.pressEventCamera;
        if (RectTransformUtility.RectangleContainsScreenPoint(_panelRect, eventData.position, cam))
            return;
        _onDimClick();
    }
}
