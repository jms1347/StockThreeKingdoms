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
    const string LogPrefix = "[WorldMarketPieChartUI]";
    const int FramesLateRefresh = 120;

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
        EnsureSegmentsHorizontalLayout();
        ConfigureBarSegment(segmentWei, new Color(0.20f, 0.55f, 0.90f));
        ConfigureBarSegment(segmentShu, new Color(0.35f, 0.80f, 0.55f));
        ConfigureBarSegment(segmentWu, new Color(0.95f, 0.40f, 0.35f));
        ConfigureBarSegment(segmentOthers, new Color(0.55f, 0.58f, 0.66f));

        EnsureInBarPercentLabels();

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

    /// <summary>기존 씬: Segments에 HLG가 없으면 추가해 막대 비율이 LayoutElement.flexibleWidth로 유지되게 합니다.</summary>
    void EnsureSegmentsHorizontalLayout()
    {
        if (segmentWei == null) return;
        var seg = segmentWei.transform.parent;
        if (seg == null) return;
        var h = seg.GetComponent<HorizontalLayoutGroup>();
        if (h == null)
            h = seg.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 0f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(0, 0, 0, 0);
        h.childAlignment = TextAnchor.MiddleCenter;

        Image[] imgs = { segmentWei, segmentShu, segmentWu, segmentOthers };
        foreach (var img in imgs)
        {
            if (img == null) continue;
            var le = img.GetComponent<LayoutElement>();
            if (le == null)
                le = img.gameObject.AddComponent<LayoutElement>();
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 0f);
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
        for (int i = 0; i < FramesLateRefresh; i++)
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
            Debug.LogWarning($"{LogPrefix} DataManager 미초기화 — 로컬 SO로 InitializeAllData 시도.");
            dm.InitializeAllData();
            RefreshFromData();
        }
        else if (dm != null && dm.IsReady && !dm.IsStateReady)
        {
            Debug.LogWarning($"{LogPrefix} IsReady는 true인데 IsStateReady가 false — InitializeStateData 재시도.");
            dm.InitializeStateData();
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

    /// <summary>합계 1.0 근사 비율로 가로 스택 막대를 갱신합니다. 세그먼트 내부에 % 표시.</summary>
    public void UpdateStackedBar(FactionCastleShare share)
    {
        Image[] imgs = { segmentWei, segmentShu, segmentWu, segmentOthers };
        TextMeshProUGUI[] texts = { textWei, textShu, textWu, textOthers };
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
        const float minVisual = 1e-4f;
        RectTransform segmentsRt = null;

        for (int i = 0; i < 4; i++)
        {
            Image img = imgs[i];
            if (img == null) continue;

            if (segmentsRt == null)
                segmentsRt = img.transform.parent as RectTransform;

            float t = Mathf.Clamp01(targets[i]);
            int pct = Mathf.RoundToInt(t * 100f);

            var le = img.GetComponent<LayoutElement>();
            if (le == null)
                le = img.gameObject.AddComponent<LayoutElement>();

            if (texts[i] != null)
            {
                string[] legendShort = { "위", "촉", "오", "기타" };
                texts[i].text = $"{legendShort[i]} 점유 {pct}%";
            }

            var inBar = img.transform.Find("PctLabel")?.GetComponent<TextMeshProUGUI>();
            if (inBar != null)
            {
                bool showPct = t >= 0.06f;
                inBar.gameObject.SetActive(showPct && t >= minVisual);
                if (showPct && t >= minVisual)
                    inBar.text = $"{pct}%";
            }

            if (t < minVisual)
            {
                img.gameObject.SetActive(false);
                le.flexibleWidth = 0f;
                le.minWidth = 0f;
                le.preferredWidth = 0f;
                if (inBar != null)
                    inBar.gameObject.SetActive(false);
                continue;
            }

            img.gameObject.SetActive(true);
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = t;
            le.minHeight = 0f;
            le.flexibleHeight = 1f;
        }

        if (segmentsRt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(segmentsRt);
            Canvas.ForceUpdateCanvases();
        }
    }

    void EnsureInBarPercentLabels()
    {
        TextMeshProUGUI template = textWei;
        Image[] imgs = { segmentWei, segmentShu, segmentWu, segmentOthers };
        foreach (var img in imgs)
        {
            if (img == null) continue;
            if (img.transform.Find("PctLabel") != null)
                continue;

            var go = new GameObject("PctLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(img.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "0%";
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = 18f;
            tmp.fontSize = 16f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
            tmp.raycastTarget = false;
            tmp.margin = new Vector4(4f, 0f, 4f, 0f);
            if (template != null)
            {
                tmp.font = template.font;
                tmp.fontSharedMaterial = template.fontSharedMaterial;
            }
        }
    }

    static void ConfigureBarSegment(Image img, Color tint)
    {
        if (img == null) return;
        img.sprite = ResolveSquareUiSprite();
        img.type = Image.Type.Simple;
        img.color = tint;
        img.raycastTarget = false;
        img.rectTransform.localEulerAngles = Vector3.zero;
    }

    static Sprite _cachedUiBlockSprite;

    /// <summary>
    /// 직사각형 막대용 단색 스프라이트. 예전 <c>UI/Skin/UISprite.psd</c> 등 내장 리소스는 Unity 6+에서 없어져 런타임 생성으로 대체.
    /// </summary>
    static Sprite ResolveSquareUiSprite()
    {
        if (_cachedUiBlockSprite != null)
            return _cachedUiBlockSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "WorldMarketPieChartUI_WhiteBlock",
            hideFlags = HideFlags.DontSave
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);

        _cachedUiBlockSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        _cachedUiBlockSprite.name = "WorldMarketPieChartUI_WhiteBlockSprite";
        return _cachedUiBlockSprite;
    }

    /// <summary>단색 <see cref="Image"/> 칩 등 다른 UI에서 재사용.</summary>
    public static Sprite GetSquareUiSprite() => ResolveSquareUiSprite();
}
