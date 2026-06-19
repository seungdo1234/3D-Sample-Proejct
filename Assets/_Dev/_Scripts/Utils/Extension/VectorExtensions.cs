using UnityEngine;

public static class Vector3Extensions
{
    /// <summary>
    /// 기존 x,z는 유지하고 y만 교체합니다.
    /// </summary>
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    
    public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);
    
    public static Vector3 WithX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);
}