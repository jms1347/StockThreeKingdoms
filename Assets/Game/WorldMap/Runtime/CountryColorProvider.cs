using UnityEngine;
using UnityEngine.Serialization;

/// <summary>CountryID 기반 성 색상.</summary>
public class CountryColorProvider : MonoBehaviour
{
    [SerializeField] Color weiColor = new Color(0.2f, 0.45f, 0.95f, 1f);
    [SerializeField] Color shuColor = new Color(0.25f, 0.75f, 0.35f, 1f);
    [SerializeField] Color wuColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [Tooltip("OTHERS(네 번째 국가) / CountryId.Others — 월드맵 성 마커 회색.")]
    [FormerlySerializedAs("otherColor")]
    [SerializeField] Color othersColor = new Color(0.58f, 0.58f, 0.6f, 1f);

    public Color GetColor(CountryId countryId)
    {
        switch (countryId)
        {
            case CountryId.Wei: return weiColor;
            case CountryId.Shu: return shuColor;
            case CountryId.Wu: return wuColor;
            case CountryId.Others: return othersColor;
            default: return othersColor;
        }
    }

    public string GetCountryDisplayName(CountryId countryId)
    {
        switch (countryId)
        {
            case CountryId.Wei: return "위(魏)";
            case CountryId.Shu: return "촉(蜀)";
            case CountryId.Wu: return "오(吳)";
            case CountryId.Others: return "OTHERS";
            default: return "OTHERS";
        }
    }
}
