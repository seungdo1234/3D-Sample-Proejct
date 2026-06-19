using UnityEngine;

/// <summary>
/// 플레이어 연출 전담. 애니메이터를 구동해 발사 등 비주얼만 담당한다 (로직 없음).
/// 순수 C#, Player가 생성·초기화한다. Animator는 모델 자식 GO에서 런타임 자동 탐색.
/// </summary>
public class PlayerVisual
{
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    private Animator _animator;

    public void Initialize(Player player)
    {
        _animator = player.GetComponentInChildren<Animator>();

        // 미연결 시 발사 연출이 안 나오므로 명확히 알림
        if (_animator == null)
            Debug.LogWarning("[Visual] 플레이어 하위에 Animator가 없어 발사 연출이 동작하지 않음", player);
    }

    /// <summary>발사 연출. 애니메이터 Shoot 트리거 발동.</summary>
    public void Shoot()
    {
        if (_animator == null) return;
        _animator.SetTrigger(ShootHash);
    }

    /// <summary>사망 연출. 애니메이터 Death 트리거 발동 (해당 상태 없으면 무시됨).</summary>
    public void PlayDeath()
    {
        if (_animator == null) return;
        _animator.SetTrigger(DeathHash);
    }
}
