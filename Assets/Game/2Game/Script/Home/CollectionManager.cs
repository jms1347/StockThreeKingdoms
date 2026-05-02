using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 성문 앞 금화 더미(시간 단계) 및 수거 연출. HUD 입금은 <see cref="HomeController.OnWallClicked"/>에서 처리.
/// </summary>
public class CollectionManager : MonoBehaviour
{
    [Header("참조")]
    public HomeController homeController;
    [Tooltip("더미가 배치된 영역 — Burst 스케일 타깃")]
    public RectTransform pileBurstRoot;
    [Tooltip("금화 더미 부모(비우면 자동 탐색)")]
    public RectTransform goldPilesParent;
    [Tooltip("레거시: HomeSceneLayoutWizard 등에서 더미 영역으로 할당. 비우면 goldPilesParent와 동일 취급 가능.")]
    public RectTransform pileArea;

    [Header("레거시·에디터 마법사 (선택)")]
    [Tooltip("비행 아이콘 프리팹 — 현재 본영 연출에서는 미사용, 마법사 직렬화 호환용.")]
    public GameObject flyingGoldPrefab;
    public RectTransform flyIconsRoot;
    public RectTransform goldFlyTarget;
    [Range(10, 32)] public int poolSize = 12;

    [Header("더미 아이콘 (각 8개)")]
    public GameObject[] goldPiles = new GameObject[8];

    [Header("플로팅 텍스트")]
    public TMP_FontAsset floatingFont;
    public Color floatingGoldColor = new Color(1f, 0.92f, 0.35f);

    HomeUIController _homeUi;

    void Awake()
    {
        if (homeController == null)
            homeController = GetComponent<HomeController>() ?? GetComponentInParent<HomeController>();
        _homeUi = GetComponent<HomeUIController>() ?? GetComponentInParent<HomeUIController>();
        EnsurePileReferences();
    }

    void EnsurePileReferences()
    {
        if (goldPilesParent == null && pileArea != null)
            goldPilesParent = pileArea;
        if (goldPilesParent == null)
        {
            var t = transform.Find("GoldPileDock/PilesGrid")
                    ?? transform.Find("GoldPile_Root")
                    ?? transform.Find("MarketWarehouse");
            if (t != null) goldPilesParent = t as RectTransform;
        }

        if ((goldPiles == null || goldPiles.Length == 0 || CountNonNull(goldPiles) == 0) && goldPilesParent != null)
        {
            var list = new List<GameObject>();
            for (int i = 0; i < 8; i++)
            {
                var c = goldPilesParent.Find($"GoldPile_{i}");
                if (c != null) list.Add(c.gameObject);
            }
            if (list.Count > 0)
                goldPiles = list.ToArray();
        }

        if (pileBurstRoot == null)
            pileBurstRoot = pileArea != null ? pileArea : goldPilesParent;
    }

    static int CountNonNull(GameObject[] a)
    {
        if (a == null) return 0;
        int n = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != null) n++;
        return n;
    }

    void Update()
    {
        UpdatePileVisuals();
    }

    void OnDestroy()
    {
        DOTween.Kill(this, true);
    }

    public void UpdatePileVisuals()
    {
        int tier = homeController != null ? homeController.GetGoldPileActiveCount() : 0;
        SetPileArray(goldPiles, tier);
    }

    static void SetPileArray(GameObject[] piles, int activeCount)
    {
        if (piles == null) return;
        for (int i = 0; i < piles.Length; i++)
        {
            if (piles[i] == null) continue;
            piles[i].SetActive(i < activeCount);
        }
    }

    /// <summary>주머니가 있었을 때 더미 튀어 오르는 연출 후 숨김.</summary>
    public void PlayPocketBurstThenHidePiles()
    {
        var rt = pileBurstRoot != null ? pileBurstRoot : goldPilesParent;
        if (rt != null)
        {
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOPunchScale(Vector3.one * 0.35f, 0.45f, 8, 0.5f).SetUpdate(true)
                .OnComplete(() => rt.localScale = Vector3.one);
        }

        SetPileArray(goldPiles, 0);
    }

    public void PlayFloatingGainText(long totalGain)
    {
        if (totalGain <= 0) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("FloatingGainText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = $"+{totalGain:N0} Gold";
        tmp.fontSize = 46;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = floatingGoldColor;
        if (floatingFont != null) tmp.font = floatingFont;

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.42f);
        r.sizeDelta = new Vector2(520f, 100f);
        r.anchoredPosition = Vector2.zero;
        r.localScale = Vector3.one * 0.6f;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(r.DOAnchorPosY(r.anchoredPosition.y + 140f, 0.85f).SetEase(Ease.OutCubic));
        seq.Join(r.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
        seq.Join(tmp.DOFade(0f, 0.65f).SetDelay(0.35f));
        seq.OnComplete(() => Destroy(go));
    }

    /// <summary>레거시 호환용 — 비행 입금은 더 이상 사용하지 않음.</summary>
    public bool IsFlyBusy => false;
}
