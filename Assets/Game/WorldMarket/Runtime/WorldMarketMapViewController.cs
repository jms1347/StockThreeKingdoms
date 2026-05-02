using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>천하 탭 지도 모드 — 마스터 좌표에 성 핀·인접 길(선)을 배치하고 상태를 반영합니다.</summary>
[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
public class WorldMarketMapViewController : MonoBehaviour
{
    [SerializeField] ScrollRect mapScroll;
    [SerializeField] RectTransform mapContent;
    [SerializeField] float mapWorldMax = 1000f;
    [SerializeField] float mapMargin = 40f;
    [Tooltip("맵 콘텐츠 최소 크기(px). 작으면 성들이 한 덩어리로 보이므로 넓게 잡습니다.")]
    [SerializeField] float mapContentMinSize = 2200f;
    [SerializeField] float roadLineThickness = 4.2f;
    [SerializeField] Color roadLineColor = new Color(0.72f, 0.76f, 0.82f, 0.78f);

    [Tooltip("비우면 WorldMarketRoot 아래에서 WorldMarketCastleVirtualList를 찾습니다. 리스트와 필터·스크롤을 동기화합니다.")]
    [SerializeField] WorldMarketCastleVirtualList listSyncTarget;

    readonly List<WorldMarketMapCastlePin> _pinPool = new List<WorldMarketMapCastlePin>();
    readonly List<Image> _roadEdgePool = new List<Image>();
    RectTransform _roadLayer;

    void Awake()
    {
        if (mapScroll == null)
            mapScroll = GetComponentInChildren<ScrollRect>(true);
        if (mapContent == null && mapScroll != null)
            mapContent = mapScroll.content;
        if (mapContent != null)
        {
            float w = Mathf.Max(mapContentMinSize, mapContent.sizeDelta.x);
            float h = Mathf.Max(mapContentMinSize, mapContent.sizeDelta.y);
            mapContent.sizeDelta = new Vector2(w, h);
        }
    }

    void Start()
    {
        if (mapScroll == null)
            mapScroll = GetComponentInChildren<ScrollRect>(true);
        if (mapContent == null && mapScroll != null)
            mapContent = mapScroll.content;
    }

    /// <summary>월드맵·범례와 동일: 위=청, 촉=녹, 오=적, 기타=회색.</summary>
    static Color FactionPinColor(Faction f)
    {
        switch (f)
        {
            case Faction.WEI: return new Color(0.22f, 0.48f, 0.92f, 1f);
            case Faction.SHU: return new Color(0.30f, 0.78f, 0.38f, 1f);
            case Faction.WU: return new Color(0.92f, 0.30f, 0.26f, 1f);
            case Faction.OTHERS: return new Color(0.58f, 0.60f, 0.64f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }

    void OnEnable()
    {
        TrySubscribe();
        ResolveListSyncTarget();
        SubscribeListFilterChanged();
        WorldMarketMapCastlePin.OnCastlePinClicked += OnCastlePinClickedForListScroll;
        StartCoroutine(CoRebuildAfterLayout());
    }

    IEnumerator CoRebuildAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RebuildPins();
    }

    void OnDisable()
    {
        WorldMarketMapCastlePin.OnCastlePinClicked -= OnCastlePinClickedForListScroll;
        Unsubscribe();
        UnsubscribeListFilterChanged();
    }

    void OnDestroy()
    {
        UnsubscribeListFilterChanged();
    }

    void OnCastlePinClickedForListScroll(string castleId)
    {
        ResolveListSyncTarget();
        if (listSyncTarget != null)
            listSyncTarget.TryScrollToCastleId(castleId);
    }

    void ResolveListSyncTarget()
    {
        if (listSyncTarget != null)
            return;
        Transform t = transform;
        for (int i = 0; i < 16 && t != null; i++, t = t.parent)
        {
            if (t.name != "WorldMarketRoot") continue;
            listSyncTarget = t.GetComponentInChildren<WorldMarketCastleVirtualList>(true);
            return;
        }

        listSyncTarget = FindObjectOfType<WorldMarketCastleVirtualList>();
    }

    void SubscribeListFilterChanged()
    {
        ResolveListSyncTarget();
        if (listSyncTarget == null) return;
        listSyncTarget.FilterChanged -= OnLinkedListFilterChanged;
        listSyncTarget.FilterChanged += OnLinkedListFilterChanged;
    }

    void UnsubscribeListFilterChanged()
    {
        if (listSyncTarget == null) return;
        listSyncTarget.FilterChanged -= OnLinkedListFilterChanged;
    }

    void OnLinkedListFilterChanged(WorldMarketCastleListFilter _) => RebuildPins();

    void TrySubscribe()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateTicked -= OnDm;
        dm.OnStateDataReady -= OnDm;
        dm.OnHomeCastleChanged -= OnDm;
        dm.OnStateTicked += OnDm;
        dm.OnStateDataReady += OnDm;
        dm.OnHomeCastleChanged += OnDm;
    }

    void Unsubscribe()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateTicked -= OnDm;
        dm.OnStateDataReady -= OnDm;
        dm.OnHomeCastleChanged -= OnDm;
    }

    void OnDm() => RebuildPins();

    void RebuildPins()
    {
        if (mapContent == null)
            return;

        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady)
        {
            HideAllPins();
            HideAllRoadEdges();
            return;
        }

        float w = mapContent.rect.width;
        float h = mapContent.rect.height;
        if (w < 2f || h < 2f)
        {
            w = mapContent.sizeDelta.x;
            h = mapContent.sizeDelta.y;
        }

        ResolveListSyncTarget();
        var filter = listSyncTarget != null
            ? listSyncTarget.CurrentFilter
            : WorldMarketCastleListFilter.All;
        var ids = dm.GetOrderedWorldCastleIds(filter);

        var positions = new Dictionary<string, Vector2>(ids.Count);
        var visibleIds = new HashSet<string>();
        foreach (var cidRaw in ids)
        {
            string cid = cidRaw?.Trim();
            if (string.IsNullOrEmpty(cid)) continue;
            if (!dm.castleMasterDataMap.TryGetValue(cid, out var master) || master == null)
                continue;
            visibleIds.Add(cid);
            positions[cid] = CastleMapCoordinateConverter.NormalizedWorldToAnchoredPosition(
                master.posX, master.posY, mapWorldMax, w, h, mapMargin);
        }

        EnsureRoadLayer();
        RebuildRoadEdges(dm, positions, visibleIds, w, h);

        int i = 0;
        foreach (var cid in ids)
        {
            if (string.IsNullOrWhiteSpace(cid)) continue;
            string cidTrim = cid.Trim();
            if (!dm.castleMasterDataMap.TryGetValue(cidTrim, out var master) || master == null)
                continue;
            if (!positions.TryGetValue(cidTrim, out var pos))
                continue;

            dm.castleStateDataMap.TryGetValue(cidTrim, out var st);
            dm.TryGetLiveCastleState(cidTrim, out var live);
            bool isWar = live != null ? live.isWar : (st != null && st.isWar);
            bool isDisaster = live != null ? live.isDisaster : (st != null && st.isDisaster);
            bool isFavorable = live != null ? live.isFavorableEvent : (st != null && st.isFavorableEvent);
            bool userInvested = (live != null && live.userDeployedTroops > 0)
                || (st != null && st.IsUserInvested);
            Faction lord = Faction.NONE;
            if (live != null) lord = live.currentLord;
            else if (st != null) lord = st.currentLord;
            if (lord == Faction.NONE && master != null)
                lord = master.GetInitialLordFaction();

            bool isHq = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                        && string.Equals(dm.HomeCastleId.Trim(), cidTrim, System.StringComparison.Ordinal);

            string displayName = dm.GetCastleDisplayName(cidTrim);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = master.name;
            string status = BuildMapPinStatusLine(isWar, isDisaster, isFavorable, userInvested);

            var pin = GetPinAt(i);
            var rt = pin.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.35f);
            rt.anchoredPosition = pos;

            string gradeStr = master.grade.ToString();
            pin.Bind(cidTrim, FactionPinColor(lord), isHq, isWar, isDisaster, isFavorable, displayName, status,
                gradeStr);
            i++;
        }

        for (int k = i; k < _pinPool.Count; k++)
            _pinPool[k].Hide();
    }

    static string BuildMapPinStatusLine(bool isWar, bool isDisaster, bool isFavorable, bool userInvested)
    {
        var parts = new List<string>(4);
        if (isWar) parts.Add("전쟁");
        if (isDisaster) parts.Add("재해");
        if (isFavorable) parts.Add("호재");
        if (userInvested) parts.Add("투자");
        if (parts.Count == 0)
            return null;
        return string.Join(" · ", parts);
    }

    void EnsureRoadLayer()
    {
        if (mapContent == null) return;
        if (_roadLayer == null)
        {
            var go = new GameObject("RoadLayer", typeof(RectTransform));
            go.transform.SetParent(mapContent, false);
            _roadLayer = go.GetComponent<RectTransform>();
            _roadLayer.anchorMin = Vector2.zero;
            _roadLayer.anchorMax = Vector2.one;
            _roadLayer.pivot = new Vector2(0.5f, 0.5f);
            _roadLayer.offsetMin = Vector2.zero;
            _roadLayer.offsetMax = Vector2.zero;
        }

        FixRoadLayerSiblingOrder();
    }

    /// <summary>지도 배경(MapBackground) 전면 이미지보다 <b>위</b>, 핀보다 아래에 두어 길이 보이게 합니다.</summary>
    void FixRoadLayerSiblingOrder()
    {
        if (_roadLayer == null || mapContent == null) return;
        var bg = mapContent.Find("MapBackground");
        int idx = bg != null ? bg.GetSiblingIndex() + 1 : 0;
        if (_roadLayer.GetSiblingIndex() != idx)
            _roadLayer.SetSiblingIndex(idx);
    }

    void RebuildRoadEdges(DataManager dm, Dictionary<string, Vector2> pinPositions, HashSet<string> visibleIds, float w, float h)
    {
        if (_roadLayer == null || dm?.castleMasterDataMap == null)
        {
            HideAllRoadEdges();
            return;
        }

        var allPositions = new Dictionary<string, Vector2>(dm.castleMasterDataMap.Count);
        foreach (var kv in dm.castleMasterDataMap)
        {
            var master = kv.Value;
            if (master == null) continue;
            string id = kv.Key?.Trim();
            if (string.IsNullOrEmpty(id)) continue;
            allPositions[id] = CastleMapCoordinateConverter.NormalizedWorldToAnchoredPosition(
                master.posX, master.posY, mapWorldMax, w, h, mapMargin);
        }

        var seen = new HashSet<string>();
        int edgeIndex = 0;

        foreach (var kv in allPositions)
        {
            string cid = kv.Key;
            if (!dm.castleMasterDataMap.TryGetValue(cid, out var master) || master == null)
                continue;

            foreach (string adj in master.GetAdjacentIds())
            {
                string na = adj?.Trim();
                if (string.IsNullOrEmpty(na))
                    continue;
                string pair = PairKey(cid, na);
                if (!seen.Add(pair))
                    continue;
                if (!allPositions.TryGetValue(cid, out var a) || !allPositions.TryGetValue(na, out var b))
                    continue;

                bool touchesVisible = visibleIds.Contains(cid) || visibleIds.Contains(na);
                if (!touchesVisible)
                    continue;

                var img = GetRoadEdgeAt(edgeIndex++);
                PlaceRoadEdge(img, a, b);
            }
        }

        for (int k = edgeIndex; k < _roadEdgePool.Count; k++)
            _roadEdgePool[k].gameObject.SetActive(false);
    }

    static string PairKey(string a, string b)
    {
        return string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    Image GetRoadEdgeAt(int index)
    {
        while (_roadEdgePool.Count <= index)
        {
            var go = new GameObject("RoadEdge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_roadLayer, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = WhiteBlockSprite();
            img.type = Image.Type.Simple;
            _roadEdgePool.Add(img);
        }

        var edge = _roadEdgePool[index];
        edge.gameObject.SetActive(true);
        return edge;
    }

    static Sprite _whiteBlockSprite;

    static Sprite WhiteBlockSprite()
    {
        if (_whiteBlockSprite != null)
            return _whiteBlockSprite;
        var tex = Texture2D.whiteTexture;
        _whiteBlockSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteBlockSprite;
    }

    void PlaceRoadEdge(Image img, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        var rt = img.rectTransform;
        if (len < 12f)
        {
            rt.gameObject.SetActive(false);
            return;
        }

        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float thick = Mathf.Max(roadLineThickness, 3f);
        rt.sizeDelta = new Vector2(len, thick);
        rt.anchoredPosition = (a + b) * 0.5f;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        rt.localEulerAngles = new Vector3(0f, 0f, ang);
        img.color = roadLineColor;
    }

    void HideAllRoadEdges()
    {
        for (int i = 0; i < _roadEdgePool.Count; i++)
        {
            if (_roadEdgePool[i] != null)
                _roadEdgePool[i].gameObject.SetActive(false);
        }
    }

    void HideAllPins()
    {
        for (int k = 0; k < _pinPool.Count; k++)
            _pinPool[k].Hide();
    }

    WorldMarketMapCastlePin GetPinAt(int index)
    {
        while (_pinPool.Count <= index)
        {
            var go = CreatePinObject(mapContent);
            _pinPool.Add(go);
        }

        return _pinPool[index];
    }

    static WorldMarketMapCastlePin CreatePinObject(RectTransform parent)
    {
        var root = new GameObject("CastlePin", typeof(RectTransform), typeof(Button));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 88f);

        var btn = root.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;

        var nameGo = new GameObject("CastleName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(root.transform, false);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, -2f);
        nameRt.sizeDelta = new Vector2(220f, 30f);
        var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            nameTmp.font = TMP_Settings.defaultFontAsset;
        nameTmp.fontSize = 16;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = new Color(0.94f, 0.95f, 0.97f, 1f);
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.enableWordWrapping = false;
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;

        var statusGo = new GameObject("StatusHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGo.transform.SetParent(root.transform, false);
        var stRt = statusGo.GetComponent<RectTransform>();
        stRt.anchorMin = stRt.anchorMax = new Vector2(0.5f, 1f);
        stRt.pivot = new Vector2(0.5f, 1f);
        stRt.anchoredPosition = new Vector2(0f, -30f);
        stRt.sizeDelta = new Vector2(220f, 22f);
        var statusTmp = statusGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            statusTmp.font = TMP_Settings.defaultFontAsset;
        statusTmp.fontSize = 13;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.color = new Color(0.92f, 0.72f, 0.95f, 1f);
        statusTmp.alignment = TextAlignmentOptions.Center;
        statusTmp.enableWordWrapping = false;
        statusGo.SetActive(false);

        var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(root.transform, false);
        var dotRt = dotGo.GetComponent<RectTransform>();
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0f);
        dotRt.pivot = new Vector2(0.5f, 0f);
        dotRt.anchoredPosition = new Vector2(0f, 10f);
        dotRt.sizeDelta = new Vector2(38f, 38f);
        var dotImg = dotGo.GetComponent<Image>();
        dotImg.sprite = WhiteBlockSprite();
        dotImg.type = Image.Type.Simple;
        dotImg.raycastTarget = true;
        dotImg.color = Color.white;
        btn.targetGraphic = dotImg;

        var hq = new GameObject("HqFlag", typeof(RectTransform), typeof(TextMeshProUGUI));
        hq.transform.SetParent(dotGo.transform, false);
        var hqRt = hq.GetComponent<RectTransform>();
        hqRt.anchorMin = new Vector2(0.5f, 1f);
        hqRt.anchorMax = new Vector2(0.5f, 1f);
        hqRt.pivot = new Vector2(0.5f, 0f);
        hqRt.anchoredPosition = new Vector2(0f, 4f);
        hqRt.sizeDelta = new Vector2(44f, 22f);
        var hqTmp = hq.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            hqTmp.font = TMP_Settings.defaultFontAsset;
        hqTmp.text = "본영";
        hqTmp.fontSize = 12;
        hqTmp.fontStyle = FontStyles.Bold;
        hqTmp.color = new Color(1f, 0.9f, 0.45f, 1f);
        hqTmp.alignment = TextAlignmentOptions.Center;
        hq.SetActive(false);

        var war = new GameObject("WarIcon", typeof(RectTransform), typeof(Image));
        war.transform.SetParent(dotGo.transform, false);
        var wrt = war.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(1f, 1f);
        wrt.anchorMax = new Vector2(1f, 1f);
        wrt.pivot = new Vector2(1f, 1f);
        wrt.anchoredPosition = new Vector2(6f, 6f);
        wrt.sizeDelta = new Vector2(18f, 18f);
        war.GetComponent<Image>().color = new Color(0.95f, 0.35f, 0.3f, 1f);
        war.GetComponent<Image>().sprite = WhiteBlockSprite();
        war.SetActive(false);

        var dis = new GameObject("DisasterIcon", typeof(RectTransform), typeof(Image));
        dis.transform.SetParent(dotGo.transform, false);
        var drt = dis.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 1f);
        drt.anchorMax = new Vector2(0f, 1f);
        drt.pivot = new Vector2(0f, 1f);
        drt.anchoredPosition = new Vector2(-6f, 6f);
        drt.sizeDelta = new Vector2(16f, 16f);
        var disImg = dis.GetComponent<Image>();
        disImg.color = new Color(1f, 0.65f, 0.25f, 1f);
        disImg.sprite = WhiteBlockSprite();
        dis.SetActive(false);

        var fav = new GameObject("FavorableIcon", typeof(RectTransform), typeof(Image));
        fav.transform.SetParent(dotGo.transform, false);
        var frt = fav.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(1f, 0f);
        frt.anchorMax = new Vector2(1f, 0f);
        frt.pivot = new Vector2(1f, 0f);
        frt.anchoredPosition = new Vector2(6f, -6f);
        frt.sizeDelta = new Vector2(16f, 16f);
        var favImg = fav.GetComponent<Image>();
        favImg.color = new Color(0.35f, 0.85f, 0.45f, 1f);
        favImg.sprite = WhiteBlockSprite();
        fav.SetActive(false);

        var evAlert = new GameObject("EventAlert", typeof(RectTransform), typeof(TextMeshProUGUI));
        evAlert.transform.SetParent(dotGo.transform, false);
        var ert0 = evAlert.GetComponent<RectTransform>();
        ert0.anchorMin = new Vector2(0.5f, 1f);
        ert0.anchorMax = new Vector2(0.5f, 1f);
        ert0.pivot = new Vector2(0.5f, 1f);
        ert0.anchoredPosition = new Vector2(0f, 8f);
        ert0.sizeDelta = new Vector2(22f, 22f);
        var evTmp = evAlert.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            evTmp.font = TMP_Settings.defaultFontAsset;
        evTmp.text = "!";
        evTmp.fontSize = 18;
        evTmp.fontStyle = FontStyles.Bold;
        evTmp.color = new Color(1f, 0.88f, 0.2f, 1f);
        evTmp.alignment = TextAlignmentOptions.Center;
        evAlert.SetActive(false);

        var gradeGo = new GameObject("GradeLetter", typeof(RectTransform), typeof(TextMeshProUGUI));
        gradeGo.transform.SetParent(dotGo.transform, false);
        var grt = gradeGo.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(34f, 34f);
        grt.anchoredPosition = Vector2.zero;
        var gradeTmp = gradeGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            gradeTmp.font = TMP_Settings.defaultFontAsset;
        gradeTmp.fontSize = 17;
        gradeTmp.fontStyle = FontStyles.Bold;
        gradeTmp.color = Color.white;
        gradeTmp.alignment = TextAlignmentOptions.Center;

        var pin = root.AddComponent<WorldMarketMapCastlePin>();
        pin.ConfigureRuntime(dotImg, hq, war, dis, fav, evAlert, nameTmp, statusTmp, gradeTmp);
        return pin;
    }
}
