using UnityEngine;

/// <summary>CountryID 기반 성 색상.</summary>
public class CountryColorProvider : MonoBehaviour
{
    [SerializeField] Color weiColor = new Color(0.2f, 0.45f, 0.95f, 1f);
    [SerializeField] Color shuColor = new Color(0.25f, 0.75f, 0.35f, 1f);
    [SerializeField] Color wuColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] Color otherColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    public Color GetColor(CountryId countryId)
    {
        switch (countryId)
        {
            case CountryId.Wei: return weiColor;
            case CountryId.Shu: return shuColor;
            case CountryId.Wu: return wuColor;
            default: return otherColor;
        }
    }

    public string GetCountryDisplayName(CountryId countryId)
    {
        switch (countryId)
        {
            case CountryId.Wei: return "위(魏)";
            case CountryId.Shu: return "촉(蜀)";
            case CountryId.Wu: return "오(吳)";
            default: return "기타";
        }
    }
}
