using UnityEngine;

/// <summary>
/// 적 추적 이동. 타깃 방향으로 직진(Rigidbody.MovePosition → 장애물 슬라이드)하고,
/// 피격 시 넉백(감쇠)+스턴을 반영한다. 겹침 밀어내기로 인한 공중 부양을 막기 위해 Y는 물리(중력)에
/// 맡기지 않고, 일정 간격으로 아래로 레이캐스트해 그 자리 바닥 높이로 스냅한다(이동 중 지형/장애물 추종).
/// 속도는 스탯에서 가져온다. 순수 C#, Enemy가 FixedTick 을 구동. (PlayerMovement 대응)
/// </summary>
public class EnemyMovement
{
    private Rigidbody _rb;
    private EnemyStatusHandler _status;
    private Transform _target;

    private bool _faceMoveDir;
    private float _knockbackDecel;
    private float _stunDuration;

    private LayerMask _groundMask;
    private float _probeHeight;     // 바닥 레이를 위/아래로 쏘는 범위
    private float _sampleInterval;  // 바닥 재샘플 간격(쓰로틀)
    private float _footOffset;      // 피벗→콜라이더 바닥 거리 (표면 위 접지 보정)

    private Vector3 _knockback;
    private float _stunTimer;
    private float _groundY;          // 현재 추종 중인 바닥 높이(샘플 사이엔 캐시 유지)
    private float _groundSampleTimer;

    public void Initialize(Enemy enemy, bool faceMoveDir, float knockbackDecel, float stunDuration,
        LayerMask groundMask, float groundProbeHeight, float groundSampleInterval)
    {
        _rb = enemy.Rigidbody;
        _status = enemy.Status;
        _faceMoveDir = faceMoveDir;
        _knockbackDecel = knockbackDecel;
        _stunDuration = stunDuration;

        _groundMask = groundMask;
        _probeHeight = Mathf.Max(0.01f, groundProbeHeight);
        _sampleInterval = Mathf.Max(0f, groundSampleInterval);

        // 피벗에서 콜라이더 바닥까지의 거리 = 발 오프셋. 바닥 표면 위에 이만큼 띄워야 접지된다.
        var col = enemy.GetComponentInChildren<Collider>();
        _footOffset = col != null ? enemy.transform.position.y - col.bounds.min.y : 0f;
    }

    public void SetTarget(Transform target) => _target = target;

    /// <summary> 스폰 시 호출 — 넉백/스턴 초기화 + 고정 높이 갱신 </summary>
    public void OnSpawn(float groundY)
    {
        _knockback = Vector3.zero;
        _stunTimer = 0f;
        _groundY = groundY;
        // 첫 재샘플 타이밍을 적마다 흩어 한 프레임에 레이캐스트가 몰리지 않게(스태거)
        _groundSampleTimer = Random.value * _sampleInterval;
    }

    /// <summary> 피격 넉백 + 스턴 </summary>
    public void ApplyKnockback(Vector3 hitDir, float force)
    {
        hitDir.y = 0f;
        _knockback = hitDir.sqrMagnitude > 1e-6f ? hitDir.normalized * force : Vector3.zero;
        _stunTimer = _stunDuration;
    }

    public void FixedTick()
    {
        if (_target == null) return;

        float dt = Time.fixedDeltaTime;
        Vector3 pos = _rb.position;
        Vector3 move = Vector3.zero;

        // 스턴 중이 아니면 타깃 방향으로 추적
        if (_stunTimer > 0f) _stunTimer -= dt;
        else
        {
            Vector3 to = _target.position - pos; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = to.normalized;
                move += dir * (_status.MoveSpeed * dt);
                if (_faceMoveDir) _rb.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
            }
        }

        // 넉백(감쇠) 적용 — 추적과 합산해 밀어냄(콜라이더 슬라이드 유지)
        if (_knockback.sqrMagnitude > 0.0001f)
        {
            move += _knockback * dt;
            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, _knockbackDecel * dt);
        }

        Vector3 target = pos + move;

        // 바닥 추종: 매 프레임이 아니라 _sampleInterval 간격으로만 레이캐스트(쓰로틀).
        // 도착할 XZ 위에서 아래로 쏴 그 자리 표면을 찾고, 발 오프셋만큼 띄운 값을 캐시.
        _groundSampleTimer -= dt;
        if (_groundSampleTimer <= 0f)
        {
            _groundSampleTimer = _sampleInterval;
            Vector3 origin = new(target.x, target.y + _probeHeight, target.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _probeHeight * 2f,
                    _groundMask, QueryTriggerInteraction.Ignore))
                _groundY = hit.point.y + _footOffset;
        }

        // Y 는 물리(중력)가 아니라 캐시된 바닥 높이로 스냅 → 겹침 밀어내기로 떠올라도 매 스텝 바닥으로 되돌림
        target.y = _groundY;
        _rb.MovePosition(target);
    }
}
