using System;
using System.Collections.Generic;

/// <summary>
/// 구조체(struct) 기반 이벤트 버스. 시스템 간 직접 참조 없이 통신하기 위한 허브.
/// </summary>
public class EventManager : Singleton<EventManager>
{
    private readonly Dictionary<Type, Delegate> _events = new();

    // ── 일반 이벤트 (발행/구독) ──────────────────────────────────────────────
    public void Subscribe<T>(Action<T> listener) where T : struct
    {
        var type = typeof(T);
        _events.TryGetValue(type, out var existing);
        _events[type] = (Action<T>)existing + listener;
    }

    public void Unsubscribe<T>(Action<T> listener) where T : struct
    {
        var type = typeof(T);
        if (!_events.TryGetValue(type, out var existing)) return;

        var updated = (Action<T>)existing - listener;
        if (updated == null) _events.Remove(type);
        else                 _events[type] = updated;
    }

    public void Publish<T>(T message) where T : struct
    {
        if (_events.TryGetValue(typeof(T), out var del))
            (del as Action<T>)?.Invoke(message);
    }

    // ── 요청-응답 이벤트 (Func) ──────────────────────────────────────────────
    public void SubscribeRequest<T, TResult>(Func<T, TResult> listener) where T : struct
    {
        var type = typeof(T);
        _events.TryGetValue(type, out var existing);
        _events[type] = (Func<T, TResult>)existing + listener;
    }

    public void UnsubscribeRequest<T, TResult>(Func<T, TResult> listener) where T : struct
    {
        var type = typeof(T);
        if (!_events.TryGetValue(type, out var existing)) return;

        var updated = (Func<T, TResult>)existing - listener;
        if (updated == null) _events.Remove(type);
        else                 _events[type] = updated;
    }

    public TResult PublishRequest<T, TResult>(T request) where T : struct
    {
        if (_events.TryGetValue(typeof(T), out var del) && del is Func<T, TResult> callback)
            return callback.Invoke(request);
        return default;
    }

    public void Clear() => _events.Clear();
}
