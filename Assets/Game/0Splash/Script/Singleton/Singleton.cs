using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    // 외부에서는 읽기만 가능하도록 프로퍼티 사용 (안전성 확보)
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogWarning($"[Singleton] {typeof(T).Name} 인스턴스가 아직 로드되지 않았거나 파괴되었습니다.");
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 1. 인스턴스가 없을 경우 자신을 할당
        if (_instance == null)
        {
            _instance = this as T;
            // DontDestroyOnLoad는 최상위(Root) 오브젝트에만 적용 가능하므로 안전하게 root를 지정
            DontDestroyOnLoad(transform.root.gameObject);
        }
        // 2. 이미 인스턴스가 존재하는데 또 생성되려고 하면 파괴 (스플래시 씬 재진입 시 중복 방지)
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] {typeof(T).Name}의 중복된 인스턴스가 발견되어 파괴합니다.");
            Destroy(gameObject);
        }
    }

    public static void Load(GameObject singletonPrefab)
    {
        // 이미 로드된 상태라면 중복 생성하지 않고 무시
        if (_instance != null) return;

        if (singletonPrefab == null)
        {
            Debug.LogError($"[Singleton] {typeof(T).Name}를 로드하기 위한 프리팹이 할당되지 않았습니다!");
            return;
        }

        // 프리팹 생성
        GameObject newGameObject = Instantiate(singletonPrefab);

        // 하이어라키 창이 지저분해지지 않도록 "(Clone)" 이름 제거
        newGameObject.name = typeof(T).Name;

        // Instantiate 하는 순간 프리팹에 붙어있는 T의 Awake()가 동기적으로 실행되며 _instance가 세팅됩니다.
        // 프리팹에 실수로 해당 컴포넌트를 안 붙였을 경우를 대비한 검증
        if (newGameObject.GetComponent<T>() == null)
        {
            Debug.LogError($"[Singleton] 프리팹 '{singletonPrefab.name}'에 {typeof(T).Name} 컴포넌트가 누락되었습니다!");
            Destroy(newGameObject);
        }
    }
}