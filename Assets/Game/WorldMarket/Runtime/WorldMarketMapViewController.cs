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

    [Header("지도 오픈 시 본영 포커스")]
    [SerializeField] bool focusHomeCastleWhenMapOpens = true;
    [Tooltip("일부 ScrollRect·앵커 조합에서 세로 스크롤 방향이 반대일 때만 켭니다.")]
    [SerializeField] bool invertVerticalFocusScroll;

    [Tooltip("비우면 WorldMarketRoot에서 찾습니다. 핀 클릭 시 리스트만 해당 항목으로 스크롤(지도는 항상 전체 성).")]
    [SerializeField] WorldMarketCastleVirtualList listSyncTarget;

    readonly List<WorldMarketMapCastlePin> _pinPool = new List<WorldMarketMapCastlePin>();
    readonly List<Image> _roadEdgePool = new List<Image>();
    RectTransform _roadLayer;
    bool _pendingFocusHomeCastle;

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
        _pendingFocusHomeCastle = focusHomeCastleWhenMapOpens;
        TrySubscribe();
        ResolveListSyncTarget();
        WorldMarketMapCastlePin.OnCastlePinClicked += OnCastlePinClickedForListScroll;
        StartCoroutine(CoRebuildAfterLayout());
    }

    IEnumerator CoRebuildAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RebuildPins();
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_pendingFocusHomeCastle)
        {
            ScrollMapToHomeCastle();
            _pendingFocusHomeCastle = false;
        }
    }

    void OnDisable()
    {
        WorldMarketMapCastlePin.OnCastlePinClicked -= OnCastlePinClickedForListScroll;
        Unsubscribe();
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
        // 리스트 상단 필터(보유·전쟁 등)는 스크롤 목록에만 적용. 지도는 항상 전체 거점·도로를 그린다.
        var ids = dm.GetOrderedWorldCastleIds(WorldMarketCastleListFilter.All);

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

            var pin = GetPinAt(i);
            var rt = pin.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.35f);
            rt.anchoredPosition = pos;

            pin.Bind(cidTrim, FactionPinColor(lord), isHq, isWar, isDisaster, isFavorable, userInvested,
                dm.GetCastleRuntimeGrade(cidTrim), displayName);
            i++;
        }

        for (int k = i; k < _pinPool.Count; k++)
            _pinPool[k].Hide();
    }

    void ScrollMapToHomeCastle()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(dm.HomeCastleId))
            return;
        if (!dm.castleMasterDataMap.TryGetValue(dm.HomeCastleId.Trim(), out var master) || master == null)
            return;
        if (mapScroll == null || mapContent == null || mapScroll.viewport == null)
            return;

        float w = mapContent.rect.width;
        float h = mapContent.rect.height;
        if (w < 2f || h < 2f)
        {
            w = mapContent.sizeDelta.x;
            h = mapContent.sizeDelta.y;
        }

        Vector2 focus = CastleMapCoordinateConverter.NormalizedWorldToAnchoredPosition(
            master.posX, master.posY, mapWorldMax, w, h, mapMargin);
        focus += new Vector2(0f, 28f);
        ScrollContentCenterOnPoint(focus);
    }

    void ScrollContentCenterOnPoint(Vector2 pointInContentBottomLeftSpace)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = mapScroll.viewport;
        RectTransform content = mapContent;

        float contentW = content.rect.width;
        float contentH = content.rect.height;
        float viewW = viewport.rect.width;
        float viewH = viewport.rect.height;

        float dx = Mathf.Max(0.001f, contentW - viewW);
        float dy = Mathf.Max(0.001f, contentH - viewH);

        float nx = (pointInContentBottomLeftSpace.x - viewW * 0.5f) / dx;
        float ny = (pointInContentBottomLeftSpace.y - viewH * 0.5f) / dy;
        if (invertVerticalFocusScroll)
            ny = 1f - ny;

        mapScroll.horizontalNormalizedPosition = Mathf.Clamp01(nx);
        mapScroll.verticalNormalizedPosition = Mathf.Clamp01(ny);
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

    static Image CreateMapStatusChip(Transform parent, string nodeName, Color col)
    {
        var go = new GameObject(nodeName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = WhiteBlockSprite();
        img.color = col;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 15f;
        le.minWidth = le.minHeight = 15f;
        go.SetActive(false);
        return img;
    }

    static WorldMarketMapCastlePin CreatePinObject(RectTransform parent)
    {
        var root = new GameObject("CastlePin", typeof(RectTransform), typeof(Button));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 88f);

        var btn = root.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;

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

        var centerGo = new GameObject("CastleNameOnDot", typeof(RectTransform), typeof(TextMeshProUGUI));
        centerGo.transform.SetParent(dotGo.transform, false);
        var cRt = centerGo.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(52f, 40f);
        cRt.anchoredPosition = Vector2.zero;
        var centerTmp = centerGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            centerTmp.font = TMP_Settings.defaultFontAsset;
        centerTmp.fontSize = 14;
        centerTmp.fontStyle = FontStyles.Bold;
        centerTmp.color = Color.white;
        centerTmp.alignment = TextAlignmentOptions.Center;
        centerTmp.enableWordWrapping = true;
        centerTmp.overflowMode = TextOverflowModes.Ellipsis;
        centerTmp.raycastTarget = false;

        var rowGo = new GameObject("StatusIconRow", typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        rowGo.transform.SetParent(root.transform, false);
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, -2f);
        rowRt.sizeDelta = new Vector2(200f, 24f);
        var rowLe = rowGo.GetComponent<LayoutElement>();
        rowLe.minHeight = 24f;
        rowLe.preferredHeight = 24f;
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 4f;
        hlg.padding = new RectOffset(2, 2, 0, 2);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        Image iconWar = CreateMapStatusChip(rowGo.transform, "IconWar", new Color(0.95f, 0.35f, 0.3f, 1f));
        Image iconDis = CreateMapStatusChip(rowGo.transform, "IconDisaster", new Color(1f, 0.65f, 0.25f, 1f));
        Image iconFav = CreateMapStatusChip(rowGo.transform, "IconFavorable", new Color(0.35f, 0.85f, 0.45f, 1f));
        Image iconInv = CreateMapStatusChip(rowGo.transform, "IconInvest", new Color(0.92f, 0.78f, 0.28f, 1f));

        var pin = root.AddComponent<WorldMarketMapCastlePin>();
        pin.ConfigureRuntime(dotImg, hq, centerTmp, iconWar, iconDis, iconFav, iconInv);
        return pin;
    }
}
