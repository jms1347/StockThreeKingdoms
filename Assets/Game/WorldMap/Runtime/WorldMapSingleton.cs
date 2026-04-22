using UnityEngine;

/// <summary>월드맵 씬 전용 싱글톤. DontDestroyOnLoad 없이 씬 생명주기에 묶입니다.</summary>
public abstract class WorldMapSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogWarning($"[WorldMapSingleton] {typeof(T).Name} 인스턴스가 없습니다.");
            return _instance;
        }
    }

    public static T InstanceOrNull => _instance;

    protected virtual void Awake()
    {
        if (_instance == null)
            _instance = this as T;
        else if (_instance != this)
        {
            Debug.LogWarning($"[WorldMapSingleton] {typeof(T).Name} 중복 제거.");
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
