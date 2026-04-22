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

    Castle _bound;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        Hide();
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
            castleIdText.text = $"Castle ID: {castle.CastleId}";
        if (countryText != null)
            countryText.text = $"Country: {castle.CountryDisplayName} ({castle.CountryId})";
        if (governorText != null)
            governorText.text = $"Governor: {castle.GovernorName}";
        if (armyText != null)
            armyText.text = $"Army: {castle.Army:N0}";
        if (populationText != null)
            populationText.text = $"Population: {castle.Population:N0}";
        if (sentimentText != null)
            sentimentText.text = $"Public Sentiment: {castle.PublicSentiment}";
        if (valueText != null)
            valueText.text = $"Castle Value: {castle.CastleValue:N0}";
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
