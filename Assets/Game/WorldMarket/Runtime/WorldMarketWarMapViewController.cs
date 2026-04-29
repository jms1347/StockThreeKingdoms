using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 전쟁 관전 뷰 — 교전 성 마커 펄스와 실시간 전황 로그를 표시합니다.</summary>
[DisallowMultipleComponent]
public class WorldMarketWarMapViewController : MonoBehaviour
{
    [SerializeField] RectTransform mapContent;
    [SerializeField] TextMeshProUGUI warFeedText;
    [SerializeField] float mapWorldMax = 1000f;
    [SerializeField] float mapMargin = 40f;

    readonly List<Image> _warMarkers = new List<Image>();

    void OnEnable()
    {
        ResolveRefsIfNeeded();
        TrySubscribe();
        RefreshView();
    }

    void OnDisable()
    {
        Unsubscribe();
        KillAllMarkerTweens();
    }

    void ResolveRefsIfNeeded()
    {
        if (mapContent == null)
        {
            var t = transform.Find("MapViewRoot/Viewport/Content");
            if (t != null) mapContent = t as RectTransform;
        }

        if (warFeedText == null)
            warFeedText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void TrySubscribe()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateTicked -= RefreshView;
        dm.OnStateDataReady -= RefreshView;
        dm.OnHomeCastleChanged -= RefreshView;
        dm.OnStateTicked += RefreshView;
        dm.OnStateDataReady += RefreshView;
        dm.OnHomeCastleChanged += RefreshView;
    }

    void Unsubscribe()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateTicked -= RefreshView;
        dm.OnStateDataReady -= RefreshView;
        dm.OnHomeCastleChanged -= RefreshView;
    }

    void RefreshView()
    {
        RefreshMarkers();
        RefreshWarFeed();
    }

    void RefreshMarkers()
    {
        if (mapContent == null) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null)
        {
            HideAllMarkers();
            return;
        }

        float w = mapContent.rect.width;
        float h = mapContent.rect.height;
        if (w < 2f || h < 2f)
        {
            w = mapContent.sizeDelta.x;
            h = mapContent.sizeDelta.y;
        }

        int i = 0;
        foreach (var kv in dm.castleStateDataMap)
        {
            var st = kv.Value;
            if (st == null || !st.isWar) continue;
            if (!dm.castleMasterDataMap.TryGetValue(kv.Key, out var master) || master == null) continue;

            var marker = GetMarkerAt(i++);
            var rt = marker.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = CastleMapCoordinateConverter.NormalizedWorldToAnchoredPosition(
                master.posX, master.posY, mapWorldMax, w, h, mapMargin);

            marker.gameObject.SetActive(true);
            marker.color = FactionTint(st.currentLord);
            marker.transform.DOKill();
            marker.transform.localScale = Vector3.one;
            marker.transform.DOScale(1.28f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        for (int k = i; k < _warMarkers.Count; k++)
        {
            _warMarkers[k].transform.DOKill();
            _warMarkers[k].gameObject.SetActive(false);
        }
    }

    void RefreshWarFeed()
    {
        if (warFeedText == null) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null)
        {
            warFeedText.text = "전황 데이터 준비 중...";
            return;
        }

        var sb = new StringBuilder(256);
        int count = 0;
        foreach (var kv in dm.castleStateDataMap)
        {
            var st = kv.Value;
            if (st == null || !st.isWar) continue;
            string cid = kv.Key;
            string name = dm.GetCastleDisplayName(cid);
            if (string.IsNullOrWhiteSpace(name))
                name = cid;
            sb.Append(" - ")
                .Append(DataManager.GetFactionLordShortLabel(st.currentLord))
                .Append("군이 ")
                .Append(name)
                .Append("을 공격 중")
                .Append('\n');
            count++;
            if (count >= 6) break;
        }

        warFeedText.text = count <= 0 ? "현재 대륙은 전면전 없이 잠잠합니다." : sb.ToString().TrimEnd();
    }

    Image GetMarkerAt(int index)
    {
        while (_warMarkers.Count <= index)
            _warMarkers.Add(CreateMarker(mapContent));
        return _warMarkers[index];
    }

    static Image CreateMarker(RectTransform parent)
    {
        var go = new GameObject("WarMarker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(26f, 26f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.28f, 0.28f, 0.94f);
        return img;
    }

    void HideAllMarkers()
    {
        for (int i = 0; i < _warMarkers.Count; i++)
        {
            if (_warMarkers[i] == null) continue;
            _warMarkers[i].transform.DOKill();
            _warMarkers[i].gameObject.SetActive(false);
        }
    }

    void KillAllMarkerTweens()
    {
        for (int i = 0; i < _warMarkers.Count; i++)
        {
            if (_warMarkers[i] == null) continue;
            _warMarkers[i].transform.DOKill();
            _warMarkers[i].transform.localScale = Vector3.one;
        }
    }

    static Color FactionTint(Faction faction)
    {
        switch (faction)
        {
            case Faction.WEI: return new Color(0.86f, 0.30f, 0.28f, 0.96f);
            case Faction.SHU: return new Color(0.30f, 0.78f, 0.34f, 0.96f);
            case Faction.WU: return new Color(0.30f, 0.50f, 0.90f, 0.96f);
            default: return new Color(0.85f, 0.65f, 0.22f, 0.96f);
        }
    }
}
