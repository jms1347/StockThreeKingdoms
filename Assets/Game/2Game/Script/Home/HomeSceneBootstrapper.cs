using UnityEngine;

/// <summary>
/// SplashScene을 거치지 않고 HomeScene만 단독 실행해도
/// 필요한 싱글톤(특히 GlobalUIManager)이 로드되도록 하는 부트스트랩.
/// </summary>
public class HomeSceneBootstrapper : MonoBehaviour
{
    [Header("Manager Prefabs (optional)")]
    public GameObject gameManagerPrefab;
    public GameObject globalUiManagerPrefab;
    public GameObject dataManagerPrefab;
    public GameObject googleSheetManagerPrefab;

    void Awake()
    {
        // 이미 로드되어 있으면 Load가 무시됨(Singleton.Load가 중복 방지)
        if (gameManagerPrefab != null) GameManager.Load(gameManagerPrefab);
        if (globalUiManagerPrefab != null) GlobalUIManager.Load(globalUiManagerPrefab);
        if (dataManagerPrefab != null) DataManager.Load(dataManagerPrefab);
        if (googleSheetManagerPrefab != null) GoogleSheetManager.Load(googleSheetManagerPrefab);
    }
}

