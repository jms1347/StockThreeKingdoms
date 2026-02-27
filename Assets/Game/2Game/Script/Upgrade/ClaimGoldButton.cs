using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClaimGoldButton : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI accumulatedText; // 창고에 쌓인 돈 표시 텍스트
    public Button claimButton;              // 수거 버튼 자기 자신

    void Update()
    {
        // Manager에서 현재 창고에 쌓인 돈 가져오기
        double amount = CapitalManager.Instance.accumulatedGold;

        if (accumulatedText != null)
        {
            // (예) "세금 징수 가능: 1,500 골드"
            accumulatedText.text = $"징수 가능: {Mathf.FloorToInt((float)amount):N0} 골드";
        }

        // 모인 돈이 1 이상일 때만 수거 버튼 누를 수 있게 활성화
        if (claimButton != null)
        {
            claimButton.interactable = amount >= 1.0;
        }
    }

    // 유저가 [징수] 버튼을 눌렀을 때 실행되는 함수 (Inspector에서 OnClick에 연결)
    public void OnClaimClicked()
    {
        // 돈 수거!
        CapitalManager.Instance.ClaimAccumulatedGold();
        Debug.Log("시장 수익 징수 완료!");

        // TODO: UI 매니저를 호출해 화면 위로 황금 엽전들이 쏟아지는 파티클 효과 추가!
    }
}