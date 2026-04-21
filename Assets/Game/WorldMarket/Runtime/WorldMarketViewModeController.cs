using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 탭 — 지도 보기 / 리스트 보기 전환.</summary>
[DisallowMultipleComponent]
public class WorldMarketViewModeController : MonoBehaviour
{
    const string PrefsKey = "WorldMarketViewMode";

    [SerializeField] GameObject mapViewRoot;
    [SerializeField] GameObject listViewRoot;
    [SerializeField] Toggle mapToggle;
    [SerializeField] Toggle listToggle;

    void Awake()
    {
        if (mapViewRoot == null || listViewRoot == null)
            AutoResolveRoots();

        if (mapToggle != null)
        {
            mapToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ApplyMode(true, true);
            });
        }

        if (listToggle != null)
        {
            listToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ApplyMode(false, true);
            });
        }

        bool preferMap = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
        ApplyMode(preferMap, false);
        if (mapToggle != null)
            mapToggle.SetIsOnWithoutNotify(preferMap);
        if (listToggle != null)
            listToggle.SetIsOnWithoutNotify(!preferMap);
    }

    void AutoResolveRoots()
    {
        var t = transform;
        for (int i = 0; i < 8 && t != null; i++, t = t.parent)
        {
            var mv = t.Find("MapViewRoot");
            var lv = t.Find("ListViewRoot");
            if (mv != null) mapViewRoot = mv.gameObject;
            if (lv != null) listViewRoot = lv.gameObject;
            if (mapViewRoot != null && listViewRoot != null)
                return;
        }
    }

    void ApplyMode(bool mapMode, bool savePrefs)
    {
        if (mapViewRoot != null)
            mapViewRoot.SetActive(mapMode);
        if (listViewRoot != null)
            listViewRoot.SetActive(!mapMode);
        if (savePrefs)
            PlayerPrefs.SetInt(PrefsKey, mapMode ? 1 : 0);
    }
}
