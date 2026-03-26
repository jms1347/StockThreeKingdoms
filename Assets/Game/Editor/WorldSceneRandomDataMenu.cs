#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 모드에서 월드(천하) 씬 UI를 볼 때 <see cref="DataManager"/> 성·소식 데이터를 무작위로 채웁니다.
/// 메뉴: StockThreeKingdoms/천하/랜덤 상태로 미리보기 (Play 모드)
/// </summary>
public static class WorldSceneRandomDataMenu
{
    const string MenuPath = "StockThreeKingdoms/천하/랜덤 상태로 미리보기 (Play 모드)";

    [MenuItem(MenuPath, false, 50)]
    public static void ApplyRandomWorldPreview()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("천하 미리보기", "플레이 모드에서만 사용할 수 있습니다.\n월드 씬을 재생한 뒤 다시 실행하세요.", "확인");
            return;
        }

        var dm = DataManager.InstanceOrNull;
        if (dm == null)
        {
            EditorUtility.DisplayDialog("천하 미리보기", "씬에 DataManager 싱글톤이 없습니다.\n스플래시 경로로 DataManager가 생성된 뒤 월드 씬으로 오거나, 씬에 DataManager를 배치하세요.", "확인");
            return;
        }

        if (!dm.IsReady)
            dm.InitializeAllData();

        if (!dm.IsReady)
        {
            EditorUtility.DisplayDialog("천하 미리보기", "데이터 초기화에 실패했습니다.\nDataManager의 마스터 SO(Level/Castle/General/Buff) 할당을 확인하세요.", "확인");
            return;
        }

        dm.ApplyRandomWorldStatePreview();
        Debug.Log("[WorldSceneRandomDataMenu] ApplyRandomWorldStatePreview 완료.");
    }

    [MenuItem(MenuPath, true)]
    public static bool ApplyRandomWorldPreviewValidate()
    {
        return Application.isPlaying;
    }
}
#endif
