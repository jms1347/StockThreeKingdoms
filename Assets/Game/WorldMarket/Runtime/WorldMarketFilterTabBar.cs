using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하 탭 성 리스트 상단 필터(MTS 탭 바). <see cref="WorldMarketCastleVirtualList.SetFilter"/> 연동 및 선택 하이라이트.
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketFilterTabBar : MonoBehaviour
{
    [Serializable]
    public class TabBinding
    {
        public Button button;
        public WorldMarketCastleListFilter filter;
    }

    [Tooltip("비우면 부모·조상에서 WorldMarketCastleVirtualList 검색")]
    [SerializeField] WorldMarketCastleVirtualList castleList;

    [SerializeField] TabBinding[] tabs;

    [SerializeField] Color normalColor = new Color(0.14f, 0.16f, 0.20f, 0.96f);
    [SerializeField] Color selectedColor = new Color(0.22f, 0.38f, 0.62f, 0.98f);
    [SerializeField] Color normalLabelColor = new Color(0.78f, 0.80f, 0.84f, 1f);
    [SerializeField] Color selectedLabelColor = Color.white;

    void Awake()
    {
        if (castleList == null)
            castleList = GetComponentInParent<WorldMarketCastleVirtualList>();
        if (castleList == null)
            castleList = UnityEngine.Object.FindObjectOfType<WorldMarketCastleVirtualList>();
    }

    void Start()
    {
        if (tabs == null) return;
        foreach (var t in tabs)
        {
            if (t?.button == null) continue;
            var f = t.filter;
            t.button.onClick.RemoveAllListeners();
            t.button.onClick.AddListener(() => OnTabClicked(f));
        }
    }

    void OnEnable()
    {
        if (castleList != null)
            castleList.FilterChanged += OnCastleListFilterChanged;
        RefreshHighlight();
    }

    void OnDisable()
    {
        if (castleList != null)
            castleList.FilterChanged -= OnCastleListFilterChanged;
    }

    void OnCastleListFilterChanged(WorldMarketCastleListFilter _) => RefreshHighlight();

    void OnTabClicked(WorldMarketCastleListFilter filter)
    {
        if (castleList == null)
            castleList = GetComponentInParent<WorldMarketCastleVirtualList>();
        castleList?.SetFilter(filter);
        RefreshHighlight();
        ForceLayoutStable();
    }

    void ForceLayoutStable()
    {
        Canvas.ForceUpdateCanvases();
        var rt = transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            if (rt.parent is RectTransform prt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
        }
    }

    void RefreshHighlight()
    {
        WorldMarketCastleListFilter current = castleList != null
            ? castleList.CurrentFilter
            : WorldMarketCastleListFilter.All;

        if (tabs == null) return;
        foreach (var t in tabs)
        {
            if (t?.button == null) continue;
            bool sel = t.filter == current;
            var img = t.button.GetComponent<Image>();
            if (img != null)
                img.color = sel ? selectedColor : normalColor;

            var label = t.button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.color = sel ? selectedLabelColor : normalLabelColor;
        }
    }
}
