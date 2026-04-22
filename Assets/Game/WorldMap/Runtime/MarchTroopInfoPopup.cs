using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>행군 마커(동그라미) 클릭 시 장수·병력·목표·상태를 표시합니다.</summary>
public class MarchTroopInfoPopup : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] TMP_Text infoText;
    [SerializeField] Button closeButton;

    static MarchTroopInfoPopup _instance;

    void Awake()
    {
        _instance = this;
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    void Start()
    {
        Hide();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static void ShowDetails(string generalName, int army, string targetCastle, bool arrived)
    {
        if (_instance == null)
            _instance = UnityEngine.Object.FindFirstObjectByType<MarchTroopInfoPopup>();

        _instance?.Apply(generalName, army, targetCastle, arrived);
    }

    public static void ShowSiegeBattle(string generalName, int attackerTroops, int defenderTroops, string defenderCastleName)
    {
        if (_instance == null)
            _instance = UnityEngine.Object.FindFirstObjectByType<MarchTroopInfoPopup>();

        _instance?.ApplySiege(generalName, attackerTroops, defenderTroops, defenderCastleName);
    }

    void ApplySiege(string generalName, int attackerTroops, int defenderTroops, string defenderCastleName)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (infoText != null)
        {
            infoText.text =
                $"장수: {generalName}\n" +
                $"공격 병력(야전): {attackerTroops:N0}\n" +
                $"수비 병력(성내): {defenderTroops:N0}\n" +
                $"공성 대상: {defenderCastleName}\n" +
                $"상태: 공성 중(게임 시간 1시간마다 양측 병력이 줄어듭니다)";
            WorldMapTmpFontSupport.Apply(infoText);
        }
    }

    void Apply(string generalName, int army, string targetCastle, bool arrived)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (infoText != null)
        {
            string state = arrived ? "도착 · 목표 성 공격 중" : "행군 중";
            infoText.text =
                $"장수: {generalName}\n" +
                $"병력: {army:N0}\n" +
                $"목표 성: {targetCastle}\n" +
                $"상태: {state}";
            WorldMapTmpFontSupport.Apply(infoText);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
