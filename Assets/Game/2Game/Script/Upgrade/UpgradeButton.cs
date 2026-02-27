using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeButton : MonoBehaviour
{
    // 🔥 Market -> AutoIncome으로 열거형 이름 변경
    public enum UpgradeType { ClickPower, AutoIncome, SoldierGrade }

    [Header("업그레이드 설정")]
    public UpgradeType type;
    public double baseCost = 50;
    public float costMultiplier = 1.15f;

    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI costText;

    public Button myButton;

    void Update()
    {
        double currentCost = GetCurrentCost();

        if (levelText != null) levelText.text = $"Lv. {GetCurrentLevel()}";

        // UI에 보여지는 텍스트도 직관적으로 수정
        if (effectText != null)
        {
            if (type == UpgradeType.ClickPower)
                effectText.text = $"클릭 파워: +{CapitalManager.Instance.GetClickPowerGold()}";
            else if (type == UpgradeType.AutoIncome) // 🔥 변경점
                effectText.text = $"자동 수익: 초당 +{CapitalManager.Instance.GetAutoGoldPerSecond()}"; // '배당금'이나 '세금'으로 적으셔도 좋습니다!
            else if (type == UpgradeType.SoldierGrade)
                effectText.text = $"병사 투자 효율 증가";
        }

        if (costText != null) costText.text = $"{Mathf.FloorToInt((float)currentCost):N0} 골드";
        myButton.interactable = CapitalManager.Instance.currentGold >= currentCost;
    }

    public void OnUpgradeClicked()
    {
        double cost = GetCurrentCost();
        if (CapitalManager.Instance.currentGold >= cost)
        {
            CapitalManager.Instance.currentGold -= cost;

            switch (type)
            {
                case UpgradeType.ClickPower: CapitalManager.Instance.clickPowerLevel++; break;
                case UpgradeType.AutoIncome: CapitalManager.Instance.autoIncomeLevel++; break; // 🔥 변경점
                case UpgradeType.SoldierGrade: CapitalManager.Instance.soldierGradeLevel++; break;
            }
        }
    }

    private int GetCurrentLevel()
    {
        switch (type)
        {
            case UpgradeType.ClickPower: return CapitalManager.Instance.clickPowerLevel;
            case UpgradeType.AutoIncome: return CapitalManager.Instance.autoIncomeLevel; // 🔥 변경점
            case UpgradeType.SoldierGrade: return CapitalManager.Instance.soldierGradeLevel;
            default: return 1;
        }
    }

    private double GetCurrentCost()
    {
        int currentLevel = GetCurrentLevel();
        return baseCost * Mathf.Pow(costMultiplier, currentLevel - 1);
    }
}