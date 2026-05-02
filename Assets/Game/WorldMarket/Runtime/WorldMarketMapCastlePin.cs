using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 지도 위 단일 성 마커 — 세력 색·전쟁(진동)·이벤트(바운스)·본영 깃발·이름·상태 한 줄.</summary>
public class WorldMarketMapCastlePin : MonoBehaviour
{
    /// <summary>상세 팝업 직전에 호출 — 리스트 스크롤 동기화 등에 사용.</summary>
    public static event System.Action<string> OnCastlePinClicked;
    [SerializeField] Image dotImage;
    [SerializeField] GameObject hqFlagRoot;
    [SerializeField] GameObject warOverlay;
    [SerializeField] GameObject disasterOverlay;
    [SerializeField] GameObject favorableOverlay;
    [SerializeField] GameObject eventAlertOverlay;
    [SerializeField] TextMeshProUGUI castleNameText;
    [SerializeField] TextMeshProUGUI statusHintText;
    [SerializeField] TextMeshProUGUI gradeLetterTmp;

    RectTransform _warRt;
    RectTransform _eventRt;
    Vector2 _warBaseAnchoredPos;
    Vector2 _eventBaseAnchoredPos;

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
    public void ConfigureRuntime(Image dot, GameObject hq, GameObject war, GameObject disaster, GameObject favorable,
        GameObject eventAlert, TextMeshProUGUI nameTmp = null, TextMeshProUGUI statusTmp = null,
        TextMeshProUGUI gradeTmp = null)
    {
        dotImage = dot;
        hqFlagRoot = hq;
        warOverlay = war;
        disasterOverlay = disaster;
        favorableOverlay = favorable;
        eventAlertOverlay = eventAlert;
        castleNameText = nameTmp;
        statusHintText = statusTmp;
        gradeLetterTmp = gradeTmp;

        _warRt = war != null ? war.GetComponent<RectTransform>() : null;
        _eventRt = eventAlert != null ? eventAlert.GetComponent<RectTransform>() : null;
        if (_warRt != null) _warBaseAnchoredPos = _warRt.anchoredPosition;
        if (_eventRt != null) _eventBaseAnchoredPos = _eventRt.anchoredPosition;
    }

    void OnClicked()
    {
        if (string.IsNullOrWhiteSpace(_castleId)) return;
        string id = _castleId.Trim();
        OnCastlePinClicked?.Invoke(id);
        WorldMarketCastleSummarySheet.OpenCastle(id);
    }

    public void Bind(
        string castleId,
        Color factionColor,
        bool isHq,
        bool isWar,
        bool isDisaster,
        bool isFavorable = false,
        string displayName = null,
        string statusHint = null,
        string gradeLetter = null)
    {
        _castleId = castleId ?? "";
        if (dotImage != null)
            dotImage.color = factionColor;
        if (gradeLetterTmp != null)
        {
            bool showG = !string.IsNullOrEmpty(gradeLetter);
            gradeLetterTmp.gameObject.SetActive(showG);
            if (showG)
            {
                gradeLetterTmp.text = gradeLetter.Trim();
                gradeLetterTmp.color = new Color(0.96f, 0.97f, 0.99f, 1f);
            }
        }

        if (hqFlagRoot != null)
            hqFlagRoot.SetActive(isHq);
        if (warOverlay != null)
            warOverlay.SetActive(isWar);

        bool eventMarker = isDisaster || isFavorable;
        if (eventAlertOverlay != null)
            eventAlertOverlay.SetActive(eventMarker);
        if (disasterOverlay != null)
            disasterOverlay.SetActive(isDisaster && !eventMarker);
        if (favorableOverlay != null)
            favorableOverlay.SetActive(isFavorable && !eventMarker);

        if (castleNameText != null)
        {
            castleNameText.text = string.IsNullOrWhiteSpace(displayName) ? castleId : displayName;
            castleNameText.gameObject.SetActive(true);
        }

        if (statusHintText != null)
        {
            bool has = !string.IsNullOrWhiteSpace(statusHint);
            statusHintText.gameObject.SetActive(has);
            if (has)
                statusHintText.text = statusHint.Trim();
        }

        RefreshMarkerAnimations(isWar, eventMarker);
        gameObject.SetActive(true);
    }

    void RefreshMarkerAnimations(bool warOn, bool eventOn)
    {
        if (_warRt != null)
        {
            _warRt.DOKill();
            _warRt.anchoredPosition = _warBaseAnchoredPos;
            if (warOn)
            {
                _warRt.DOShakeAnchorPos(0.55f, new Vector2(2.8f, 2.8f), 14, 35f, false, false)
                    .SetLoops(-1)
                    .SetUpdate(true);
            }
        }

        if (_eventRt != null)
        {
            _eventRt.DOKill();
            _eventRt.anchoredPosition = _eventBaseAnchoredPos;
            if (eventOn)
            {
                float y0 = _eventBaseAnchoredPos.y;
                _eventRt.DOAnchorPosY(y0 + 7f, 0.35f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }
    }

    public void Hide()
    {
        if (_warRt != null) _warRt.DOKill();
        if (_eventRt != null) _eventRt.DOKill();
        _castleId = "";
        if (castleNameText != null)
            castleNameText.text = "";
        if (gradeLetterTmp != null)
        {
            gradeLetterTmp.text = "";
            gradeLetterTmp.gameObject.SetActive(false);
        }
        if (statusHintText != null)
        {
            statusHintText.text = "";
            statusHintText.gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
    }
}
