using UnityEngine;

/// <summary>
/// 적 스탯 보관·계산. 체력/이동속도 등 수치를 소유한다. 각 기능 컴포넌트는 여기서 최종값만 가져다 쓴다.
/// 순수 C#, Enemy가 생성·초기화한다. (PlayerStatusHandler 대응)
/// </summary>
public class EnemyStatusHandler
{
    public float MaxHealth { get; private set; }
    public float MoveSpeed { get; private set; }
    public float Health { get; private set; }

    public bool IsDead => Health <= 0f;

    public void Initialize(float maxHealth, float moveSpeed)
    {
        MaxHealth = maxHealth;
        MoveSpeed = moveSpeed;
    }

    /// <summary> 풀 재사용 시 체력 원복 </summary>
    public void ResetHealth() => Health = MaxHealth;

    public void TakeDamage(float amount) => Health = Mathf.Max(0f, Health - amount);
}
