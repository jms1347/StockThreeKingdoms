using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 지도 위 단일 성 마커 — 세력 색·전쟁/재해 오버레이·본영 깃발.</summary>
public class WorldMarketMapCastlePin : MonoBehaviour
{
    [SerializeField] Image dotImage;
    [SerializeField] GameObject hqFlagRoot;
    [SerializeField] GameObject warOverlay;
    [SerializeField] GameObject disasterOverlay;

    Button _button;
    string _castleId;

    void Awake()
    {
        if (dotImage == null)
            dotImage = GetComponent<Image>();
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClicked);
            _button.onClick.AddListener(OnClicked);
        }
    }

    /// <summary>코드 생성 핀용 — 인스펙터 없이 참조를 연결합니다.</summary>
    public void ConfigureRuntime(Image dot, GameObject hq, GameObject war, GameObject disaster)
    {
        dotImage = dot;
        hqFlagRoot = hq;
        warOverlay = war;
        disasterOverlay = disaster;
    }

    void OnClicked()
    {
        if (string.IsNullOrWhiteSpace(_castleId)) return;
        WorldMarketCastleDetailPopup.OpenCastle(_castleId.Trim());
    }

    public void Bind(
        string castleId,
        Color factionColor,
        bool isHq,
        bool isWar,
        bool isDisaster)
    {
        _castleId = castleId ?? "";
        if (dotImage != null)
            dotImage.color = factionColor;
        if (hqFlagRoot != null)
            hqFlagRoot.SetActive(isHq);
        if (warOverlay != null)
            warOverlay.SetActive(isWar);
        if (disasterOverlay != null)
            disasterOverlay.SetActive(isDisaster);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _castleId = "";
        gameObject.SetActive(false);
    }
}
