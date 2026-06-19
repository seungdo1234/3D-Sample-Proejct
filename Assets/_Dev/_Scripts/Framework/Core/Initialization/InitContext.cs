using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// 부팅 태스크들이 공유하는 데이터 가방.
/// SdkInit 단계에서 Set 한 결과를 ManagerInit 단계에서 Get 해 사용할 수 있다.
/// </summary>
public class InitContext
{
    public CancellationToken CancellationToken { get; }

    // 타입 기준 1:1 저장소. 같은 타입을 여러 개 공유해야 하면 래퍼 타입을 만들어 넣는다.
    private readonly Dictionary<Type, object> _bag = new();

    public InitContext(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    public void Set<T>(T value) => _bag[typeof(T)] = value;

    public bool TryGet<T>(out T value)
    {
        if (_bag.TryGetValue(typeof(T), out object boxed) && boxed is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public T Get<T>() => TryGet(out T value) ? value : default;
}
