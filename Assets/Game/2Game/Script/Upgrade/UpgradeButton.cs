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
        // 초기 텍스트 설정
        if (costText != null) costText.text = "로딩중...";
        if (myButton != null) myButton.interactable = false;

        // 1. 데이터 매니저 이벤트 구독
        if (DataManager.Instance.IsReady)
        {
            InitializeUI();
        }
        else
        {
            DataManager.Instance.OnDataReady += InitializeUI;
        }

        // 2. 골드 변경 이벤트 구독
        if (CapitalManager.Instance != null)
        {
            CapitalManager.Instance.OnGoldChanged += HandleGoldChanged;
        }
    }

    void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 구독 해제 (매우 중요)
        if (DataManager.Instance != null)
        {
            DataManager.Instance.OnDataReady -= InitializeUI;
        }
        if (CapitalManager.Instance != null)
        {
            CapitalManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    private void InitializeUI()
    {
        UpdateUpgradeUI();
        HandleGoldChanged(CapitalManager.Instance.currentGold);
    }

    // [무거운 작업] 레벨, 비용, 효과 텍스트를 업데이트 (레벨업 시, 데이터 로드 시에만 호출)
    private void UpdateUpgradeUI()
    {
        if (!DataManager.Instance.IsReady) return;

        int currentLevel = GetCurrentLevel();
        LevelRuleData data = DataManager.Instance.GetLevelData(currentLevel);
        
        cachedCost = GetCurrentCost(); // 다음 레벨 비용 캐싱

        if (levelText != null) levelText.text = $"Lv. {currentLevel}";

        if (effectText != null && data != null)
        {
            switch (type)
            {
                case UpgradeType.ClickPower:
                    effectText.text = $"클릭 파워: +{data.clickPowerValue}";
                    break;
                case UpgradeType.AutoIncome:
                    effectText.text = $"자동 수익: 초당 +{data.autoIncomeValue}";
                    break;
                case UpgradeType.SoldierGrade:
                    effectText.text = $"병사 투자 효율 증가";
                    break;
            }
        }

        if (costText != null)
        {
            if (data == null) costText.text = "MAX";
            else costText.text = $"{cachedCost:N0} 골드";
        }
    }

    // [가벼운 작업] 버튼의 활성화 여부만 체크 (골드가 바뀔 때마다 호출)
    private void HandleGoldChanged(double currentGold)
    {
        if (!DataManager.Instance.IsReady) return;

        // 매번 데이터를 찾지 않고 캐싱된 비용(cachedCost)과 비교
        bool isMaxLevel = DataManager.Instance.GetLevelData(GetCurrentLevel()) == null;
        if (myButton != null) myButton.interactable = (!isMaxLevel) && (currentGold >= cachedCost);
    }

    public void OnUpgradeClicked()
    {
        if (!DataManager.Instance.IsReady) return;

        if (CapitalManager.Instance.currentGold >= cachedCost)
        {
            // 골드 차감 (Property의 setter를 통해 자동으로 OnGoldChanged 이벤트가 발생함)
            CapitalManager.Instance.currentGold -= cachedCost;

            switch (type)
            {
                case UpgradeType.ClickPower: CapitalManager.Instance.clickPowerLevel++; break;
                case UpgradeType.AutoIncome: CapitalManager.Instance.autoIncomeLevel++; break;
                case UpgradeType.SoldierGrade: CapitalManager.Instance.soldierGradeLevel++; break;
            }

            // 레벨이 올랐으므로 UI 전체 갱신
            UpdateUpgradeUI();
            
            // UI 갱신 후 바뀐 비용으로 버튼 상태 재검사
            HandleGoldChanged(CapitalManager.Instance.currentGold);
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