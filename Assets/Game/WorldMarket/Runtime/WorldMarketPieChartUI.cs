using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 천하 탭 파이 차트: Image fillAmount(Radial360) + Z 누적 회전 + DOTween + 범례 % 텍스트.
/// 세그먼트 순서: 위(WEI) → 촉(SHU) → 오(WU) → 기타(OTHERS), 시계 방향으로 이어짐.
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketPieChartUI : MonoBehaviour
{
    [SerializeField] Image segmentWei;
    [SerializeField] Image segmentShu;
    [SerializeField] Image segmentWu;
    [SerializeField] Image segmentOthers;

    [SerializeField] TextMeshProUGUI textWei;
    [SerializeField] TextMeshProUGUI textShu;
    [SerializeField] TextMeshProUGUI textWu;
    [SerializeField] TextMeshProUGUI textOthers;

    [SerializeField, Min(0.05f)] float tweenDuration = 0.55f;

    FactionCastleShare _lastShare;
    bool _hasShareSample;

    void Awake()
    {
        ConfigureRadial(segmentWei, new Color(0.20f, 0.55f, 0.90f));
        ConfigureRadial(segmentShu, new Color(0.35f, 0.80f, 0.55f));
        ConfigureRadial(segmentWu, new Color(0.95f, 0.40f, 0.35f));
        ConfigureRadial(segmentOthers, new Color(0.55f, 0.58f, 0.66f));
    }

    void OnEnable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateDataReady += HandleStateRefresh;
            dm.OnStateTicked += HandleStateRefresh;
        }

        RefreshFromData();
    }

    void OnDisable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateDataReady -= HandleStateRefresh;
            dm.OnStateTicked -= HandleStateRefresh;
        }
    }

    void HandleStateRefresh() => RefreshFromData();

    void RefreshFromData()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return;

        var share = dm.GetFactionCastleOwnershipShare();

        if (!_hasShareSample)
        {
            _hasShareSample = true;
            _lastShare = share;
            UpdatePieChart(share, forceFromZero: true);
            return;
        }

        if (ApproximatelySame(_lastShare, share))
            return;

        _lastShare = share;
        UpdatePieChart(share, forceFromZero: false);
    }

    static bool ApproximatelySame(FactionCastleShare a, FactionCastleShare b)
    {
        return Mathf.Approximately(a.wei, b.wei)
               && Mathf.Approximately(a.shu, b.shu)
               && Mathf.Approximately(a.wu, b.wu)
               && Mathf.Approximately(a.others, b.others);
    }

    /// <summary>
    /// 세력 점유율에 맞춰 파이를 갱신합니다.
    /// </summary>
    /// <param name="share">합계 1.0 근사 비율</param>
    /// <param name="forceFromZero">true면 fillAmount를 0에서 목표로 채움</param>
    public void UpdatePieChart(FactionCastleShare share, bool forceFromZero = false)
    {
        Image[] imgs = { segmentWei, segmentShu, segmentWu, segmentOthers };
        TextMeshProUGUI[] texts = { textWei, textShu, textWu, textOthers };
        string[] prefixes = { "WEI", "SHU", "WU", "OTHERS" };
        float[] targets = { share.wei, share.shu, share.wu, share.others };

        float cum = 0f;
        for (int i = 0; i < 4; i++)
        {
            Image img = imgs[i];
            if (img == null) continue;

            float target = Mathf.Clamp01(targets[i]);
            float start = forceFromZero ? 0f : Mathf.Clamp01(img.fillAmount);

            SetZRotation(img.rectTransform, -cum * 360f);
            cum += target;

            img.DOKill(false);

            TextMeshProUGUI label = texts[i];
            string prefix = prefixes[i];

            DOVirtual.Float(start, target, tweenDuration, v =>
                {
                    img.fillAmount = v;
                    if (label != null)
                        label.text = $"{prefix}: {Mathf.RoundToInt(v * 100f)}%";
                })
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(img.gameObject);
        }
    }

    static void SetZRotation(RectTransform rt, float z)
    {
        if (rt == null) return;
        Vector3 e = rt.localEulerAngles;
        e.z = z;
        rt.localEulerAngles = e;
    }

    static void ConfigureRadial(Image img, Color tint)
    {
        if (img == null) return;
        if (img.sprite == null)
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        img.color = tint;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = 2; // Image.Origin360.Top
        img.fillClockwise = true;
        if (img.fillAmount < 0f || img.fillAmount > 1f) img.fillAmount = 0f;
    }
}
