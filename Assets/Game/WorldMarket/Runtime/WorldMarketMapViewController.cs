using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>천하 탭 지도 모드 — 마스터 좌표에 성 핀을 배치하고 상태를 반영합니다.</summary>
[DisallowMultipleComponent]
public class WorldMarketMapViewController : MonoBehaviour
{
    [SerializeField] ScrollRect mapScroll;
    [SerializeField] RectTransform mapContent;
    [SerializeField] float mapWorldMax = 1000f;
    [SerializeField] float mapMargin = 40f;

    readonly List<WorldMarketMapCastlePin> _pinPool = new List<WorldMarketMapCastlePin>();

    void Start()
    {
        if (mapScroll == null)
            mapScroll = GetComponentInChildren<ScrollRect>(true);
        if (mapContent == null && mapScroll != null)
            mapContent = mapScroll.content;
    }

    static Color FactionPinColor(Faction f)
    {
        switch (f)
        {
            case Faction.WEI: return new Color(0.85f, 0.32f, 0.28f, 1f);
            case Faction.SHU: return new Color(0.35f, 0.72f, 0.42f, 1f);
            case Faction.WU: return new Color(0.35f, 0.52f, 0.92f, 1f);
            case Faction.OTHERS: return new Color(0.58f, 0.58f, 0.6f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }

    void OnEnable()
    {
        TrySubscribe();
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
        Unsubscribe();
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
            return;
        }

        float w = mapContent.rect.width;
        float h = mapContent.rect.height;
        if (w < 2f || h < 2f)
        {
            w = mapContent.sizeDelta.x;
            h = mapContent.sizeDelta.y;
        }

        var ids = dm.GetOrderedWorldCastleIds(WorldMarketCastleListFilter.All);
        int i = 0;
        for (; i < ids.Count; i++)
        {
            string cid = ids[i];
            if (string.IsNullOrWhiteSpace(cid)) continue;
            if (!dm.castleMasterDataMap.TryGetValue(cid.Trim(), out var master) || master == null)
                continue;

            dm.castleStateDataMap.TryGetValue(cid.Trim(), out var st);
            dm.TryGetLiveCastleState(cid.Trim(), out var live);
            bool isWar = live != null ? live.isWar : (st != null && st.isWar);
            bool isDisaster = live != null ? live.isDisaster : (st != null && st.isDisaster);
            Faction lord = Faction.NONE;
            if (live != null) lord = live.currentLord;
            else if (st != null) lord = st.currentLord;
            if (lord == Faction.NONE && master != null)
                lord = master.GetInitialLordFaction();

            bool isHq = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                        && string.Equals(dm.HomeCastleId.Trim(), cid.Trim(), System.StringComparison.Ordinal);

            var pin = GetPinAt(i);
            var pos = CastleMapCoordinateConverter.NormalizedWorldToAnchoredPosition(
                master.posX, master.posY, mapWorldMax, w, h, mapMargin);
            var rt = pin.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;

            pin.Bind(cid, FactionPinColor(lord), isHq, isWar, isDisaster);
        }

        for (int k = i; k < _pinPool.Count; k++)
            _pinPool[k].Hide();
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
        var root = new GameObject("CastlePin", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(22f, 22f);

        var img = root.GetComponent<Image>();
        img.raycastTarget = true;
        img.color = Color.white;

        var hq = new GameObject("HqFlag", typeof(RectTransform), typeof(TextMeshProUGUI));
        hq.transform.SetParent(root.transform, false);
        var hqRt = hq.GetComponent<RectTransform>();
        hqRt.anchorMin = new Vector2(0.5f, 0f);
        hqRt.anchorMax = new Vector2(0.5f, 0f);
        hqRt.pivot = new Vector2(0.5f, 0f);
        hqRt.anchoredPosition = new Vector2(0f, 18f);
        hqRt.sizeDelta = new Vector2(36f, 22f);
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
        war.transform.SetParent(root.transform, false);
        var wrt = war.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0.5f, 1f);
        wrt.anchorMax = new Vector2(0.5f, 1f);
        wrt.pivot = new Vector2(0.5f, 0f);
        wrt.anchoredPosition = new Vector2(0f, 10f);
        wrt.sizeDelta = new Vector2(16f, 16f);
        war.GetComponent<Image>().color = new Color(0.95f, 0.35f, 0.3f, 1f);
        war.SetActive(false);

        var dis = new GameObject("DisasterIcon", typeof(RectTransform), typeof(Image));
        dis.transform.SetParent(root.transform, false);
        var drt = dis.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.5f, 1f);
        drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(10f, 10f);
        drt.sizeDelta = new Vector2(14f, 14f);
        dis.GetComponent<Image>().color = new Color(1f, 0.65f, 0.25f, 1f);
        dis.SetActive(false);

        var pin = root.AddComponent<WorldMarketMapCastlePin>();
        pin.ConfigureRuntime(root.GetComponent<Image>(), hq, war, dis);
        return pin;
    }
}
