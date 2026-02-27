using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class GateClicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI 연결")]
    public TextMeshProUGUI goldText; // 재화 표시 텍스트

    private Coroutine holdCoroutine;

    void Update()
    {
        // UI 업데이트는 매 프레임 Manager의 현재 수치를 가져와서 표시합니다.
        if (goldText != null)
        {
            // Utils.AbbreviateScore 대신 "N0" 포맷으로 콤마 표시
            goldText.text = "자본: " + Mathf.FloorToInt((float)CapitalManager.Instance.currentGold).ToString("N0");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("성문 탭! 자본 축적 시작");

        // 1. 누르는 순간 즉시 1회 보상 (이름 변경 완료!)
        CapitalManager.Instance.AddGold(CapitalManager.Instance.GetClickPowerGold());

        // 2. 기존 코루틴 정리 및 지속 획득 코루틴 시작
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(AddGoldOverTime());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("클릭 종료");
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
            // 초당 획득량 = CapitalManager의 '클릭 파워' 기반 수익 (이름 변경 완료!)
            double goldToAdd = CapitalManager.Instance.GetClickPowerGold() * Time.deltaTime;

            // 매니저에 자본 추가
            CapitalManager.Instance.AddGold(goldToAdd);

            yield return null; // 다음 프레임 대기
        }
    }
}