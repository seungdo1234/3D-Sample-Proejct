/// <summary>
/// 플레이어 스탯 보관·최종값 계산. 각 기능 컴포넌트는 여기서 최종 스탯만 가져다 쓴다.
/// </summary>
public class PlayerStatusHandler
{
    private float _baseMoveSpeed = 5f;
    private const float AttackMoveMult = 0.5f; // 공격 중 이동속도 배율

    // 보정치 (장비/버프 등)
    public float MoveSpeedFlat;
    public float MoveSpeedMult = 1f;

    // 공격 중 여부 (Hand가 발사 린저에 따라 설정 → 이동 감속에 반영)
    public bool IsAttacking;

    // 처치한 적 수 (게임오버 팝업에서 읽음). Player 가 킬 이벤트 받을 때 누적.
    public int KillCount { get; private set; }
    public void AddKill() => KillCount++;

    // 사망 여부 (적과 1번 닿으면 즉사). 중복 처리 방지 가드로도 사용.
    public bool IsDead { get; private set; }
    public void Die() => IsDead = true;

    public void Initialize(Player player)
    {
        // TODO: 테이블/세이브에서 base 스탯 로드
    }

    public float MoveSpeed =>
        (_baseMoveSpeed + MoveSpeedFlat) * MoveSpeedMult * (IsAttacking ? AttackMoveMult : 1f);
}
