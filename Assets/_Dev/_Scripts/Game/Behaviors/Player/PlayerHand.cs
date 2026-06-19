using UnityEngine;

/// <summary>
/// 무기 매니저. 장착 무기를 보유하고 조준(몸통 회전)·에임 라인·(추후)발사를 담당.
/// 조준은 총구가 회전 피벗에서 떨어진 오프셋을 보정해 총구가 커서를 정확히 향하게 한다. 순수 C#, Player가 틱 구동.
/// </summary>
public class PlayerHand
{
    private const float AttackLinger = 0.15f; // 마지막 발사 후 이동 감속 유지 시간(초)

    private Rigidbody _rb;
    private Transform _playerTr;
    private PlayerInputController _input;
    private Weapon _weapon;
    private PlayerVisual _visual;
    private PlayerStatusHandler _status;

    private Vector3 _lookDir;     // 조준 방향(수평 정규화). 투영 시점 플레이어 기준 — 이후 rb.position을 다시 빼지 않는다.
    private float _lookRange;     // 조준점까지 수평 거리 (측면 오프셋 보정용)
    private float _lateralRest;   // 정지 포즈 기준 총구 측면 오프셋(피벗 로컬 x). 달릴 때 라이플 스쿼시로 흔들리지 않게 고정값 사용.
    private bool _hasTarget;

    private bool _firing;        // 발사 버튼 누름 유지 중
    private float _cooldown;     // 다음 발사까지 남은 시간
    private float _attackLinger; // 남은 공격 감속 유지 시간

    public Weapon Weapon => _weapon;

    public void Initialize(Player player)
    {
        _rb = player.Rigidbody;
        _playerTr = player.transform;
        _input = player.Input;
        _weapon = player.Weapon;
        _visual = player.Visual;
        _status = player.Status;
        _input.OnLookInput += OnLook;
        _input.OnFirePressed += OnFirePressed;
        _input.OnFireReleased += OnFireReleased;

        // 측면 오프셋은 리그 고정 기하 — 정지 포즈에서 한 번만 캐싱(달릴 때 스케일 흔들림 배제).
        if (_weapon != null && _weapon.Muzzle != null)
            _lateralRest = _playerTr.InverseTransformPoint(_weapon.Muzzle.position).x;

        // 미연결 시 회전이 멈추므로 명확히 알림
        if (_weapon == null)
            Debug.LogWarning("[Hand] Player의 Weapon 슬롯이 비어있어 조준 회전이 동작하지 않음", player);
        else if (_weapon.Muzzle == null)
            Debug.LogWarning("[Hand] Weapon의 Muzzle 슬롯이 비어있어 조준 회전이 동작하지 않음", _weapon);
    }

    // 투영(LateUpdate) 그 순간의 플레이어 위치로 방향을 확정한다.
    // 절대 좌표를 저장했다가 나중(FixedUpdate)에 rb.position을 빼면 두 시점 차이(=속도×Δt)가
    // 오차로 들어가 이동 중 에임이 떨린다. 같은 시점 기준 방향만 남기면 그 오차가 사라진다.
    private void OnLook(Vector3 worldPoint)
    {
        Vector3 flat = worldPoint - _playerTr.position;
        flat.y = 0f;
        _lookRange = flat.magnitude;
        if (_lookRange > 1e-4f) _lookDir = flat / _lookRange;
        _hasTarget = true;
    }

    // 발사 버튼 누름: 연사 시작 + 즉발(쿨다운 0으로 첫 발은 이번 Tick에 바로 나감)
    private void OnFirePressed()
    {
        if (_weapon == null) return;
        _firing = true;
        _cooldown = 0f;
    }

    // 발사 버튼 뗌: 연사 종료(공격 감속은 린저 타이머가 자연 소멸)
    private void OnFireReleased() => _firing = false;

