using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>실데이터 테스트용 — 천하 병사 투입(SO·로컬 JSON·플레이 중 런타임) 초기화.</summary>
public static class UserDeploymentResetMenu
{
    const string MenuPath = "StockThreeKingdoms/천하 테스트/병사 투입 데이터 초기화…";

    [MenuItem(MenuPath, false, 50)]
    static void ClearDeployments()
    {
        string jsonPath = Path.Combine(Application.persistentDataPath, "castle_state.json");
        bool hasJson = File.Exists(jsonPath);

        int nCastleSo = AssetDatabase.FindAssets("t:CastleStateSo").Length;
        int nPortfolioSo = AssetDatabase.FindAssets("t:UserPortfolioSo").Length;

        string msg =
            "CastleStateSo·UserPortfolioSo 에셋과(있으면) 로컬 castle_state.json에서 유저 투입만 제거합니다.\n\n" +
            $"CastleStateSo 에셋: {nCastleSo}개\n" +
            $"UserPortfolioSo 에셋: {nPortfolioSo}개\n" +
            (hasJson ? $"JSON:\n{jsonPath}\n" : "JSON: 없음\n") +
            (EditorApplication.isPlaying ? "\n플레이 중이면 DataManager 런타임 맵도 동기화합니다.\n" : "");

        if (!EditorUtility.DisplayDialog("병사 투입 초기화", msg, "진행", "취소"))
            return;

        int clearedCastles = ClearAllCastleStateSoAssets();
        int clearedPortfolios = ClearAllUserPortfolioSoAssets();
        bool jsonOk = !hasJson || StripDeploymentsInCastleStateJson(jsonPath);

        if (EditorApplication.isPlaying)
        {
            var dm = DataManager.InstanceOrNull;
            if (dm != null && dm.IsStateReady)
                dm.ClearAllUserCastleDeployments();
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[UserDeploymentReset] 완료 — CastleStateSo 행 갱신 {clearedCastles}에셋, UserPortfolioSo {clearedPortfolios}에셋, JSON={(jsonOk ? "처리" : "실패/없음")}");
    }

    static int ClearAllCastleStateSoAssets()
    {
        int touched = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CastleStateSo"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<CastleStateSo>(path);
            if (so == null || so.castles == null) continue;
            bool dirty = false;
            for (int i = 0; i < so.castles.Count; i++)
            {
                var e = so.castles[i];
                if (e == null) continue;
                if (e.userDeployedTroops != 0 || e.averagePurchasePrice != 0f)
                    dirty = true;
                e.userDeployedTroops = 0;
                e.averagePurchasePrice = 0f;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(so);
                touched++;
            }
        }

        return touched;
    }

    static int ClearAllUserPortfolioSoAssets()
    {
        int touched = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:UserPortfolioSo"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<UserPortfolioSo>(path);
            if (so == null) continue;
            if (so.holdings == null || so.holdings.Count == 0) continue;
            so.holdings.Clear();
            EditorUtility.SetDirty(so);
            touched++;
        }

        return touched;
    }

    static bool StripDeploymentsInCastleStateJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            var payload = JsonUtility.FromJson<CastleStateSavePayload>(json);
            if (payload?.castles == null) return false;
            for (int i = 0; i < payload.castles.Count; i++)
            {
                var s = payload.castles[i];
                if (s == null) continue;
                s.userDeployedTroops = 0;
                s.averagePurchasePrice = 0f;
            }

            File.WriteAllText(path, JsonUtility.ToJson(payload, true));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UserDeploymentReset] JSON 수정 실패: {e.Message}");
            return false;
        }
    }
}
