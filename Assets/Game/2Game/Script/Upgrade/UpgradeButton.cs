using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeButton : MonoBehaviour
{
    public enum UpgradeType { ClickPower, AutoIncome, SoldierGrade }

    [Header("업그레이드 설정")]
    public UpgradeType type;

    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI costText;
    public Button myButton;

    void Update()
    {
        // 1. 구글 시트 다운로드가 안 끝났으면 UI 업데이트 대기
        if (!DataManager.Instance.IsReady)
        {
            if (costText != null) costText.text = "로딩중...";
            myButton.interactable = false;
            return; // 아래 로직은 실행하지 않음
        }

        // 2. 다운로드가 끝났다면 정상 작동
        double currentCost = GetCurrentCost();
        int currentLevel = GetCurrentLevel();
        LevelRuleData data = DataManager.Instance.GetLevelData(currentLevel);

        if (levelText != null) levelText.text = $"Lv. {currentLevel}";

        if (effectText != null && data != null)
        {
            if (type == UpgradeType.ClickPower)
                effectText.text = $"클릭 파워: +{data.clickPowerValue}";
            else if (type == UpgradeType.AutoIncome)
                effectText.text = $"자동 수익: 초당 +{data.autoIncomeValue}";
            else if (type == UpgradeType.SoldierGrade)
                effectText.text = $"병사 투자 효율 증가";
        }

        // 🔥 Mathf.FloorToInt 삭제! double 형에 바로 ToString("N0")을 써서 21억 한계 돌파
        if (costText != null)
        {
            if (data == null) costText.text = "MAX"; // 데이터가 없으면 만렙 처리
            else costText.text = $"{currentCost:N0} 골드";
        }

        // 버튼 활성화 여부
        myButton.interactable = (data != null) && (CapitalManager.Instance.currentGold >= currentCost);
    }

    public void OnUpgradeClicked()
    {
        if (!DataManager.Instance.IsReady) return;

        double cost = GetCurrentCost();
        if (CapitalManager.Instance.currentGold >= cost)
        {
            CapitalManager.Instance.currentGold -= cost;

            switch (type)
            {
                case UpgradeType.ClickPower: CapitalManager.Instance.clickPowerLevel++; break;
                case UpgradeType.AutoIncome: CapitalManager.Instance.autoIncomeLevel++; break;
                case UpgradeType.SoldierGrade: CapitalManager.Instance.soldierGradeLevel++; break;
            }
        }
    }

    private int GetCurrentLevel()
    {
        switch (type)
        {
            case UpgradeType.ClickPower: return CapitalManager.Instance.clickPowerLevel;
            case UpgradeType.AutoIncome: return CapitalManager.Instance.autoIncomeLevel;
            case UpgradeType.SoldierGrade: return CapitalManager.Instance.soldierGradeLevel;
            default: return 1;
        }
    }

    private double GetCurrentCost()
    {
        LevelRuleData data = DataManager.Instance.GetLevelData(GetCurrentLevel());
        if (data == null) return double.MaxValue;

        switch (type)
        {
            case UpgradeType.ClickPower: return data.clickPowerCost;
            case UpgradeType.AutoIncome: return data.autoIncomeCost;
            case UpgradeType.SoldierGrade: return data.soldierGradeCost;
            default: return double.MaxValue;
        }
    }
}