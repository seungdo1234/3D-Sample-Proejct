using UnityEngine;

/// <summary>
/// 적 루트 매니저. 하위 순수 C# 컴포넌트(스탯/이동/연출/사망)를 생성·초기화하고,
/// 순수 클래스라 Unity가 안 불러주는 Update/FixedUpdate 틱을 대신 구동한다. (Player 대응 구조)
/// 풀링 대상이며 IDamageable. 스폰 시 Setup→Run 연출, 피격 시 넉백/플래시, 사망 시 Death 연출+래그돌.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Enemy : PoolObject, IDamageable
{
    [Header("# Stats")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool faceMoveDirection = true; // 진행 방향(Y축)으로 회전

    [Header("# Ground Follow")]
    [Tooltip("바닥 추종 레이캐스트 대상 (바닥+장애물 레이어만 선택)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("발 위치 기준 위/아래로 바닥을 탐색하는 범위(m)")]
    [SerializeField] private float groundProbeHeight = 3f;
    [Tooltip("바닥 높이 재샘플 간격(초) — 클수록 가볍고, 너무 크면 지형 추종이 늦음")]
    [SerializeField] private float groundSampleInterval = 0.1f;

    [Header("# Knockback")]
    [SerializeField] private float knockbackForce = 6f;   // 피격 시 뒤로 밀리는 초기 속도
    [SerializeField] private float knockbackDecel = 30f;  // 넉백 감쇠(초당 감소량)
    [SerializeField] private float stunDuration = 0.1f;   // 피격 시 추적 정지 시간

    [Header("# Death")]
    [SerializeField] private string deadLayerName = "EnemyDead"; // 사망 중 전환 레이어
    [SerializeField] private float launchForce = 8f;     // 날아가는 힘
    [SerializeField] private float launchUpBias = 0.4f;  // 살짝 떠오르는 정도(장풍 느낌)
    [SerializeField] private float torqueForce = 12f;    // 회전 토크
    [SerializeField] private float settleSpeed = 0.4f;   // 이 속도 이하면 "땅에 멈춤"으로 판정
    [SerializeField] private float minAirTime = 0.25f;   // 발사 직후 오판 방지(최소 체공)
    [SerializeField] private float despawnDelay = 1f;    // 멈춘 뒤 사라지기까지
    [SerializeField] private float maxDeathTime = 6f;    // 안전장치: 안 멈춰도 이 시간 뒤 강제 반납

    [Header("# Hit Flash")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.12f;

    public Rigidbody Rigidbody { get; private set; }
    public EnemyStatusHandler Status { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyVisual Visual { get; private set; }

    private EnemyDeathHandler _death;
    private bool _initialized;

    public bool IsDead => _death != null && _death.IsDead;

    private void Awake() => Initialize();

    // 컴포넌트 생성·초기화는 1회만 (풀 인스턴스 생성 시점)
    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Rigidbody = GetComponent<Rigidbody>();

        Status = new EnemyStatusHandler();
        Movement = new EnemyMovement();
        Visual = new EnemyVisual();
        _death = new EnemyDeathHandler();

        Status.Initialize(maxHealth, moveSpeed);                                  // 스탯 먼저 (이동이 의존)
        Movement.Initialize(this, faceMoveDirection, knockbackDecel, stunDuration,
            groundMask, groundProbeHeight, groundSampleInterval);
        Visual.Initialize(this, flashColor, flashDuration);
        _death.Initialize(this, deadLayerName, launchForce, launchUpBias, torqueForce,
            settleSpeed, minAirTime, despawnDelay, maxDeathTime);
    }

    /// <summary> 스폰 시 호출 — 타깃 지정 + 상태 전체 리셋(풀 재사용) + Run 연출 </summary>
    public void Setup(Transform target)
    {
        Status.ResetHealth();
        _death.Reset();
        Visual.ResetVisual();
        Movement.SetTarget(target);
        Visual.PlayRun(); // 활성화 시 Run 트리거
    }

    /// <summary> 풀에서 꺼낸 직후/재배치 시 위치를 강제 스냅 (보간 잔상 방지) </summary>
    public void Warp(Vector3 position)
    {
        Rigidbody.position = position;
        transform.position = position;
        Movement.OnSpawn(position.y); // 생존 높이 고정 기준
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return; // 시체 재타격 무시(레이어로도 막지만 안전장치)

        Status.TakeDamage(info.amount);
        Visual.Flash();

        if (Status.IsDead)
        {
            Visual.PlayDeath();      // 죽을 때 Death 트리거
            _death.Die(info.hitDir); // 래그돌 발사 (이후 추적은 IsDead 로 멈춤)
            EventManager.Instance.Publish(new OnEnemyKilledEvent()); // 킬 카운트 누적용
            return;
        }

        Movement.ApplyKnockback(info.hitDir, knockbackForce);
    }

    // 플래시 + 사망 라이프사이클 (player 참조 불필요한 자체 연출/정리)
    private void Update()
    {
        Visual.Tick();
        _death.Tick();
    }

    // 추적 이동 (사망 중엔 순수 물리가 굴리므로 중단)
    private void FixedUpdate()
    {
        if (IsDead) return;
        Movement.FixedTick();
    }

    /// <summary> 플레이어와의 XZ 평면 거리 제곱 (재배치 판정용) </summary>
    public float DistanceSqrTo(Vector3 p)
    {
        Vector3 d = transform.position - p; d.y = 0f;
        return d.sqrMagnitude;
    }
}
