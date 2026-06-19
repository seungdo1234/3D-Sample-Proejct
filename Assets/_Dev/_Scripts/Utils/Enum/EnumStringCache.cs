using System;
using System.Collections.Generic;
using UnityEngine;

// 정적 클래스로 선언하여 어디서든 접근 가능하게 합니다.
public static class EnumStringCache<T> where T : struct, Enum
{
    private static readonly Dictionary<T, string> cache = new Dictionary<T, string>();

    // 정적 생성자
    static EnumStringCache()
    {
        // Enum의 모든 값을 가져옵니다.
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (!cache.ContainsKey(value))
            {
                cache.Add(value, value.ToString());
            }
        }
        
        Debug.Log($"[EnumStringCache] Cached {typeof(T).Name} ({cache.Count} items)");
    }

    // 외부에서 호출할 메서드입니다.
    public static string ToString(T value)
    {
        // 딕셔너리에 값이 있으면 반환, 없으면(혹시 모를 예외) ToString 후 반환
        if (cache.TryGetValue(value, out string result))
        {
            return result;
        }

        // 이론상 여기 도달하면 안 되지만, 안전 장치입니다.
        return value.ToString();
    }
}