#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Home / World / News 씬의 Canvas를 프리팹으로 뽑고, GlobalUIManager 탭과 묶인 GameScene을 만듭니다.
/// 메뉴: StockTK / Build GameScene (Hub Tabs)
/// </summary>
public static class GameSceneHubBuilder
{
    const string HubFolder = "Assets/Game/0Scene/GameHub";
    const string HomeCanvasPrefab = HubFolder + "/GameHub_HomeCanvas.prefab";
    const string WorldCanvasPrefab = HubFolder + "/GameHub_WorldCanvas.prefab";
    const string NewsCanvasPrefab = HubFolder + "/GameHub_NewsCanvas.prefab";
    const string GameScenePath = "Assets/Game/0Scene/GameScene.unity";
    /// <summary>씬이 <c>0Scene</c> 루트 또는 <c>TestScene</c> 폴더에 있을 수 있어 둘 다 시도합니다.</summary>
    static readonly string[] HomeSceneCandidates =
    {
        "Assets/Game/0Scene/TestScene/HomeScene.unity",
        "Assets/Game/0Scene/HomeScene.unity",
    };
    static readonly string[] WorldSceneCandidates =
    {
        "Assets/Game/0Scene/TestScene/WorldScene.unity",
        "Assets/Game/0Scene/WorldScene.unity",
    };
    static readonly string[] NewsSceneCandidates =
    {
        "Assets/Game/0Scene/TestScene/NewsScene.unity",
        "Assets/Game/0Scene/NewsScene.unity",
    };

    /// <summary>CI/배치: Unity.exe -batchmode -quit -projectPath ... -executeMethod GameSceneHubBuilder.BuildFromBatch</summary>
    public static void BuildFromBatch()
    {
        try
        {
            BuildGameScene();
        }
        finally
        {
            EditorApplication.Exit(0);
        }
    }

