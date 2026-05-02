using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>성벽 메인 버튼 — 탭 1회마다 <see cref="HomeController.OnWallClicked"/> (주머니+노동 수익 수거).</summary>
[RequireComponent(typeof(Button))]
public class GateButtonHold : MonoBehaviour, IPointerDownHandler
{
    public HomeController controller;
    public CollectionManager collectionManager;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controller == null) return;
        controller.OnWallClicked(collectionManager);
    }
}
