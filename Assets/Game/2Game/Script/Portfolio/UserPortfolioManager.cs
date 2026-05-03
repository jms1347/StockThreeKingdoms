using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
    public bool isWar;
    /// <summary>대략적 미실현 손익: (호가−평단)×병력.</summary>
    public float unrealizedPnLGold;
}

/// <summary>
/// 천하에서 매수한 병력(성별 주둔) 조회 및 포트폴리오 탭 UI(HTS·전쟁 대응 센터).
/// </summary>
public class UserPortfolioManager : MonoBehaviour
{
    const float QuickBuyGoldFraction = 0.2f;
    const float RoiCrashDeltaThreshold = 2f;
    [SerializeField] Color profitNeonColor = new Color(0.2f, 1f, 0.45f, 1f);
    [SerializeField] Color lossRedColor = new Color(1f, 0.2f, 0.25f, 1f);
    [SerializeField] Color panelBgDark = new Color(0.04f, 0.06f, 0.12f, 0.96f);

    [Header("Header · TotalStats")]
    [SerializeField] TextMeshProUGUI headerTotalSoldiersText;
    [SerializeField] TextMeshProUGUI headerUnrealizedPnLText;
    [SerializeField] TextMeshProUGUI headerMaintenanceText;
    [Tooltip("구버전 단일 요약. 위 헤더가 비었을 때만 사용됩니다.")]
    [SerializeField] TextMeshProUGUI headerSummaryText;

    [Header("Section 1 · Emergency (War)")]
    [SerializeField] GameObject warZoneRoot;
    [SerializeField] ScrollRect warZoneScroll;
    [Tooltip("비우면 warZoneScroll.content 사용")]
    [SerializeField] RectTransform warZoneContent;
    [Tooltip("비우면 런타임 카드 생성")]
    [SerializeField] GameObject warCardPrefab;

    [Header("Section 2 · General")]
    [SerializeField] ScrollRect generalScrollRect;
    [Tooltip("비우면 generalScrollRect.content 또는 레거시 contentRoot")]
    [SerializeField] RectTransform generalContent;
    [SerializeField] GameObject generalLinePrefab;

    [Header("Legacy (단일 스크롤)")]
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform contentRoot;
    [SerializeField] GameObject linePrefab;

    [Header("Stress · 정오·급락 피드백")]
    [SerializeField] CanvasGroup stressOverlay;
    [SerializeField] Image stressTintImage;
    [SerializeField] Color stressTintColor = new Color(0.85f, 0.05f, 0.05f, 0.22f);
    [SerializeField] float stressPulseDuration = 0.85f;

    [Header("전쟁 감지 연출")]
    [SerializeField] AudioClip warAlertClip;
    [SerializeField] ParticleSystem warAlertVfx;

    readonly Dictionary<string, float> _lastRoiPercent = new Dictionary<string, float>(StringComparer.Ordinal);
    readonly HashSet<string> _lastWarCastleSnapshot = new HashSet<string>(StringComparer.Ordinal);
    Tweener _stressTween;
    AudioSource _audio;
    bool _stressDangerActive;
    bool _warAlertBaselineReady;

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

    public static List<OwnedCastleStock> BuildHoldings(DataManager dm, Dictionary<string, float> lastRoiScratch = null)
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

            float pnl = (mark - s.averagePurchasePrice) * s.userDeployedTroops;

