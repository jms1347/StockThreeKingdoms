using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class GateClicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI 연결")]
    public TextMeshProUGUI goldText;

    private Coroutine holdCoroutine;

    void Update()
    {
        if (goldText != null && GameManager.Instance != null)
            goldText.text = "자본: " + Utils.AbbreviateScore(GameManager.Instance.currentGold);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance == null) return;

        double value = GameManager.Instance.GetClickPowerValue(GameManager.Instance.clickPowerLevel);
        if (value > 0) GameManager.Instance.AddGold(value);

        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(AddGoldOverTime());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
    }

    private IEnumerator AddGoldOverTime()
    {
        while (true)
        {
            double value = GameManager.Instance.GetClickPowerValue(GameManager.Instance.clickPowerLevel);
            if (value > 0)
                GameManager.Instance.AddGold(value * Time.deltaTime);
            yield return null;
        }
    }
}