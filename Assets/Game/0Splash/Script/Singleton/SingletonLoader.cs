using UnityEngine;
using UnityEngine.SceneManagement;

public class SingletonLoader : MonoBehaviour
{
    [Header("Manager Prefabs")]
    public GameObject googlesheetManagerPrefab;
    public GameObject dataManagerPrefab;
    public GameObject translationManagerPrefab;
    public GameObject soundManagerPrefab;
    public GameObject fadeManagerPrefab;
    public GameObject popupManagerPrefab;
    public GameObject gameManagerPrefab;
    public GameObject globalUiManagerPrefab;

    [Header("Scene Transition")]
    [Tooltip("매니저 로드 완료 후 이동할 다음 씬의 이름입니다.")]
    public string nextSceneName = "WorldScene";

    private void Awake()
    {
        LoadAllManagers();
        LoadNextScene();
    }

    private void LoadAllManagers()
    {
        // 위 Singleton<T> 클래스에서 이미 중복 체크와 null 체크를 하므로 안심하고 호출 가능합니다.
        // 각 매니저 스크립트는 Singleton<T>를 상속받았다고 가정합니다.
        GameManager.Load(gameManagerPrefab);
        GlobalUIManager.Load(globalUiManagerPrefab);

        // 예시: public class GoogleSheetManager : Singleton<GoogleSheetManager> { ... }
        GoogleSheetManager.Load(googlesheetManagerPrefab);
        DataManager.Load(dataManagerPrefab);
        // TranslationManager.Load(translationManagerPrefab);
        // SoundManager.Load(soundManagerPrefab);
        // FadeManager.Load(fadeManagerPrefab);
        // PopupManager.Load(popupManagerPrefab);
    }

    private void LoadNextScene()
    {
        // 이미 목표 씬에 있는 경우에는 씬을 다시 로드하지 않는다.
        // (예: 동일 씬 내 SingletonLoader가 있는 경우 무한 로드 방지)
        var currentScene = SceneManager.GetActiveScene();
        string target = ResolveNextSceneName();
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[SingletonLoader] 빌드 프로필에서 로드 가능한 씬을 찾지 못해 씬 전환을 건너뜁니다.");
            return;
        }

        if (currentScene.name == target)
        {
            return;
        }

        SceneManager.LoadScene(target);
    }

    string ResolveNextSceneName()
    {
        // 1) Inspector에서 지정한 씬 우선
        if (IsSceneInBuildByName(nextSceneName))
            return nextSceneName;

        // 2) 월드 탭 기본 씬
        if (IsSceneInBuildByName("WorldScene"))
            return "WorldScene";

        // 3) 홈 씬 fallback
        if (IsSceneInBuildByName("HomeScene"))
            return "HomeScene";

        // 4) 마지막 fallback: 빌드 설정 첫 씬
        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(0);
            if (!string.IsNullOrEmpty(path))
                return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        return string.Empty;
    }

    static bool IsSceneInBuildByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }
}