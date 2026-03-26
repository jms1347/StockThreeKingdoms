using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하 탭 세력 점유: 가로 100% 스택 막대(위·촉·오·기타) + 범례 % 텍스트.
/// (구 파이 차트 UI를 동일 컴포넌트 슬롯으로 대체해 씬 참조를 유지합니다.)
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

    FactionCastleShare _lastShare;
    bool _hasShareSample;
    Coroutine _lateRefreshRoutine;
    bool _catchUpRefreshOnce;

    void Awake()
    {
        StripLegacyPieChrome();
        ConfigureBarSegment(segmentWei, new Color(0.20f, 0.55f, 0.90f));
        ConfigureBarSegment(segmentShu, new Color(0.35f, 0.80f, 0.55f));
        ConfigureBarSegment(segmentWu, new Color(0.95f, 0.40f, 0.35f));
        ConfigureBarSegment(segmentOthers, new Color(0.55f, 0.58f, 0.66f));

        UpdateStackedBar(new FactionCastleShare
        {
            wei = 0.25f,
            shu = 0.25f,
            wu = 0.25f,
            others = 0.25f
        });
    }

    void StripLegacyPieChrome()
    {
        var af = GetComponent<AspectRatioFitter>();
        if (af != null)
            Destroy(af);

        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.name == "DonutHole")
                ch.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        _catchUpRefreshOnce = true;
        TrySubscribeDataManager();
        RefreshFromData();
        if (_lateRefreshRoutine != null)
            StopCoroutine(_lateRefreshRoutine);
        _lateRefreshRoutine = StartCoroutine(CoLateRefreshUntilData());
        StartCoroutine(CoDeferLayoutRebuild());
    }

    void Start()
    {
        TrySubscribeDataManager();
        RefreshFromData();
    }

    IEnumerator CoDeferLayoutRebuild()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        var parentRt = transform.parent as RectTransform;
        if (parentRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
        RefreshFromData();
    }

    void OnDisable()
    {
        if (_lateRefreshRoutine != null)
        {
            StopCoroutine(_lateRefreshRoutine);
            _lateRefreshRoutine = null;
        }

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateDataReady -= HandleStateRefresh;
            dm.OnStateTicked -= HandleStateRefresh;
        }
    }

    IEnumerator CoLateRefreshUntilData()
    {
        DataManager dm;
        for (int i = 0; i < 120; i++)
        {
            yield return null;
            TrySubscribeDataManager();
            RefreshFromData();
            dm = DataManager.InstanceOrNull;
            if (dm != null && dm.IsStateReady && dm.castleStateDataMap != null && dm.castleStateDataMap.Count > 0)
            {
                _lateRefreshRoutine = null;
                yield break;
            }
        }

        _lateRefreshRoutine = null;

        dm = DataManager.InstanceOrNull;
        if (dm != null && !dm.IsReady)
        {
            Debug.LogWarning("[WorldMarketPieChartUI] DataManager 미초기화 — 로컬 SO로 초기화 시도.");
            dm.InitializeAllData();
            RefreshFromData();
        }
    }

    void TrySubscribeDataManager()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateDataReady -= HandleStateRefresh;
        dm.OnStateTicked -= HandleStateRefresh;
        dm.OnStateDataReady += HandleStateRefresh;
        dm.OnStateTicked += HandleStateRefresh;

        if (_catchUpRefreshOnce && dm.IsStateReady)
        {
            _catchUpRefreshOnce = false;
            RefreshFromData();
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
            UpdateStackedBar(share);
            return;
        }

        if (ApproximatelySame(_lastShare, share))
            return;

        _lastShare = share;
        UpdateStackedBar(share);
    }

    static bool ApproximatelySame(FactionCastleShare a, FactionCastleShare b)
    {
        return Mathf.Approximately(a.wei, b.wei)
               && Mathf.Approximately(a.shu, b.shu)
               && Mathf.Approximately(a.wu, b.wu)
               && Mathf.Approximately(a.others, b.others);
    }

    /// <summary>합계 1.0 근사 비율로 가로 스택 막대를 갱신합니다.</summary>
    public void UpdateStackedBar(FactionCastleShare share)
    {
        Image[] imgs = { segmentWei, segmentShu, segmentWu, segmentOthers };
        TextMeshProUGUI[] texts = { textWei, textShu, textWu, textOthers };
        string[] prefixes = { "WEI", "SHU", "WU", "OTHERS" };

        float w = Mathf.Max(0f, share.wei);
        float sh = Mathf.Max(0f, share.shu);
        float wu = Mathf.Max(0f, share.wu);
        float o = Mathf.Max(0f, share.others);
        float sum = w + sh + wu + o;
        if (sum > 0.0001f)
        {
            w /= sum;
            sh /= sum;
            wu /= sum;
            o /= sum;
        }
        else
        {
            w = sh = wu = o = 0.25f;
        }

        float[] targets = { w, sh, wu, o };
        float cum = 0f;
        const float minVisual = 1e-4f;

        for (int i = 0; i < 4; i++)
        {
            Image img = imgs[i];
            if (img == null) continue;

            float t = Mathf.Clamp01(targets[i]);
            int pct = Mathf.RoundToInt(t * 100f);
            if (texts[i] != null)
                texts[i].text = $"{prefixes[i]}: {pct}%";

            if (t < minVisual)
            {
                img.gameObject.SetActive(false);
                continue;
            }

            img.gameObject.SetActive(true);
            float x0 = cum;
            float x1 = i == 3 ? 1f : cum + t;
            ApplyHorizontalSlice(img.rectTransform, x0, x1);
            cum = x1;
        }
    }

    static void ApplyHorizontalSlice(RectTransform rt, float anchorXMin, float anchorXMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(anchorXMin, 0f);
        rt.anchorMax = new Vector2(anchorXMax, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localEulerAngles = Vector3.zero;
        rt.localScale = Vector3.one;
    }

    static void ConfigureBarSegment(Image img, Color tint)
    {
        if (img == null) return;
        if (img.sprite == null)
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        img.type = Image.Type.Simple;
        img.color = tint;
        img.raycastTarget = false;
        img.rectTransform.localEulerAngles = Vector3.zero;
    }
}
