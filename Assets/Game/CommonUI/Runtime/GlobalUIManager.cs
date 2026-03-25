using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 씬에서 공통으로 유지되는 상단바/하단 탭바 UI.
/// SingletonLoader에서 프리팹을 Load하면 씬 전환에도 유지됩니다.
/// </summary>
public class GlobalUIManager : Singleton<GlobalUIManager>
{
    [Header("Top Bar")]
    [SerializeField] RectTransform topBarRoot;
    [SerializeField] TextMeshProUGUI userNameText;
    [SerializeField] TextMeshProUGUI totalAssetsText;
    [SerializeField] TextMeshProUGUI foodText;

    [Header("Bottom Tab Bar (5)")]
    [SerializeField] RectTransform bottomTabRoot;
    [SerializeField] Button homeButton;
    [SerializeField] Button marketButton;
    [SerializeField] Button portfolioButton;
    [SerializeField] Button newsButton;
    [SerializeField] Button ordersButton;

    public event Action<string> TabSelected;

    protected override void Awake()
    {
        base.Awake();
        WireTabs();
    }

    void WireTabs()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => TabSelected?.Invoke("Home"));
        if (marketButton != null) marketButton.onClick.AddListener(() => TabSelected?.Invoke("Market"));
        if (portfolioButton != null) portfolioButton.onClick.AddListener(() => TabSelected?.Invoke("Portfolio"));
        if (newsButton != null) newsButton.onClick.AddListener(() => TabSelected?.Invoke("News"));
        if (ordersButton != null) ordersButton.onClick.AddListener(() => TabSelected?.Invoke("Orders"));
    }

    public void SetTopBar(string userName, string totalAssets, string food)
    {
        if (userNameText != null) userNameText.text = userName;
        if (totalAssetsText != null) totalAssetsText.text = totalAssets;
        if (foodText != null) foodText.text = food;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}

