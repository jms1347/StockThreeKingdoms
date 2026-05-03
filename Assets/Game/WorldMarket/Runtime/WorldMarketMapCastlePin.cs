using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 지도 위 단일 성 마커 — 세력 색·등급별 크기·본영 깃발·상단 상태 아이콘·중앙 성명.</summary>
public class WorldMarketMapCastlePin : MonoBehaviour
{
    /// <summary>상세 팝업 직전에 호출 — 리스트 스크롤 동기화 등에 사용.</summary>
    public static event System.Action<string> OnCastlePinClicked;

    [SerializeField] Image dotImage;
    [SerializeField] GameObject hqFlagRoot;
    [SerializeField] TextMeshProUGUI centerCastleNameTmp;
    [SerializeField] Image statusIconWar;
    [SerializeField] Image statusIconDisaster;
    [SerializeField] Image statusIconFavorable;
    [SerializeField] Image statusIconInvest;

    RectTransform _rootRt;
    Transform _warShakeTr;
    Transform _eventBounceTr;

    Button _button;
    string _castleId;

    /// <summary>SS 최대 ~ D 최소 (등급은 숫자로 표시하지 않고 마커 크기만 반영).</summary>
    public static float DotScaleForGrade(Grade grade)
    {
        int g = Mathf.Clamp((int)grade, 0, 5);
        return Mathf.Lerp(1.2f, 0.7f, g / 5f);
    }

    void Awake()
    {
        _rootRt = transform as RectTransform;
        if (dotImage == null)
            dotImage = GetComponentInChildren<Image>(true);
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClicked);
            _button.onClick.AddListener(OnClicked);
        }
    }

    /// <summary>코드 생성 핀용 — 인스펙터 없이 참조를 연결합니다.</summary>
    public void ConfigureRuntime(
        Image dot,
        GameObject hq,
        TextMeshProUGUI centerNameTmp,
        Image iconWar,
        Image iconDis,
        Image iconFav,
        Image iconInv)
    {
        dotImage = dot;
        hqFlagRoot = hq;
        centerCastleNameTmp = centerNameTmp;
        statusIconWar = iconWar;
        statusIconDisaster = iconDis;
        statusIconFavorable = iconFav;
        statusIconInvest = iconInv;

        _warShakeTr = iconWar != null ? iconWar.transform : null;
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
        bool isFavorable,
        bool userInvested,
        Grade grade,
        string displayName)
    {
        _castleId = castleId ?? "";
        if (_rootRt != null)
        {
            float s = DotScaleForGrade(grade);
            _rootRt.localScale = new Vector3(s, s, 1f);
        }

        if (dotImage != null)
            dotImage.color = factionColor;

        if (hqFlagRoot != null)
            hqFlagRoot.SetActive(isHq);

        string name = string.IsNullOrWhiteSpace(displayName) ? castleId : displayName.Trim();
        if (centerCastleNameTmp != null)
        {
            centerCastleNameTmp.gameObject.SetActive(true);
            centerCastleNameTmp.text = name;
            centerCastleNameTmp.fontSize = name.Length > 6 ? 13 : 16;
        }

        void SetIcon(Image img, bool on)
        {
            if (img == null) return;
            img.gameObject.SetActive(on);
        }

        SetIcon(statusIconWar, isWar);
        SetIcon(statusIconDisaster, isDisaster);
        SetIcon(statusIconFavorable, isFavorable);
        SetIcon(statusIconInvest, userInvested);

        _eventBounceTr = null;
        if (isDisaster && statusIconDisaster != null)
            _eventBounceTr = statusIconDisaster.transform;
        else if (isFavorable && statusIconFavorable != null)
            _eventBounceTr = statusIconFavorable.transform;

        bool eventMarker = isDisaster || isFavorable;
        RefreshMarkerAnimations(isWar, eventMarker);
        gameObject.SetActive(true);
    }

    void RefreshMarkerAnimations(bool warOn, bool eventOn)
    {
        if (_warShakeTr != null)
        {
            _warShakeTr.DOKill();
            _warShakeTr.localScale = Vector3.one;
            if (warOn)
            {
                _warShakeTr.DOScale(1.16f, 0.38f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        if (_eventBounceTr != null)
        {
            _eventBounceTr.DOKill();
            _eventBounceTr.localScale = Vector3.one;
            if (eventOn)
            {
                _eventBounceTr.DOScale(1.18f, 0.28f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }
    }

    public void Hide()
    {
        if (_warShakeTr != null)
        {
            _warShakeTr.DOKill();
            _warShakeTr.localScale = Vector3.one;
        }

        if (_eventBounceTr != null)
        {
            _eventBounceTr.DOKill();
            _eventBounceTr.localScale = Vector3.one;
        }
        _castleId = "";
        if (_rootRt != null)
            _rootRt.localScale = Vector3.one;

        if (centerCastleNameTmp != null)
        {
            centerCastleNameTmp.text = "";
            centerCastleNameTmp.gameObject.SetActive(false);
        }

        foreach (var img in new[] { statusIconWar, statusIconDisaster, statusIconFavorable, statusIconInvest })
        {
            if (img != null)
                img.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }
}
