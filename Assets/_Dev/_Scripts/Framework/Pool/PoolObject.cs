using UnityEngine;

/// <summary>
/// 풀링 대상 베이스. 매니저가 PoolKey/InPool 을 세팅 →
/// Return 은 GetComponent 없이 키를 바로 읽고, 재사용은 obj as T 로 끝낸다.
/// </summary>
public abstract class PoolObject : MonoBehaviour
{
    public string PoolKey { get; internal set; }
    public bool InPool { get; internal set; }
}
