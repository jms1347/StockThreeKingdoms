using UnityEngine;
using UnityEngine.EventSystems; // 포인터 이벤트를 사용하기 위해 필요합니다.
using TMPro;
using System.Collections; // 코루틴을 사용하기 위해 필요합니다.

// IPointerDownHandler: 마우스 버튼이 눌러지는 순간을 감지합니다.
// IPointerUpHandler: 마우스 버튼에서 손을 떼는 순간을 감지합니다.
public class HoldClicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // 점수 표시를 위한 TextMeshPro 컴포넌트
    public TextMeshProUGUI scoreText;

    [Header("게임 설정")]
    // 누르고 있을 때 초당 얻는 점수
    public float scorePerSecond = 10f;

    // 실제 클리커 점수
    private long score = 0;

    // 코루틴을 참조하기 위한 변수
    private Coroutine scoreCoroutine;

    void Start()
    {
        UpdateScoreDisplay();
    }

    /**
     * @brief 마우스 버튼을 누르는 순간 호출됩니다. (Pointer Down)
     */
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("클릭 시작");

        // **이전에 실행 중이던 코루틴이 있다면 중지합니다.** (안전 장치)
        if (scoreCoroutine != null)
        {
            StopCoroutine(scoreCoroutine);
        }

        // **점수 추가 코루틴을 시작하고 참조를 저장합니다.**
        scoreCoroutine = StartCoroutine(AddScoreOverTime());
    }

    /**
     * @brief 마우스 버튼에서 손을 떼는 순간 호출됩니다. (Pointer Up)
     */
    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("클릭 종료");

        // **실행 중인 코루틴이 있다면 중지합니다.**
        if (scoreCoroutine != null)
        {
            StopCoroutine(scoreCoroutine);
            scoreCoroutine = null; // 참조 해제
        }
    }

    /**
     * @brief 코루틴: 버튼이 눌려 있는 동안 점수를 프레임 단위로 추가합니다.
     */
    private IEnumerator AddScoreOverTime()
    {
        float scoreAccumulator = 0f;

        // 무한 루프를 돌면서 버튼이 눌러져 있는 동안 매 프레임 실행됩니다.
        while (true)
        {
            // Time.deltaTime: 마지막 프레임 이후 경과된 시간 (초).
            float scoreToAdd = scorePerSecond * Time.deltaTime;

            // 소수점 누적기에 점수를 더합니다.
            scoreAccumulator += scoreToAdd;

            // 누적된 점수가 1.0 이상이 되면 정수 점수로 변환합니다.
            if (scoreAccumulator >= 1.0f)
            {
                int integerScoreToAdd = Mathf.FloorToInt(scoreAccumulator);
                score += integerScoreToAdd;
                scoreAccumulator -= integerScoreToAdd;
                UpdateScoreDisplay();
            }

            // **다음 프레임까지 기다립니다.** (Update 함수와 동일하게 동작)
            yield return null;
        }
    }

    /**
     * @brief 현재 점수를 UI 텍스트에 반영합니다.
     */
    private void UpdateScoreDisplay()
    {
        // "N0" 포맷은 천 단위 구분 기호(콤마)를 표시합니다.
        // null 체크를 하여 에디터에서 실수로 TextMeshPro 컴포넌트를 연결하지 않았을 때의 오류를 방지합니다.
        if (scoreText != null)
        {
            scoreText.text = "점수: " + Utils.AbbreviateScore(score);
        }
    }
}