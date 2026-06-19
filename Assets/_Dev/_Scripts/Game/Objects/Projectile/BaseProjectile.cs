using UnityEngine;

/// <summary>
/// 모든 투사체의 베이스. 풀링 대상이며, 궤적(선형/포물선)은 파생 클래스가 Integrate 로 구현한다.
/// 물리(Rigidbody/Collider) 없이 매 프레임 직접 이동 + 이전→현재 위치 레이캐스트로 충돌을 잡는다(터널링 방지).
/// 공통 스펙(속도/수명/충돌 레이어)과 풀 반납을 여기서 관리.
/// </summary>
public abstract class BaseProjectile : PoolObject
{
    [Header("Spec")]
    [SerializeField] protected float speed = 40f;
    [SerializeField] protected float lifeTime = 2f;
    [SerializeField] protected float damage = 10f; // 1발 데미지
    [Tooltip("총알을 막는 레이어(벽/적 등). Player 는 제외할 것")]
    [SerializeField] protected LayerMask hitMask = ~0;

    protected Vector3 _position; // 현재 위치 캐시 (transform 읽기 비용 회피)
    private float _age;
    private TrailRenderer _trail; // 트레이서 꼬리 (있으면 재사용 시 비워줌)

    protected virtual void Awake() => _trail = GetComponentInChildren<TrailRenderer>();

    /// <summary>발사 진입점: 위치·방향·타이머를 리셋하고 비행 시작.</summary>
    public void Fire(Vector3 origin, Vector3 direction)
    {
        direction = direction.normalized;
        _position = origin;
        _age = 0f;

        // 진행 방향으로 정렬 → 꼬리가 곧게 뻗는다
        Quaternion rot = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction) : transform.rotation;
        transform.SetPositionAndRotation(origin, rot);

        // 풀 재사용 함정: 안 비우면 이전 소멸 위치에서 여기까지 선이 그어진다
        if (_trail != null) _trail.Clear();

        OnFired(direction);
    }

    /// <summary>
    /// 비행 없이 즉시 명중 처리(초근접 직격). 총구가 몸통보다 앞이라 끌어안은 적/벽을
    /// 총알이 뚫어버리는 거리에서, 총알을 날리는 대신 명중점에 바로 데미지·임팩트를 주고 반납.
    /// 데미지 값과 적용 규칙은 일반 발사와 동일(여기 한 곳이 단일 출처).
    /// </summary>
    public void HitInstant(RaycastHit hit, Vector3 direction)
    {
        _position = hit.point;
        transform.position = hit.point;
        if (_trail != null) _trail.Clear(); // 풀 재사용 시 이전 궤적이 이어지지 않게

        Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : transform.forward;
        ApplyDamage(hit, dir);
        OnHit(hit);
        Despawn();
    }

    /// <summary>파생이 초기 속도/방향을 설정.</summary>
    protected abstract void OnFired(Vector3 direction);

    /// <summary>dt 동안 이동한 다음 위치를 계산(파생이 궤적 구현).</summary>
    protected abstract Vector3 Integrate(float dt);

    private void Update()
    {
        float dt = Time.deltaTime;

        _age += dt;
        if (_age >= lifeTime) { Despawn(); return; }

        Vector3 next = Integrate(dt);
        Vector3 delta = next - _position;
        float dist = delta.magnitude;

        // 이전→다음 위치 사이를 레이로 검사 (빠른 총알도 벽 안 뚫음)
        if (dist > 0f &&
            Physics.Raycast(_position, delta / dist, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = _position = hit.point;
            ApplyDamage(hit, delta / dist); // 데미지 + 넉백/사망 정보 전달
            OnHit(hit); // 임팩트 이펙트 훅 (파생이 구현)
            Despawn();
            return;
        }

        transform.position = _position = next;
    }

    // 명중 대상이 IDamageable 이면 데미지 적용. 적은 Rigidbody 보유라 hit.rigidbody 로 루트를 바로 얻는다.
    private void ApplyDamage(RaycastHit hit, Vector3 dir)
    {
        var go = hit.rigidbody != null ? hit.rigidbody.gameObject : hit.collider.gameObject;
        if (!go.TryGetComponent(out IDamageable target)) return;

        target.TakeDamage(new DamageInfo
        {
            amount = damage,
            hitPoint = hit.point,
            hitDir = dir,
            hitNormal = hit.normal
        });
    }

    /// <summary>충돌 시 처리(임팩트 이펙트 등). 기본은 비움.</summary>
    protected virtual void OnHit(RaycastHit hit) { }

    protected void Despawn() => AddressablePoolManager.Instance.Return(this);
}
