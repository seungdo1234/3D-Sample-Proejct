using UnityEngine;

/// <summary> 피격 정보. 데미지 + 충돌 지점/방향(넉백·사망 연출용). </summary>
public struct DamageInfo
{
    public float amount;     // 데미지량
    public Vector3 hitPoint; // 명중 월드 좌표
    public Vector3 hitDir;   // 총알 진행 방향 (넉백/날아갈 방향)
    public Vector3 hitNormal;// 표면 법선
}

/// <summary> 데미지를 받을 수 있는 대상(적/파괴물 등 공통). </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
