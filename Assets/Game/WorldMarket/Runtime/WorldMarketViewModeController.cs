using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 탭 — 지도 보기 / 리스트 보기 전환.</summary>
[DisallowMultipleComponent]
public class WorldMarketViewModeController : MonoBehaviour
{
    const string PrefsKey = "WorldMarketViewMode";
    const int ModeList = 0;
    const int ModeMap = 1;
    const int ModeWar = 2;

    [SerializeField] GameObject mapViewRoot;
    [SerializeField] GameObject listViewRoot;
    [SerializeField] GameObject warMapViewRoot;
    [SerializeField] Toggle mapToggle;
    [SerializeField] Toggle listToggle;
    [SerializeField] Toggle warToggle;

    void Awake()
    {
        if (mapViewRoot == null || listViewRoot == null || warMapViewRoot == null)
            AutoResolveRoots();

        if (mapToggle != null)
        {
            mapToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ApplyMode(ModeMap, true);
            });
        }

        if (listToggle != null)
        {
            listToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ApplyMode(ModeList, true);
            });
        }

        if (warToggle != null)
        {
            warToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ApplyMode(ModeWar, true);
            });
        }

        int savedMode = PlayerPrefs.GetInt(PrefsKey, ModeList);
        if (savedMode < ModeList || savedMode > ModeWar)
            savedMode = ModeList;
        if (savedMode == ModeWar && warMapViewRoot == null)
            savedMode = ModeList;

        ApplyMode(savedMode, false);
        if (mapToggle != null)
            mapToggle.SetIsOnWithoutNotify(savedMode == ModeMap);
        if (listToggle != null)
            listToggle.SetIsOnWithoutNotify(savedMode == ModeList);
        if (warToggle != null)
            warToggle.SetIsOnWithoutNotify(savedMode == ModeWar);
    }

    void AutoResolveRoots()
    {
        var t = transform;
        for (int i = 0; i < 8 && t != null; i++, t = t.parent)
        {
            var mv = t.Find("MapViewRoot");
            var lv = t.Find("ListViewRoot");
            var wv = t.Find("WarMapViewRoot");
            if (mv != null) mapViewRoot = mv.gameObject;
            if (lv != null) listViewRoot = lv.gameObject;
            if (wv != null) warMapViewRoot = wv.gameObject;
            if (mapViewRoot != null && listViewRoot != null && warMapViewRoot != null)
                return;
        }

        if (mapToggle == null)
            mapToggle = FindToggleByName("MapToggle");
        if (listToggle == null)
            listToggle = FindToggleByName("ListToggle");
        if (warToggle == null)
            warToggle = FindToggleByName("WarToggle");
    }

    Toggle FindToggleByName(string toggleName)
    {
        if (string.IsNullOrEmpty(toggleName)) return null;
        var all = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == toggleName)
                return all[i];
        }

        return null;
    }

    void ApplyMode(int mode, bool savePrefs)
    {
        bool mapMode = mode == ModeMap;
        bool listMode = mode == ModeList;
        bool warMode = mode == ModeWar;
        if (mapViewRoot != null)
            mapViewRoot.SetActive(mapMode);
        if (listViewRoot != null)
            listViewRoot.SetActive(listMode);
        if (warMapViewRoot != null)
            warMapViewRoot.SetActive(warMode);
        if (savePrefs)
            PlayerPrefs.SetInt(PrefsKey, mode);
    }
}
