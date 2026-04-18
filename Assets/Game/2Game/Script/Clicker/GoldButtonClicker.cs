using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 버튼에 장착하는 클리커. 클릭 시 노동력 레벨(UpgradeButton ClickPower)에 따른 금화 추가.
/// GameManager 유저데이터(금화)와 연동
/// </summary>
[RequireComponent(typeof(Button))]
public class GoldButtonClicker : MonoBehaviour
{
    static readonly Color GoldDebtColor = new Color(1f, 0f, 0f);

    [Header("UI 연결 (선택)")]
    [Tooltip("금화 표시 텍스트 - 연결 시 자동 갱신")]
    public TextMeshProUGUI goldText;

    Button _button;

    void Start()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClickAddGold);

        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged += RefreshResourceUI;

        RefreshResourceUI(GameManager.Instance?.currentGold ?? 0d);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged -= RefreshResourceUI;
    }

    public void OnClickAddGold()
    {
        if (GameManager.Instance == null) return;

        int lv = GameManager.Instance.clickPowerLevel;
        double value = GameManager.Instance.GetClickPowerValue(lv);
        if (value > 0)
            GameManager.Instance.AddGold(value);
    }

    void RefreshResourceUI(double _)
    {
        if (GameManager.Instance == null) return;

        if (goldText != null)
        {
            goldText.text = "금화: " + Utils.AbbreviateScore(GameManager.Instance.currentGold);
            goldText.color = GameManager.Instance.IsInDebt ? GoldDebtColor : Color.white;
        }
    }
}
