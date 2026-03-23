#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ThreeKingdoms.WorldMap;

namespace ThreeKingdoms.WorldMap.EditorTools
{
    /// <summary>
    /// 씬에 WorldMapManager를 두고 프리팹·DB를 연결합니다. 실제 50개 노드·선·캔버스는 플레이 시 Awake에서 자동 생성됩니다.
    /// </summary>
    public static class WorldMapSceneBootstrapEditor
    {
        const string CityPrefabResources = "Assets/Game/WorldMap/Resources/WorldMap/CityNode.prefab";
        const string CityPrefabPrefabs = "Assets/Prefabs/CityNode.prefab";
        const string DatabasePath = "Assets/Game/WorldMap/Data/CityDatabase.asset";

        [MenuItem("StockThreeKingdoms/천하/씬에 천하 맵 자동 배치", false, 2)]
        public static void PlaceWorldMapInScene()
        {
            var existing = Object.FindObjectOfType<WorldMapManager>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "천하",
                        "씬에 이미 WorldMapManager가 있습니다. 그래도 새로 만듭니까?",
                        "추가",
                        "취소"))
                    return;
            }

            var go = new GameObject("WorldMap");
            Undo.RegisterCreatedObjectUndo(go, "천하 맵 자동 배치");
            var wm = go.AddComponent<WorldMapManager>();

            var cityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityPrefabResources)
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(CityPrefabPrefabs);
            var db = AssetDatabase.LoadAssetAtPath<CityDatabase>(DatabasePath);

            var so = new SerializedObject(wm);
            if (cityPrefab != null)
                so.FindProperty("cityNodePrefab").objectReferenceValue = cityPrefab;
            if (db != null)
                so.FindProperty("database").objectReferenceValue = db;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(wm);
            Selection.activeGameObject = go;

            string msg = "WorldMap 오브젝트에 WorldMapManager를 붙였습니다.\n\n";
            if (cityPrefab == null)
            {
                msg += "⚠ CityNode 프리팹을 찾지 못했습니다.\n";
                msg += "먼저 [StockThreeKingdoms → 천하 → 성 노드·오버레이 프리팹 생성]을 실행하세요.\n\n";
            }
            else
            {
                msg += "▶ 플레이(Play)를 누르면 캔버스·스크롤·50개 성 노드·연결선·디테일 오버레이가 자동으로 만들어지고 배치됩니다.\n";
                msg += "(에디터 씬 뷰에는 플레이 전까지 UI가 보이지 않는 것이 정상입니다.)\n\n";
            }

            if (db == null)
                msg += "CityDatabase 에셋이 없어 플레이 시 코드 기본 50성 데이터를 사용합니다.\n(선택) [City Database 에셋 생성] 후 인스펙터에 넣을 수 있습니다.";

            EditorUtility.DisplayDialog("천하 맵 자동 배치", msg, "OK");
        }
    }
}
#endif