    // 실제 1발: 연출(애니메이터 트리거) + 무기 발사를 같은 시점에 호출해 싱크 보장.
    // 에임 방향 = 몸통 forward 수평 투영(에임 라인과 동일).
    private void FireOnce()
    {
        Vector3 dir = Vector3.ProjectOnPlane(_playerTr.forward, Vector3.up);
        if (dir.sqrMagnitude < 1e-6f) return;
        _visual?.Shoot();
        _weapon.Fire(BodyRef(), dir.normalized);
        _attackLinger = AttackLinger; // 발사할 때마다 감속 유지 시간 갱신
    }

    // 발사·조준 원점 보정의 기준점: 몸통 피벗(벽의 올바른 쪽)을 총구 높이로 맞춘 점.
    // 같은 높이라야 클리어런스 레이가 벽을 정확한 높이에서 검사한다.
    private Vector3 BodyRef()
    {
        Vector3 p = _playerTr.position;
        Transform muzzle = _weapon != null ? _weapon.Muzzle : null;
        if (muzzle != null) p.y = muzzle.position.y;
        return p;
    }

    /// <summary>프레임 단위: 에임 라인 + 연사 타이머 + 공격 감속 상태.</summary>
    public void Tick()
    {
        // 시작점 = 총구 실제 위치, 방향 = 몸통 forward를 수평 평면에 투영(레이는 항상 평면)
        Transform muzzle = _weapon != null ? _weapon.Muzzle : null;
        if (muzzle != null)
        {
            Vector3 dir = Vector3.ProjectOnPlane(_playerTr.forward, Vector3.up);
            if (dir.sqrMagnitude > 1e-6f)
                _weapon.UpdateAimLine(BodyRef(), dir.normalized); // 원점은 Weapon이 벽 관통 보정
        }

        // 연사: 버튼 유지 중이면 FireInterval 간격으로 발사
        if (_firing && _weapon != null)
        {
            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f)
            {
                FireOnce();
                _cooldown += _weapon.FireInterval; // 누적 보정(프레임 밀림 흡수)
            }
        }

        // 공격 감속 상태: "최근 발사" 기준으로 파생 (탭/연사 모두 일관)
        if (_attackLinger > 0f) _attackLinger -= Time.deltaTime;
        if (_status != null) _status.IsAttacking = _attackLinger > 0f;
    }

    /// <summary>물리: 총구가 커서를 향하도록 몸통 회전.</summary>
    public void FixedTick()
    {
        if (!_hasTarget || _weapon == null) return;
        Transform muzzle = _weapon.Muzzle;
        if (muzzle == null) return;

        // 총은 몸통 정면(+Z)으로 장착됐다고 보고, 총구는 위치(측면 오프셋)만 사용.
        // 측면 오프셋 = 피벗 기준 총구의 로컬 x. 애니메이션 흔들림을 피해 정지 포즈 캐싱값 사용.
        float lateral = _lateralRest;

        // 방향은 투영 시점에 확정됨(rb.position을 다시 빼지 않음 → 이동 중 떨림 제거).
        if (_lookRange < 1e-4f) return;

        // 총구가 피벗에서 옆으로 떨어져 있어 몸통을 목표로 그냥 향하면 발사선이 옆으로 빗나간다.
        // 발사선이 목표를 지나도록 yaw를 보정: yaw = alpha - asin(lateral / range)
        float alpha = Mathf.Atan2(_lookDir.x, _lookDir.z);           // 목표 방향 (Unity yaw)
        float s = Mathf.Clamp(lateral / _lookRange, -1f, 1f);        // 목표가 오프셋보다 가까우면 클램프
        float yaw = alpha - Mathf.Asin(s);

        _rb.MoveRotation(Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
    }

    public void Dispose()
    {
        if (_input != null)
        {
            _input.OnLookInput -= OnLook;
            _input.OnFirePressed -= OnFirePressed;
            _input.OnFireReleased -= OnFireReleased;
        }
    }
}
