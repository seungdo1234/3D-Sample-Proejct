using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _quitting;

    /// <summary>인스턴스가 실제로 존재하는지 (접근만으로 생성하지 않음).</summary>
    public static bool HasInstance => _instance != null;

    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;

            // 앱 종료/씬 언로드 중에는 새로 만들지 않는다 (유령 오브젝트 방지)
            if (_quitting) return null;

            _instance = FindFirstObjectByType<T>();
            if (_instance != null) return _instance;

            GameObject singletonObject = new GameObject(typeof(T).Name);
            _instance = singletonObject.AddComponent<T>();
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            _quitting = false;
            OnSingletonInitialized();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnSingletonInitialized() { }

    // 종료 시점 플래그. private 이므로 하위 클래스가 실수로 base 호출을 빠뜨릴 일이 없다.
    private void OnApplicationQuit() => _quitting = true;

    protected virtual void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
