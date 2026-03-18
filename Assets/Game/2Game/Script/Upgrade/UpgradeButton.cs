using System;
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

    // 비용을 캐싱해두어 골드 변동 시마다 데이터를 다시 찾지 않게 합니다.
    private double cachedCost; 

    void Start()
    {
        if (costText != null) costText.text = "";
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged += HandleGoldChanged;
            InitializeUI();
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGoldChanged -= HandleGoldChanged;
    }

    private void InitializeUI()
    {
        UpdateUpgradeUI();
        HandleGoldChanged(GameManager.Instance.currentGold);
    }

    private void UpdateUpgradeUI()
    {
        if (GameManager.Instance == null) return;

        int currentLevel = GetCurrentLevel();
        cachedCost = GetCurrentCost();

        if (levelText != null) levelText.text = $"Lv. {currentLevel}";

        if (effectText != null)
        {
            switch (type)
            {
                case UpgradeType.ClickPower:
                    effectText.text = $"클릭 파워: +{Utils.AbbreviateScore(GameManager.Instance.GetClickPowerValue(currentLevel))}";
                    break;
                case UpgradeType.AutoIncome:
                    effectText.text = $"자동 수익: 초당 +{Utils.AbbreviateScore(GameManager.Instance.GetAutoIncomeValue(currentLevel))}";
                    break;
                case UpgradeType.SoldierGrade:
                    effectText.text = $"병사 효율 x{GameManager.Instance.GetSoldierGradeMultiplier(currentLevel):F1}";
                    break;
            }
        }

        if (costText != null)
            costText.text = Utils.AbbreviateScore(cachedCost) + " 골드";
    }

    private void HandleGoldChanged(double currentGold)
    {
        if (myButton != null)
            myButton.interactable = currentGold >= cachedCost;
    }

    public void OnUpgradeClicked()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.currentGold >= cachedCost)
        {
            // 골드 차감 (Property의 setter를 통해 자동으로 OnGoldChanged 이벤트가 발생함)
            GameManager.Instance.currentGold -= cachedCost;

            switch (type)
            {
                case UpgradeType.ClickPower: GameManager.Instance.clickPowerLevel++; break;
                case UpgradeType.AutoIncome: GameManager.Instance.autoIncomeLevel++; break;
                case UpgradeType.SoldierGrade: GameManager.Instance.soldierGradeLevel++; break;
            }

            // 레벨이 올랐으므로 UI 전체 갱신
            UpdateUpgradeUI();
            
            // UI 갱신 후 바뀐 비용으로 버튼 상태 재검사
            HandleGoldChanged(GameManager.Instance.currentGold);
        }
    }

    private int GetCurrentLevel()
    {
        switch (type)
        {
            case UpgradeType.ClickPower: return GameManager.Instance.clickPowerLevel;
            case UpgradeType.AutoIncome: return GameManager.Instance.autoIncomeLevel;
            case UpgradeType.SoldierGrade: return GameManager.Instance.soldierGradeLevel;
            default: return 1;
        }
    }

    private double GetCurrentCost()
    {
        int lv = GetCurrentLevel();
        switch (type)
        {
            case UpgradeType.ClickPower: return GameManager.Instance.GetClickPowerCost(lv);
            case UpgradeType.AutoIncome: return GameManager.Instance.GetAutoIncomeCost(lv);
            case UpgradeType.SoldierGrade: return GameManager.Instance.GetSoldierGradeCost(lv);
            default: return double.MaxValue;
        }
    }
}