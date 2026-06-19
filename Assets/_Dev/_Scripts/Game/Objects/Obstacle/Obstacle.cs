using UnityEngine;

/// <summary>
/// 맵에 배치되는 장애물. 풀링 대상.
/// 풋프린트 반지름(Radius)을 콜라이더(없으면 렌더러) bounds 에서 자동 계산해
/// 맵 배치 시 슬롯 밖으로 안 나가도록 쓰인다 (별도 입력 불필요).
/// </summary>
public class Obstacle : PoolObject
{
    public float Radius { get; private set; }

    private void Awake()
    {
        Radius = CalcFootprintRadius();
    }

    // XZ 평면 풋프린트 반지름 (회전 고려 없이 AABB extents 의 최대값)
    private float CalcFootprintRadius()
    {
        var col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            var e = col.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var e = rend.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        DebugUtil.DevelopmentLog($"[Obstacle] '{name}' 콜라이더/렌더러 없음 — 반지름 0.5 폴백", DebugUtil.LogType.Warning);
        return 0.5f;
    }
}
