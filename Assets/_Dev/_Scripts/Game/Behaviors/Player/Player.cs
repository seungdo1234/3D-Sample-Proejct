using UnityEngine;

/// <summary>
/// 플레이어 루트 매니저. 하위 순수 C# 컴포넌트를 생성·초기화하고,
/// 순수 클래스라 Unity 가 안 불러주는 Update/FixedUpdate 틱을 대신 구동한다.
/// Initialize() 는 게임 시작 시 MainGame(→스폰 시스템)에서 호출한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    private Rigidbody rb;
    public Rigidbody Rigidbody => rb;

    [SerializeField] private Weapon weapon; // 무기 
    public Weapon Weapon => weapon;

    public PlayerStatusHandler Status { get; private set; }
    public PlayerInputController Input { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PlayerHand PlayerHand { get; private set; }
    public PlayerVisual Visual { get; private set; }

    private bool _initialized;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        rb = GetComponent<Rigidbody>();

        Status = new PlayerStatusHandler();
        Input = new PlayerInputController();
        Movement = new PlayerMovement();
        Visual = new PlayerVisual();
        PlayerHand = new PlayerHand();

        Status.Initialize(this); // 스탯 먼저 (다른 컴포넌트가 의존)
        Input.Initialize(this);
        Movement.Initialize(this); // OnMoveInput 구독
        Visual.Initialize(this); // Animator 탐색 (Hand가 참조하므로 먼저)
        PlayerHand.Initialize(this); // OnLookInput/OnFirePressed/Released 구독 (조준·연사)

        EventManager.Instance.Subscribe<OnEnemyKilledEvent>(OnEnemyKilled); // 킬 누적
    }

    // 적 처치 → 플레이어 스탯에 누적하고 HUD 갱신 이벤트 발행 (킬 카운트 단일 소스)
    private void OnEnemyKilled(OnEnemyKilledEvent _)
    {
        Status.AddKill();
        EventManager.Instance.Publish(new OnKillCountChangedEvent { Count = Status.KillCount });
    }

    // 적 몸에 한 번이라도 닿으면 즉사. 살아있는 적만 유효(시체/래그돌은 무시).
    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized || Status.IsDead) return;

        var enemy = collision.collider.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.IsDead) return;

        Die();
    }

    // 사망 처리: 조작/물리 정지 후 게임오버 이벤트 발행 (팝업·소환 중지는 구독자가 처리)
    private void Die()
    {
        Status.Die();

        // 입력·이동 즉시 차단 + 잔여 속도 제거 (Update/FixedTick 는 살아있어도 멈춘 상태 유지)
        Input.Dispose();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Visual.PlayDeath();

        EventManager.Instance.Publish(new OnPlayerDeadEvent { KillCount = Status.KillCount });
    }

    private void Update()
    {
        if (!_initialized) return;
        Input.Tick(); // 입력 감지 (이동·발사)
        PlayerHand.Tick(); // 에임 라인 등 (프레임 단위)
    }

    private void LateUpdate()
    {
        if (!_initialized) return;
        Input.LookTick(); // 조준 투영 (카메라 추적 확정 후라야 정지 커서가 안 떨림)
    }

    private void FixedUpdate()
    {
        if (!_initialized) return;
        Movement.FixedTick(); // 물리 이동
        PlayerHand.FixedTick(); // 물리 회전 (총구 조준)
    }

    private void OnDestroy()
    {
        if (EventManager.HasInstance) // 종료/씬 언로드 중 유령 싱글톤 생성 방지
            EventManager.Instance.Unsubscribe<OnEnemyKilledEvent>(OnEnemyKilled);
        Movement?.Dispose();
        PlayerHand?.Dispose();
        Input?.Dispose();
    }
}
