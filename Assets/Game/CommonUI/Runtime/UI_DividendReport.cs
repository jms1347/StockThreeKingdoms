using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>주간 배당 지급 요약 팝업. 씬에 인스턴스를 두고 <see cref="InstanceOrNull"/>로 표시합니다.</summary>
public sealed class UI_DividendReport : MonoBehaviour
{
    public static UI_DividendReport InstanceOrNull { get; private set; }

    [SerializeField] GameObject panelRoot;
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Button closeButton;

    void Awake()
    {
        if (InstanceOrNull != null && InstanceOrNull != this)
        {
            Debug.LogWarning("[UI_DividendReport] 중복 인스턴스 — 이 오브젝트를 제거하세요.");
            return;
        }

        InstanceOrNull = this;
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
    }

    public void ShowReport(long totalGold, IReadOnlyList<DividendPayoutLine> lines)
    {
        if (panelRoot == null || bodyText == null)
        {
            Debug.LogWarning("[UI_DividendReport] panelRoot/bodyText 미할당");
            return;
        }

        var sb = new StringBuilder(256);
        sb.AppendLine("<b>주간 배당</b>");
        sb.AppendLine($"합계 <color=#E8C44A>{totalGold:N0}</color> 금\n");
        if (lines == null || lines.Count == 0)
            sb.AppendLine("이번 정산에서 받은 배당이 없습니다.");
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                sb.AppendLine(
                    $"· {l.castleDisplayName} <color=#E8C44A>+{l.gold:N0}</color> 금 <size=85%>(풀 {l.poolBefore:N0})</size>");
            }
        }

        bodyText.text = sb.ToString();
        panelRoot.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
