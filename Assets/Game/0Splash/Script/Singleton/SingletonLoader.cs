using UnityEngine;
using UnityEngine.SceneManagement;

public class SingletonLoader : MonoBehaviour
{
    [Header("Manager Prefabs")]
    public GameObject googlesheetManagerPrefab;
    public GameObject translationManagerPrefab;
    public GameObject soundManagerPrefab;
    public GameObject fadeManagerPrefab;
    public GameObject popupManagerPrefab;
    public GameObject capitalManagerPrefab;

    [Header("Scene Transition")]
    [Tooltip("매니저 로드 완료 후 이동할 다음 씬의 이름입니다.")]
    public string nextSceneName = "TitleScene";

    private void Awake()
    {
        LoadAllManagers();
        //LoadNextScene();
    }

    private void LoadAllManagers()
    {
        // 위 Singleton<T> 클래스에서 이미 중복 체크와 null 체크를 하므로 안심하고 호출 가능합니다.
        // 각 매니저 스크립트는 Singleton<T>를 상속받았다고 가정합니다.
        CapitalManager.Load(capitalManagerPrefab);

        // 예시: public class GoogleSheetManager : Singleton<GoogleSheetManager> { ... }
        // GoogleSheetManager.Load(googlesheetManagerPrefab);
        // TranslationManager.Load(translationManagerPrefab);
        // SoundManager.Load(soundManagerPrefab);
        // FadeManager.Load(fadeManagerPrefab);
        // PopupManager.Load(popupManagerPrefab);
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[SingletonLoader] 이동할 다음 씬의 이름이 설정되지 않았습니다.");
        }
    }
}