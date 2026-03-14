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
        if (goldText != null)
        {
            // 🔥 double 형을 그대로 포맷팅하여 21억 오버플로우 방지
            goldText.text = "자본: " + CapitalManager.Instance.currentGold.ToString("N0");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!DataManager.Instance.IsReady) return; // 로딩 중 클릭 방지

        // Manager에서 현재 레벨에 맞는 데이터를 직접 꺼내옵니다.
        LevelRuleData data = DataManager.Instance.GetLevelData(CapitalManager.Instance.clickPowerLevel);
        if (data != null) CapitalManager.Instance.AddGold(data.clickPowerValue);

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
            LevelRuleData data = DataManager.Instance.GetLevelData(CapitalManager.Instance.clickPowerLevel);
            if (data != null)
            {
                double goldToAdd = data.clickPowerValue * Time.deltaTime;
                CapitalManager.Instance.AddGold(goldToAdd);
            }
            yield return null;
        }
    }
}