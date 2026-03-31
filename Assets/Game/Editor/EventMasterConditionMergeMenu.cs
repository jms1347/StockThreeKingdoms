#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>이벤트별 Condition ID 목록을 SO에 합칩니다. TSV 한 줄: <c>eventId[TAB]C_PUB_01,C_SOL_01</c> (콤마 구분).</summary>
public static class EventMasterConditionMergeMenu
{
    const string MenuPath = "주삼/이벤트/EventMasterDataSo에 조건 TSV 병합…";

    [MenuItem(MenuPath)]
    static void MergeFromTsvFile()
    {
        string path = EditorUtility.OpenFilePanel("조건 TSV (열1: eventId, 열2: Condition)", Application.dataPath, "tsv;txt;csv");
        if (string.IsNullOrEmpty(path)) return;

        var so = LoadEventMasterSo();
        if (so == null)
        {
            EditorUtility.DisplayDialog("EventMasterDataSo", "프로젝트에서 EventMasterDataSo 에셋을 찾지 못했습니다.", "확인");
            return;
        }

        string text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            EditorUtility.DisplayDialog("조건 TSV", "파일이 비어 있습니다.", "확인");
            return;
        }

        string[] rows = text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        int applied = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            string line = rows[r].Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] cells = line.Split('\t');
            if (cells.Length < 2) continue;
            string id = cells[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;

            bool any = false;
            if (so.list != null)
            {
                for (int i = 0; i < so.list.Count; i++)
                {
                    var ev = so.list[i];
                    if (ev == null || string.IsNullOrWhiteSpace(ev.id)) continue;
                    if (!string.Equals(ev.id.Trim(), id, System.StringComparison.Ordinal)) continue;
                    any = true;
                    string condRaw = cells[1].Trim();
                    ev.conditionIds = GoogleSheetManager.ParseConditionIdsCell(condRaw);
                    applied++;
                }
            }

            if (!any)
                Debug.LogWarning($"[EventMasterConditionMerge] SO에 없는 eventId — 건너뜀: {id}");
        }

        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("조건 TSV 병합", $"{applied}행 반영했습니다.\n런타임 맵은 시트 다운로드 또는 DataManager 초기화 시 갱신됩니다.", "확인");
    }

    static EventMasterDataSo LoadEventMasterSo()
    {
        string[] guids = AssetDatabase.FindAssets("t:EventMasterDataSo");
        if (guids == null || guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<EventMasterDataSo>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
