using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>선택된 성의 스탯을 표시하는 반응형 패널.</summary>
public class CastleDetailPanel : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] Button closeButton;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text castleIdText;
    [SerializeField] TMP_Text countryText;
    [SerializeField] TMP_Text governorText;
    [SerializeField] TMP_Text armyText;
    [SerializeField] TMP_Text populationText;
    [SerializeField] TMP_Text sentimentText;
    [SerializeField] TMP_Text valueText;

    [Header("장수·지원")]
    [SerializeField] TMP_Text generalsText;
    [SerializeField] TMP_Text generalMovementText;
    [SerializeField] TMP_Text siegeSupportHintText;
    [SerializeField] Button siegeSupportButton;
    [SerializeField] TMP_Text siegeAttackSupportHintText;
    [SerializeField] Button siegeAttackSupportButton;

    Castle _bound;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        if (siegeSupportButton != null)
            siegeSupportButton.onClick.AddListener(OnSiegeDefenseSupportClicked);
        if (siegeAttackSupportButton != null)
            siegeAttackSupportButton.onClick.AddListener(OnSiegeAttackSupportClicked);
        Hide();
    }

    void OnSiegeDefenseSupportClicked()
    {
        var mm = MapManager.InstanceOrNull;
        if (mm == null || _bound == null) return;
        if (!mm.TryGetSiegeDefenseSupportOpportunity(_bound, out var ally, out _))
            return;
        if (mm.TrySendSiegeDefenseSupport(_bound, ally))
            mm.RefreshCastleDetailIfOpen();
    }

    void OnSiegeAttackSupportClicked()
    {
        var mm = MapManager.InstanceOrNull;
        if (mm == null || _bound == null) return;
        if (!mm.TryGetSiegeAttackSupportOpportunity(_bound, out var ally, out _))
            return;
        if (mm.TrySendSiegeAttackSupport(_bound, ally))
            mm.RefreshCastleDetailIfOpen();
    }

    public void Bind(Castle castle)
    {
        _bound = castle;
        if (castle == null)
        {
            Hide();
            return;
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (titleText != null)
        {
            titleText.text = castle.DisplayCastleName;
            WorldMapTmpFontSupport.Apply(titleText);
        }

        if (castleIdText != null)
            castleIdText.text = $"성 ID: {castle.CastleId}";

        if (countryText != null)
            countryText.text = $"세력: {castle.CountryDisplayName} ({castle.CountryId})";

        if (governorText != null)
            governorText.text = $"태수: {castle.GovernorName}";

        if (armyText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"군대(맵 공성): {castle.Army:N0}");
            var dm = DataManager.InstanceOrNull;
            if (dm != null &&
                !string.IsNullOrWhiteSpace(castle.MasterId) &&
                dm.castleStateDataMap != null &&
                dm.castleStateDataMap.TryGetValue(castle.MasterId.Trim(), out var st) &&
                st != null)
            {
                int garrisonTotal = st.userDeployedTroops + st.currentAiGarrison;
                sb.Append(
                    $"천하 주둔: 유저 {st.userDeployedTroops:N0} + AI {st.currentAiGarrison:N0} (합 {garrisonTotal:N0})");
            }

            armyText.text = sb.ToString().TrimEnd();
            WorldMapTmpFontSupport.Apply(armyText);
        }

        if (populationText != null)
            populationText.text = $"인구: {castle.Population:N0}";

        if (sentimentText != null)
            sentimentText.text = $"민심: {castle.PublicSentiment}";

        if (valueText != null)
            valueText.text = $"성 가치금: {castle.CastleValue:N0} (징병·구제·투자로 변동)";

        if (generalsText != null)
        {
            var sb = new StringBuilder();
            WorldMapGeneralRoster.AppendGeneralsSummary(castle, sb);
            generalsText.text = sb.ToString().TrimEnd();
            WorldMapTmpFontSupport.Apply(generalsText);
        }

        if (generalMovementText != null)
        {
            var sb = new StringBuilder();
            WorldMapGeneralRoster.AppendMovementSummary(castle, sb);
            generalMovementText.text = sb.ToString().TrimEnd();
            WorldMapTmpFontSupport.Apply(generalMovementText);
        }

        var mm = MapManager.InstanceOrNull;
        Castle besieged = null;
        int planDef = 0;
        bool canDefense = mm != null &&
                            mm.TryGetSiegeDefenseSupportOpportunity(castle, out besieged, out planDef) &&
                            besieged != null;
        if (siegeSupportHintText != null)
        {
            siegeSupportHintText.gameObject.SetActive(canDefense);
            if (canDefense)
            {
                siegeSupportHintText.text =
                    $"아군 수비 지원(도로 연결): {besieged.DisplayCastleName} 공격받는 중 — 약 {planDef:N0} 병력";
                WorldMapTmpFontSupport.Apply(siegeSupportHintText);
            }
        }

        if (siegeSupportButton != null)
            siegeSupportButton.gameObject.SetActive(canDefense);

        Castle attackerAlly = null;
        int planAtk = 0;
        bool canAttack = mm != null &&
                         mm.TryGetSiegeAttackSupportOpportunity(castle, out attackerAlly, out planAtk) &&
                         attackerAlly != null;
        if (siegeAttackSupportHintText != null)
        {
            siegeAttackSupportHintText.gameObject.SetActive(canAttack);
            if (canAttack)
            {
                siegeAttackSupportHintText.text =
                    $"아군 공격 지원(도로 연결): {attackerAlly.DisplayCastleName} 공성 진행 중 — 약 {planAtk:N0} 병력";
                WorldMapTmpFontSupport.Apply(siegeAttackSupportHintText);
            }
        }

        if (siegeAttackSupportButton != null)
            siegeAttackSupportButton.gameObject.SetActive(canAttack);
    }

    public void RefreshFromBound()
    {
        if (_bound != null)
            Bind(_bound);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        _bound = null;
    }
}
