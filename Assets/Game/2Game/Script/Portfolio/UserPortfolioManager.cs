using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>성별 유저 주둔(천하 AMM 매수분) 스냅샷. 실제 저장 원본은 <see cref="CastleStateData"/>.</summary>
[Serializable]
public struct OwnedCastleStock
{
    public string castleId;
    public int ownedSoldiers;
    public float averagePurchasePrice;
    /// <summary>현재 1단위 매수 한계 호가(금화).</summary>
    public float currentMarkPrice;
    /// <summary>평단 대비 현재 호가 수익률(%).</summary>
    public float roiPercent;
}

/// <summary>
/// 천하에서 매수한 병력(성별 주둔) 조회 및 포트폴리오 탭 UI 갱신.
/// </summary>
public class UserPortfolioManager : MonoBehaviour
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform contentRoot;
    [SerializeField] TextMeshProUGUI headerSummaryText;
    [Tooltip("비우면 런타임에 TextMeshProUGUI 행만 생성합니다.")]
    [SerializeField] GameObject linePrefab;

    /// <summary>모든 성 중 <see cref="CastleStateData.userDeployedTroops"/> 합계.</summary>
    public static long GetTotalOwnedSoldiers(DataManager dm)
    {
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null) return 0L;
        long sum = 0;
        foreach (var kv in dm.castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            sum += Math.Max(0, s.userDeployedTroops);
        }

        return sum;
    }

    public static List<OwnedCastleStock> BuildHoldings(DataManager dm)
    {
        var list = new List<OwnedCastleStock>();
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null) return list;

        foreach (var kv in dm.castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || s.userDeployedTroops <= 0) continue;

            float mark = dm.EvaluateCastleQuoteForCastle(s.id);
            float roi = 0f;
            if (s.averagePurchasePrice > 1e-4f)
                roi = (mark - s.averagePurchasePrice) / s.averagePurchasePrice * 100f;

            list.Add(new OwnedCastleStock
            {
                castleId = s.id,
                ownedSoldiers = s.userDeployedTroops,
                averagePurchasePrice = s.averagePurchasePrice,
                currentMarkPrice = mark,
                roiPercent = roi
            });
        }

        list.Sort((a, b) => string.CompareOrdinal(a.castleId, b.castleId));
        return list;
    }

    void OnEnable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnStateTicked -= OnStateTicked;
        if (dm != null)
            dm.OnStateTicked += OnStateTicked;
        Refresh();
    }

    void OnDisable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnStateTicked -= OnStateTicked;
    }

    void OnStateTicked() => Refresh();

    public void Refresh()
    {
        var dm = DataManager.InstanceOrNull;
        var holdings = BuildHoldings(dm);
        long total = GetTotalOwnedSoldiers(dm);

        if (headerSummaryText != null)
        {
            headerSummaryText.richText = true;
            if (holdings.Count == 0)
                headerSummaryText.text = "보유 거점이 없습니다.\n천하 탭에서 AI 수비군을 매수하세요.";
            else
            {
                float avgRoi = 0f;
                for (int i = 0; i < holdings.Count; i++)
                    avgRoi += holdings[i].roiPercent;
                avgRoi /= holdings.Count;
                headerSummaryText.text =
                    $"총 주둔 <b>{total:N0}</b>명 · 거점 <b>{holdings.Count}</b>성 · 평균 수익률 <b>{avgRoi:+#0.0;-#0.0;0}%</b>";
            }
        }

        var root = contentRoot != null ? contentRoot : scrollRect != null ? scrollRect.content : null;
        if (root == null) return;

        if (root.GetComponent<VerticalLayoutGroup>() == null)
        {
            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            v.spacing = 4;
            v.padding = new RectOffset(8, 8, 8, 8);
        }

        ClearContent(root);

        if (linePrefab != null)
        {
            foreach (var h in holdings)
            {
                var row = Instantiate(linePrefab, root);
                row.SetActive(true);
                ApplyRow(row, h, dm);
            }
        }
        else
        {
            foreach (var h in holdings)
                CreateRuntimeRow(root, h, dm);
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    static void ClearContent(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (Application.isPlaying)
                Destroy(c.gameObject);
            else
                DestroyImmediate(c.gameObject);
        }
    }

    static void ApplyRow(GameObject row, OwnedCastleStock h, DataManager dm)
    {
        var tmp = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;
        string name = dm != null ? dm.GetCastleDisplayName(h.castleId) : h.castleId;
        if (string.IsNullOrWhiteSpace(name)) name = h.castleId;
        string roiCol = h.roiPercent >= 0f ? "#7dffb0" : "#ff8a8a";
        tmp.richText = true;
        tmp.text =
            $"<b>{name}</b>  <color=#8899aa>({h.castleId})</color>\n" +
            $"주둔 <b>{h.ownedSoldiers:N0}</b>명 · 평단 {h.averagePurchasePrice:0.#} G · 호가 {h.currentMarkPrice:0.#} G · " +
            $"<color={roiCol}>수익률 {h.roiPercent:+#0.0;-#0.0;0}%</color>";
    }

    static void CreateRuntimeRow(RectTransform parent, OwnedCastleStock h, DataManager dm)
    {
        var go = new GameObject("Row_" + h.castleId, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 56f;
        le.preferredHeight = 56f;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.margin = new Vector4(8, 4, 8, 4);
        ApplyRow(go, h, dm);
    }
}