            list.Add(new OwnedCastleStock
            {
                castleId = s.id,
                ownedSoldiers = s.userDeployedTroops,
                averagePurchasePrice = s.averagePurchasePrice,
                currentMarkPrice = mark,
                roiPercent = roi,
                isWar = s.isWar,
                unrealizedPnLGold = pnl
            });
        }

        SortHoldings(list, lastRoiScratch);
        return list;
    }

    /// <summary>전쟁 중 &gt; 수익률 급변동 &gt; 보유 규모(평가액).</summary>
    static void SortHoldings(List<OwnedCastleStock> list, Dictionary<string, float> lastRoi)
    {
        list.Sort((a, b) =>
        {
            if (a.isWar != b.isWar)
                return a.isWar ? -1 : 1;
            float va = VolatilityScore(a, lastRoi);
            float vb = VolatilityScore(b, lastRoi);
            if (Mathf.Abs(va - vb) > 0.0001f)
                return vb.CompareTo(va);
            float sa = a.ownedSoldiers * Mathf.Max(0f, a.currentMarkPrice);
            float sb = b.ownedSoldiers * Mathf.Max(0f, b.currentMarkPrice);
            return sb.CompareTo(sa);
        });
    }

    static float VolatilityScore(OwnedCastleStock h, Dictionary<string, float> lastRoi)
    {
        if (lastRoi == null || string.IsNullOrEmpty(h.castleId)) return 0f;
        if (!lastRoi.TryGetValue(h.castleId, out float prev)) prev = h.roiPercent;
        return Mathf.Abs(h.roiPercent - prev);
    }

    void OnEnable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnStateTicked -= OnStateTicked;
        if (dm != null)
            dm.OnStateTicked += OnStateTicked;
        EnsureStressOverlay();
        Refresh();
    }

    void OnDisable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnStateTicked -= OnStateTicked;
        KillStressTween();
    }

    void OnDestroy() => KillStressTween();

    void OnStateTicked() => Refresh();

    void EnsureStressOverlay()
    {
        if (stressOverlay != null) return;
        var rt = transform as RectTransform;
        if (rt == null) return;
        var go = new GameObject("StressOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(rt, false);
        var stretch = go.GetComponent<RectTransform>();
        StretchFull(stretch);
        var img = go.GetComponent<Image>();
        img.color = stressTintColor;
        img.raycastTarget = false;
        stressOverlay = go.GetComponent<CanvasGroup>();
        stressOverlay.alpha = 0f;
        stressOverlay.blocksRaycasts = false;
        stressTintImage = img;
        go.transform.SetAsLastSibling();
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void Refresh()
    {
        var dm = DataManager.InstanceOrNull;
        var holdings = BuildHoldings(dm, _lastRoiPercent);

        float totalPnl = 0f;
        for (int i = 0; i < holdings.Count; i++)
            totalPnl += holdings[i].unrealizedPnLGold;

        long totalSoldiers = GetTotalOwnedSoldiers(dm);
        FillHeader(totalSoldiers, totalPnl);
        UpdateStressFeedback(holdings);

        var warList = new List<OwnedCastleStock>();
        var genList = new List<OwnedCastleStock>();
        for (int i = 0; i < holdings.Count; i++)
        {
            var h = holdings[i];
            if (h.isWar) warList.Add(h);
            else genList.Add(h);
        }

        DetectWarAlert(warList);

        RectTransform warParent = ResolveWarContent();
        RectTransform genParent = ResolveGeneralContent();
        if (genParent == null) return;

        EnsureVerticalLayout(genParent);
        if (warParent != null && warParent != genParent)
            EnsureVerticalLayout(warParent);

        bool separateWarUi = warParent != null && warParent != genParent;
        if (!separateWarUi)
            FillMergedScroll(genParent, warList, genList, dm);
        else
        {
            FillWarZone(warParent, warList, dm);
            FillGeneralZone(genParent, genList, dm);
        }

        for (int i = 0; i < holdings.Count; i++)
        {
            var h = holdings[i];
            _lastRoiPercent[h.castleId] = h.roiPercent;
        }

        Canvas.ForceUpdateCanvases();
        if (generalScrollRect != null)
            generalScrollRect.verticalNormalizedPosition = 1f;
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
        if (warZoneScroll != null)
            warZoneScroll.verticalNormalizedPosition = 1f;
    }

    void FillHeader(long totalSoldiers, float totalPnl)
    {
        if (headerTotalSoldiersText != null)
            headerTotalSoldiersText.text = $"총 병력 <b>{totalSoldiers:N0}</b>";
        if (headerUnrealizedPnLText != null)
        {
            headerUnrealizedPnLText.richText = true;
            string col = totalPnl >= 0f ? ColorUtilityToHex(profitNeonColor) : ColorUtilityToHex(lossRedColor);
            headerUnrealizedPnLText.text =
                $"미실현 손익 <b><color=#{col}>{totalPnl:+0;−0;0}</color></b> G";
        }

        if (headerMaintenanceText != null)
        {
            double amt = EconomyManager.InstanceOrNull != null
                ? EconomyManager.InstanceOrNull.ComputeNextSettlementGold()
                : 0d;
            headerMaintenanceText.richText = true;
            headerMaintenanceText.text =
                $"자정 일일 유지비 <b>{Utils.AbbreviateScore(amt)}</b> G · 남은 시간 {EconomyManager.FormatCountdownUntilNextDailySettlementHms()}";
        }

        if (headerSummaryText != null && headerTotalSoldiersText == null && headerUnrealizedPnLText == null)
        {
            headerSummaryText.richText = true;
            headerSummaryText.text =
                $"총 주둔 <b>{totalSoldiers:N0}</b> · 미실현 <b>{totalPnl:+0;−0;0}</b> G";
        }
    }

    static string ColorUtilityToHex(Color c)
    {
        Color32 x = c;
        return $"{x.r:x2}{x.g:x2}{x.b:x2}";
    }

    void UpdateStressFeedback(List<OwnedCastleStock> holdings)
    {
        bool crash = false;
        for (int i = 0; i < holdings.Count; i++)
        {
            var h = holdings[i];
            if (!_lastRoiPercent.TryGetValue(h.castleId, out float prev))
                prev = h.roiPercent;
            if (h.roiPercent - prev <= -RoiCrashDeltaThreshold)
            {
                crash = true;
                break;
            }
        }

        bool imminent = IsMaintenanceImminentWindow();
        bool danger = crash || imminent;
        if (danger == _stressDangerActive && _stressTween != null && _stressTween.IsActive())
            return;

        _stressDangerActive = danger;
        if (stressOverlay == null) return;

        KillStressTween();
        if (!danger)
        {
            stressOverlay.alpha = 0f;
            return;
        }

        stressOverlay.alpha = 0f;
        if (stressTintImage != null)
            stressTintImage.color = stressTintColor;

        _stressTween = stressOverlay.DOFade(stressTintColor.a * 0.95f, stressPulseDuration * 0.45f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    void KillStressTween()
    {
        _stressTween?.Kill();
        _stressTween = null;
        if (stressOverlay != null)
            stressOverlay.alpha = 0f;
    }

    /// <summary>로컬 11:30~12:00 — 정오 유지비 직전 구간.</summary>
    static bool IsMaintenanceImminentWindow()
    {
        var now = DateTime.Now;
        if (now.Hour == 11 && now.Minute >= 30) return true;
        return false;
    }

    void DetectWarAlert(List<OwnedCastleStock> warList)
    {
        var current = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < warList.Count; i++)
            current.Add(warList[i].castleId.Trim());

        bool newWar = false;
        foreach (var id in current)
        {
            if (!_lastWarCastleSnapshot.Contains(id))
                newWar = true;
        }

        if (warZoneRoot != null)
            warZoneRoot.SetActive(warList.Count > 0);

        if (!_warAlertBaselineReady)
        {
            _warAlertBaselineReady = true;
        }
        else if (newWar && warList.Count > 0)
        {
            PlayWarAlertFx();
        }

        _lastWarCastleSnapshot.Clear();
        foreach (var id in current)
            _lastWarCastleSnapshot.Add(id);
    }

    void PlayWarAlertFx()
    {
        if (warAlertClip != null)
        {
            if (_audio == null)
            {
                _audio = GetComponent<AudioSource>();
                if (_audio == null)
                    _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
            }

            _audio.PlayOneShot(warAlertClip);
        }

        if (warAlertVfx != null)
            warAlertVfx.Play();
    }

    RectTransform ResolveWarContent()
    {
        if (warZoneContent != null) return warZoneContent;
        if (warZoneScroll != null && warZoneScroll.content != null) return warZoneScroll.content;
        return null;
    }

    RectTransform ResolveGeneralContent()
    {
        if (generalContent != null) return generalContent;
        if (generalScrollRect != null && generalScrollRect.content != null) return generalScrollRect.content;
        if (scrollRect != null && scrollRect.content != null) return scrollRect.content;
        return contentRoot;
    }

    void FillMergedScroll(RectTransform root, List<OwnedCastleStock> war, List<OwnedCastleStock> gen, DataManager dm)
    {
        ClearContent(root);
        if (war.Count > 0)
            CreateSectionLabel(root, "⚔ 전쟁 구역 · 즉시 대응", lossRedColor);
        for (int i = 0; i < war.Count; i++)
            CreateWarCard(root, war[i], dm, warCardPrefab != null);
        if (war.Count > 0 && gen.Count > 0)
            CreateSectionLabel(root, "일반 포지션", profitNeonColor);
        for (int i = 0; i < gen.Count; i++)
            CreateGeneralRow(root, gen[i], dm);
        if (war.Count == 0 && gen.Count == 0)
            CreateEmptyHint(root);
    }

    void FillWarZone(RectTransform warParent, List<OwnedCastleStock> war, DataManager dm)
    {
        if (warParent == null) return;
        ClearContent(warParent);
        if (warZoneRoot != null)
            warZoneRoot.SetActive(war.Count > 0);
        for (int i = 0; i < war.Count; i++)
            CreateWarCard(warParent, war[i], dm, warCardPrefab != null);
    }

    void FillGeneralZone(RectTransform genParent, List<OwnedCastleStock> gen, DataManager dm)
    {
        ClearContent(genParent);
        for (int i = 0; i < gen.Count; i++)
            CreateGeneralRow(genParent, gen[i], dm);
        if (gen.Count == 0 && (warZoneRoot == null || !warZoneRoot.activeSelf))
            CreateEmptyHint(genParent);
    }

    void CreateSectionLabel(RectTransform parent, string line, Color col)
    {
        var go = new GameObject("Sec_" + line, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 32f;
        le.preferredHeight = 32f;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = col;
        tmp.text = line;
        tmp.alignment = TextAlignmentOptions.Left;
    }

    void CreateEmptyHint(RectTransform parent)
    {
        var go = new GameObject("Empty", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 24f;
        tmp.color = new Color(0.55f, 0.6f, 0.68f);
        tmp.text = "보유 거점이 없습니다. 천하 탭에서 AI 수비군을 매수하세요.";
        tmp.enableWordWrapping = true;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 64f;
    }

    void CreateWarCard(RectTransform parent, OwnedCastleStock h, DataManager dm, bool usePrefab)
    {
        if (usePrefab && warCardPrefab != null)
        {
            var row = Instantiate(warCardPrefab, parent);
            row.SetActive(true);
            ApplyWarCard(row, h, dm);
            return;
        }

        var root = new GameObject("War_" + h.castleId, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        root.GetComponent<Image>().color = panelBgDark;
        var le = root.GetComponent<LayoutElement>();
        le.minHeight = 108f;
        le.preferredHeight = 108f;

        var hor = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var hrt = hor.GetComponent<RectTransform>();
        hrt.SetParent(rt, false);
        StretchFull(hrt);
        var hg = hor.GetComponent<HorizontalLayoutGroup>();
        hg.padding = new RectOffset(12, 12, 10, 10);
        hg.spacing = 10f;
        hg.childAlignment = TextAnchor.MiddleLeft;
        hg.childForceExpandWidth = false;
        hg.childForceExpandHeight = true;
        hg.childControlWidth = true;
        hg.childControlHeight = true;

        string name = dm != null ? dm.GetCastleDisplayName(h.castleId) : h.castleId;
        if (string.IsNullOrWhiteSpace(name)) name = h.castleId;
        string roiCol = h.roiPercent >= 0f ? ColorUtilityToHex(profitNeonColor) : ColorUtilityToHex(lossRedColor);
        var txtGo = new GameObject("Info", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        txtGo.transform.SetParent(hrt, false);
        var inf = txtGo.GetComponent<TextMeshProUGUI>();
        inf.richText = true;
        inf.fontSize = 20f;
        inf.alignment = TextAlignmentOptions.MidlineLeft;
        inf.text =
            $"<b>{name}</b> <color=#8899aa>({h.castleId})</color>\n" +
            $"주둔 <b>{h.ownedSoldiers:N0}</b> · 호가 {h.currentMarkPrice:0.#} G · " +
            $"<color=#{roiCol}>ROI {h.roiPercent:+#0.0;-#0.0;0}%</color>";
        var infLe = txtGo.GetComponent<LayoutElement>();
        infLe.flexibleWidth = 1f;
        infLe.minWidth = 120f;

        CreateWarButton(hrt, "BuyMore", "추가매수 20%", () => QuickAction(h.castleId, true));
        CreateWarButton(hrt, "SellAll", "전량 회수", () => QuickAction(h.castleId, false));
    }

    void CreateWarButton(RectTransform parent, string name, string label, Action onClick)
    {
        var bgo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        bgo.transform.SetParent(parent, false);
        var img = bgo.GetComponent<Image>();
        img.color = name == "SellAll" ? new Color(0.45f, 0.1f, 0.12f, 0.95f) : new Color(0.1f, 0.28f, 0.18f, 0.95f);
        var btn = bgo.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        var ble = bgo.GetComponent<LayoutElement>();
        ble.preferredWidth = 132f;
        ble.minHeight = 44f;
        ble.preferredHeight = 44f;
        var tgo = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI));
        tgo.transform.SetParent(bgo.transform, false);
        var tmp = tgo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        StretchFull(tgo.GetComponent<RectTransform>());
    }

    void ApplyWarCard(GameObject row, OwnedCastleStock h, DataManager dm)
    {
        var tmp = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            string name = dm != null ? dm.GetCastleDisplayName(h.castleId) : h.castleId;
            string roiCol = h.roiPercent >= 0f ? ColorUtilityToHex(profitNeonColor) : ColorUtilityToHex(lossRedColor);
            tmp.richText = true;
            tmp.text =
                $"<b>{name}</b> · ROI <color=#{roiCol}>{h.roiPercent:+#0.0;-#0.0;0}%</color>";
        }

        var buttons = row.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            b.onClick.RemoveAllListeners();
            if (n.Contains("buy") || n.Contains("more"))
                b.onClick.AddListener(() => QuickAction(h.castleId, true));
            else if (n.Contains("sell") || n.Contains("all"))
                b.onClick.AddListener(() => QuickAction(h.castleId, false));
        }
    }

    void CreateGeneralRow(RectTransform parent, OwnedCastleStock h, DataManager dm)
    {
        var pref = generalLinePrefab != null ? generalLinePrefab : linePrefab;
        if (pref != null)
        {
            var row = Instantiate(pref, parent);
            row.SetActive(true);
            ApplyGeneralRow(row, h, dm);
            return;
        }

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
        ApplyGeneralRow(go, h, dm);
    }

    static void ApplyGeneralRow(GameObject row, OwnedCastleStock h, DataManager dm)
    {
        var tmp = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;
        string name = dm != null ? dm.GetCastleDisplayName(h.castleId) : h.castleId;
        if (string.IsNullOrWhiteSpace(name)) name = h.castleId;
        string roiCol = h.roiPercent >= 0f ? "#00ff88" : "#ff4444";
        tmp.richText = true;
        tmp.text =
            $"<b>{name}</b>  <color=#8899aa>({h.castleId})</color>\n" +
            $"주둔 <b>{h.ownedSoldiers:N0}</b> · 평단 {h.averagePurchasePrice:0.#} G · 호가 {h.currentMarkPrice:0.#} G · " +
            $"<color={roiCol}>ROI {h.roiPercent:+#0.0;-#0.0;0}%</color> · 손익 {h.unrealizedPnLGold:+0;−0;0} G";
    }

    static void EnsureVerticalLayout(RectTransform root)
    {
        if (root == null) return;
        if (root.GetComponent<VerticalLayoutGroup>() == null)
        {
            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            v.spacing = 6;
            v.padding = new RectOffset(10, 10, 8, 12);
        }

    }

    static void ClearContent(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (Application.isPlaying)
                Destroy(c.gameObject);
            else
                DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>팝업 없이 즉시 매수(보유 금화의 20%) 또는 전량 매도.</summary>
    public void QuickAction(string castleId, bool isBuy)
    {
        castleId = castleId?.Trim();
        if (string.IsNullOrEmpty(castleId)) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return;

        if (isBuy)
            dm.TryQuickBuyWithFractionOfGold(castleId, QuickBuyGoldFraction);
        else
            dm.RecallUserCastleDeployment(castleId);

        GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
        Refresh();
    }
}
