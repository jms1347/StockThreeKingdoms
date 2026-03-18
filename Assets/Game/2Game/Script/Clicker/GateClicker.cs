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
            goldText.text = "자본: " + FormatAbbreviated(GameManager.Instance.currentGold);
    }

    static string FormatAbbreviated(double value)
    {
        if (value >= 1e12) return (value / 1e12).ToString("0.#") + "T";
        if (value >= 1e9) return (value / 1e9).ToString("0.#") + "G";
        if (value >= 1e6) return (value / 1e6).ToString("0.#") + "M";
        if (value >= 1e3) return (value / 1e3).ToString("0.#") + "K";
        return value.ToString("N0");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!DataManager.Instance.IsReady) return; // 로딩 중 클릭 방지

        // Manager에서 현재 레벨에 맞는 데이터를 직접 꺼내옵니다.
        LevelRuleData data = DataManager.Instance.GetLevelData(GameManager.Instance.clickPowerLevel);
        if (data != null) GameManager.Instance.AddGold(data.clickPowerValue);

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
            LevelRuleData data = DataManager.Instance.GetLevelData(GameManager.Instance.clickPowerLevel);
            if (data != null)
            {
                double goldToAdd = data.clickPowerValue * Time.deltaTime;
                GameManager.Instance.AddGold(goldToAdd);
            }
            yield return null;
        }
    }
}