    [MenuItem("StockTK/Build GameScene (Hub Tabs)")]
    public static void BuildGameScene()
    {
        if (!AssetDatabase.IsValidFolder(HubFolder))
        {
            var parent = Path.GetDirectoryName(HubFolder)?.Replace("\\", "/");
            var leaf = Path.GetFileName(HubFolder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        var homePath = ResolveExistingScenePath(HomeSceneCandidates, "HomeScene");
        var worldPath = ResolveExistingScenePath(WorldSceneCandidates, "WorldScene");
        var newsPath = ResolveExistingScenePath(NewsSceneCandidates, "NewsScene");
        if (homePath == null || worldPath == null || newsPath == null)
        {
            Debug.LogError("[GameSceneHubBuilder] Home/World/News 씬 중 하나를 찾을 수 없어 중단합니다.");
            return;
        }

        ExportCanvasPrefab(homePath, "Canvas", HomeCanvasPrefab);
        ExportCanvasPrefab(worldPath, "Canvas", WorldCanvasPrefab);
        ExportCanvasPrefab(newsPath, "Canvas", NewsCanvasPrefab);

        GameObject gameManagerPrefab = null;
        GameObject globalUiManagerPrefab = null;
        GameObject dataManagerPrefab = null;
        GameObject googleSheetManagerPrefab = null;
        GameObject newsManagerPrefab = null;
        GameObject eventManagerPrefab = null;

        var homeScene = EditorSceneManager.OpenScene(homePath, OpenSceneMode.Single);
        var boot = Object.FindFirstObjectByType<HomeSceneBootstrapper>();
        if (boot != null)
        {
            gameManagerPrefab = boot.gameManagerPrefab;
            globalUiManagerPrefab = boot.globalUiManagerPrefab;
            dataManagerPrefab = boot.dataManagerPrefab;
            googleSheetManagerPrefab = boot.googleSheetManagerPrefab;
            newsManagerPrefab = boot.newsManagerPrefab;
            eventManagerPrefab = boot.eventManagerPrefab;
        }
        else
            Debug.LogWarning("[GameSceneHubBuilder] HomeScene에 HomeSceneBootstrapper가 없습니다. GameScene 부트스트랩은 수동 연결하세요.");

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var hub = new GameObject("GameHub");
        var bootstrap = hub.AddComponent<HomeSceneBootstrapper>();
        bootstrap.gameManagerPrefab = gameManagerPrefab;
        bootstrap.globalUiManagerPrefab = globalUiManagerPrefab;
        bootstrap.dataManagerPrefab = dataManagerPrefab;
        bootstrap.googleSheetManagerPrefab = googleSheetManagerPrefab;
        bootstrap.newsManagerPrefab = newsManagerPrefab;
        bootstrap.eventManagerPrefab = eventManagerPrefab;

        var tabContent = new GameObject("TabContent");
        tabContent.transform.SetParent(hub.transform, false);

        var homePfb = AssetDatabase.LoadAssetAtPath<GameObject>(HomeCanvasPrefab);
        var worldPfb = AssetDatabase.LoadAssetAtPath<GameObject>(WorldCanvasPrefab);
        var newsPfb = AssetDatabase.LoadAssetAtPath<GameObject>(NewsCanvasPrefab);

        var homeInst = InstantiateUnder(homePfb, tabContent.transform, "HomeTerritoryCanvas");
        var worldInst = InstantiateUnder(worldPfb, tabContent.transform, "WorldMarketCanvas");
        var newsInst = InstantiateUnder(newsPfb, tabContent.transform, "NewsCanvas");
        if (homeInst == null || worldInst == null || newsInst == null)
        {
            Debug.LogError("[GameSceneHubBuilder] Canvas 프리팹 생성에 실패했습니다. Export 로그를 확인하세요.");
            return;
        }

        var tabs = hub.AddComponent<GameHubTabController>();
        var so = new SerializedObject(tabs);
        so.FindProperty("homeTerritoryPanel").objectReferenceValue = homeInst;
        so.FindProperty("worldMarketPanel").objectReferenceValue = worldInst;
        so.FindProperty("newsPanel").objectReferenceValue = newsInst;
        so.FindProperty("initialTabId").stringValue = "Home";
        so.ApplyModifiedPropertiesWithoutUndo();

        if (worldInst != null) worldInst.SetActive(false);
        if (newsInst != null) newsInst.SetActive(false);

        EditorSceneManager.MarkSceneDirty(newScene);
        EditorSceneManager.SaveScene(newScene, GameScenePath);

        AddToBuildSettingsIfMissing(GameScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[GameSceneHubBuilder] 완료: {GameScenePath} (메뉴에서 씬을 열어 플레이하세요.)");
    }

    static string ResolveExistingScenePath(string[] candidates, string labelForError)
    {
        foreach (var p in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", p.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(full))
                return p;
        }

        Debug.LogError($"[GameSceneHubBuilder] {labelForError} 씬을 찾을 수 없습니다. 다음 경로를 확인하세요: {string.Join(", ", candidates)}");
        return null;
    }

    static void ExportCanvasPrefab(string scenePath, string canvasName, string prefabPath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return;
        var sceneFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scenePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(sceneFull))
        {
            Debug.LogError($"[GameSceneHubBuilder] 씬 파일이 없습니다: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var canvasGo = GameObject.Find(canvasName);
        if (canvasGo == null)
        {
            Debug.LogError($"[GameSceneHubBuilder] {scenePath} 에 '{canvasName}' 을 찾을 수 없습니다.");
            return;
        }

        var temp = Object.Instantiate(canvasGo);
        temp.name = canvasGo.name;
        PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        Object.DestroyImmediate(temp);
        Debug.Log($"[GameSceneHubBuilder] 프리팹 저장: {prefabPath}");
    }

    static GameObject InstantiateUnder(GameObject prefab, Transform parent, string instanceName)
    {
        if (prefab == null)
        {
            Debug.LogError("[GameSceneHubBuilder] 프리팹이 없습니다.");
            return null;
        }

        var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (go == null) return null;
        go.name = instanceName;
        go.transform.SetParent(parent, false);
        return go;
    }

    static void AddToBuildSettingsIfMissing(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == scenePath)
                return;
        }

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[GameSceneHubBuilder] Build Settings에 추가: {scenePath}");
    }
}
#endif
