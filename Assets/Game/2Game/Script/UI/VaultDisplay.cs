using UnityEngine;
using TMPro;

/// <summary> 금고 실시간 카운팅 연출. Text만 Time에 맞춰 연산, 실제 데이터 수정 없음. 성능 부하 거의 없음. </summary>
public class VaultDisplay : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI capacityText;
    [Tooltip("버튼 위 충만감 연출 예: ●●●○○ (선택)")]
    public TextMeshProUGUI fillBarText;

    void Update()
    {
        if (GameManager.Instance == null) return;

        double display = GameManager.Instance.GetAccumulatedMarketGold();
        double maxCap = GameManager.Instance.GetMarketMaxCapacity();

        if (amountText != null)
            amountText.text = "⚙️ " + Utils.AbbreviateScore(display);

        if (capacityText != null && maxCap > 0)
            capacityText.text = "/ " + Utils.AbbreviateScore(maxCap);

        if (fillBarText != null && maxCap > 0)
        {
            float ratio = (float)Mathd.Clamp01(display / maxCap);
            int filled = Mathf.RoundToInt(ratio * 5);
            fillBarText.text = new string('●', filled) + new string('○', 5 - filled);
        }
    }
}

/// <summary> double용 Clamp01 </summary>
public static class Mathd
{
    public static double Clamp01(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}
