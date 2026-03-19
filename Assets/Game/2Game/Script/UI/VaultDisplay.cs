using UnityEngine;
using TMPro;

/// <summary> 금고 실시간 카운팅 연출. HomeController에서 동적 계산값 조회. </summary>
public class VaultDisplay : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI capacityText;
    [Tooltip("버튼 위 충만감 연출 예: ●●●○○ (선택)")]
    public TextMeshProUGUI fillBarText;

    HomeController _homeController;

    void Start()
    {
        _homeController = FindObjectOfType<HomeController>();
    }

    void Update()
    {
        if (_homeController == null) return;

        double display = _homeController.CurrentMarketAccumulated;
        double maxCap = _homeController.GetMarketMaxCapacity();

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
