using System.Collections.Generic;
using UnityEngine;

public class ComponentRecycle<T> where T : MonoBehaviour
{
    private readonly Dictionary<int, T> components = new();

    public T GetComponent(GameObject obj)
    {
        int key = obj.GetInstanceID();
        if (!components.TryGetValue(key, out T component))
        {
            components[key] = component = obj.GetComponent<T>();
        }
        return component;
    }

    public void RegisterComponent(T component)
    {
        int key = component.gameObject.GetInstanceID();
        components.TryAdd(key, component);
    }

    public void Unregister(GameObject obj)
    {
        components.Remove(obj.GetInstanceID());
    }

    public void ResetState()
    {
        components.Clear();
    }
}


public class ComponentRecycleWithString<T>
{
    private readonly Dictionary<string, T> components = new();

    public T GetComponent(string key)
    {
        return components.GetValueOrDefault(key);
    }

    public void RegisterComponent(string key, T component)
    {
        if (!components.TryAdd(key, component))
        {
            Debug.LogWarning($"{key} is already registered");
        }
    }

    public void Unregister(string key)
    {
        components.Remove(key);
    }

    public void ResetState()
    {
        components.Clear();
    }
}


/// <summary>
/// 인터페이스를 캐싱하는 헬퍼 클래스
/// GameObject의 InstanceID를 키로 사용
/// </summary>
public class InterfaceRecycle<T> where T : class
{
    private readonly Dictionary<int, T> interfaces = new();

    /// <summary>
    /// GameObject에서 인터페이스를 가져옴 (캐싱됨)
    /// </summary>
    public T GetInterface(GameObject obj)
    {
        if (obj == null) return null;

        int key = obj.GetInstanceID();
        if (!interfaces.TryGetValue(key, out T cachedInterface))
        {
            cachedInterface = obj.GetComponent<T>();
            if (cachedInterface != null)
            {
                interfaces[key] = cachedInterface;
            }
        }
        return cachedInterface;
    }

    /// <summary>
    /// Component에서 인터페이스를 가져옴 (캐싱됨)
    /// </summary>
    public T GetInterface(Component component)
    {
        if (component == null) return null;
        return GetInterface(component.gameObject);
    }

    /// <summary>
    /// 인터페이스를 직접 등록
    /// </summary>
    public void RegisterInterface(GameObject obj, T interfaceInstance)
    {
        if (obj == null || interfaceInstance == null) return;

        int key = obj.GetInstanceID();
        interfaces.TryAdd(key, interfaceInstance);
    }

    /// <summary>
    /// MonoBehaviour에서 인터페이스를 직접 등록
    /// </summary>
    public void RegisterInterface(MonoBehaviour mono, T interfaceInstance)
    {
        if (mono == null || interfaceInstance == null) return;
        RegisterInterface(mono.gameObject, interfaceInstance);
    }

    /// <summary>
    /// 등록 해제
    /// </summary>
    public void Unregister(GameObject obj)
    {
        if (obj == null) return;
        interfaces.Remove(obj.GetInstanceID());
    }

    public void Unregister(Component component)
    {
        if (component == null) return;
        interfaces.Remove(component.gameObject.GetInstanceID());
    }

    /// <summary>
    /// 특정 GameObject에 캐시된 인터페이스가 있는지 확인
    /// </summary>
    public bool HasInterface(GameObject obj)
    {
        if (obj == null) return false;
        return interfaces.ContainsKey(obj.GetInstanceID());
    }

    /// <summary>
    /// 인터페이스를 가져오고, 없으면 false 반환 (TryGet 패턴)
    /// </summary>
    public bool TryGetInterface(GameObject obj, out T interfaceInstance)
    {
        interfaceInstance = null;
        if (obj == null) return false;

        int key = obj.GetInstanceID();
        if (interfaces.TryGetValue(key, out interfaceInstance))
        {
            return interfaceInstance != null;
        }

        interfaceInstance = obj.GetComponent<T>();
        if (interfaceInstance != null)
        {
            interfaces[key] = interfaceInstance;
            return true;
        }

        return false;
    }

    public void ResetState()
    {
        interfaces.Clear();
    }

    public int Count => interfaces.Count;
}


/// <summary>
/// 문자열 키로 인터페이스를 캐싱하는 헬퍼 클래스
/// </summary>
public class InterfaceRecycleWithString<T> where T : class
{
    private readonly Dictionary<string, T> interfaces = new();

    public T GetInterface(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return interfaces.GetValueOrDefault(key);
    }

    public void RegisterInterface(string key, T interfaceInstance)
    {
        if (string.IsNullOrEmpty(key) || interfaceInstance == null) return;

        if (!interfaces.TryAdd(key, interfaceInstance))
        {
            Debug.LogWarning($"[InterfaceRecycle] '{key}' is already registered");
        }
    }

    public bool TryGetInterface(string key, out T interfaceInstance)
    {
        interfaceInstance = null;
        if (string.IsNullOrEmpty(key)) return false;
        return interfaces.TryGetValue(key, out interfaceInstance);
    }

    public void Unregister(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        interfaces.Remove(key);
    }

    public bool HasInterface(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return interfaces.ContainsKey(key);
    }

    public void ResetState()
    {
        interfaces.Clear();
    }

    public int Count => interfaces.Count;
}