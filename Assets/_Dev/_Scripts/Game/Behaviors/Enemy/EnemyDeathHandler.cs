using UnityEngine;

/// <summary>
/// 적 사망 처리(적 전용 — Player엔 대응 없음).
/// 추적을 멈추고 순수 물리 래그돌로 맞은 방향으로 날아가며 회전시킨다. 이때 EnemyDead 레이어로 바꿔
/// 다른 적/시체와는 충돌하지 않고 벽·땅에만 부딪히게 한다(충돌 매트릭스 설정). 땅에 멈추면 despawnDelay 뒤 풀 반납.
/// 순수 C#, Enemy가 생성·초기화·Tick. Die/Reset 으로 사망·부활(풀 재사용)을 전환한다.
/// </summary>
public class EnemyDeathHandler
{
    private Enemy _enemy;
    private Rigidbody _rb;

    private int _aliveLayer;
    private int _deadLayer;
    private RigidbodyConstraints _aliveConstraints;

    private float _launchForce, _launchUpBias, _torqueForce;
    private float _settleSpeed, _minAirTime, _despawnDelay, _maxDeathTime;

    private bool _dying;
    private bool _despawning;
    private float _deathAge;
    private float _despawnTimer;

    public bool IsDead => _dying;

    public void Initialize(Enemy enemy, string deadLayerName,
        float launchForce, float launchUpBias, float torqueForce,
        float settleSpeed, float minAirTime, float despawnDelay, float maxDeathTime)
    {
        _enemy = enemy;
        _rb = enemy.Rigidbody;

        _aliveLayer = enemy.gameObject.layer;      // 프리팹의 살아있을 때 레이어
        _aliveConstraints = _rb.constraints;       // 프리팹의 회전/위치 제약 (사망 시 풀었다 복원)
        _deadLayer = LayerMask.NameToLayer(deadLayerName);
        if (_deadLayer < 0)
            DebugUtil.DevelopmentLog($"[EnemyDeath] '{deadLayerName}' 레이어 없음 — 추가 필요", DebugUtil.LogType.Warning);

        _launchForce = launchForce;
        _launchUpBias = launchUpBias;
        _torqueForce = torqueForce;
        _settleSpeed = settleSpeed;
        _minAirTime = minAirTime;
        _despawnDelay = despawnDelay;
        _maxDeathTime = maxDeathTime;
    }

    /// <summary> 풀 재사용 시 — 사망 물리 상태 전체 원복 </summary>
    public void Reset()
    {
        _dying = false;
        _despawning = false;
        _deathAge = 0f;

        SetLayerRecursive(_enemy.gameObject, _aliveLayer);
        _rb.useGravity = false;
        _rb.constraints = _aliveConstraints;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _enemy.transform.rotation = Quaternion.identity;
    }

    /// <summary> 사망 진입 — 맞은 방향으로 날아가며 회전 </summary>
    public void Die(Vector3 hitDir)
    {
        _dying = true;
        _despawning = false;
        _deathAge = 0f;

        SetLayerRecursive(_enemy.gameObject, _deadLayer);
        _rb.constraints = RigidbodyConstraints.None; // 자유 회전
        _rb.useGravity = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        Vector3 launch = hitDir; launch.y = 0f;
        launch = launch.sqrMagnitude > 1e-6f ? launch.normalized : _enemy.transform.forward;
        launch.y = _launchUpBias; // 살짝 떠오르며 날아감

        _rb.AddForce(launch.normalized * _launchForce, ForceMode.Impulse);
        _rb.AddTorque(Random.onUnitSphere * _torqueForce, ForceMode.Impulse);
    }

    public void Tick()
    {
        if (!_dying) return;

        _deathAge += Time.deltaTime;

        if (_despawning)
        {
            _despawnTimer -= Time.deltaTime;
            if (_despawnTimer <= 0f)
                AddressablePoolManager.Instance.Return(_enemy);
            return;
        }

        // 최소 체공 후 속도가 충분히 떨어지면 "땅에 멈춤" → 반납 카운트다운.
        // 어떤 이유로도 안 멈추면(무한 낙하 등) maxDeathTime 에 강제 반납(풀 누수 방지).
        bool settled = _deathAge >= _minAirTime && _rb.linearVelocity.sqrMagnitude < _settleSpeed * _settleSpeed;
        if (settled || _deathAge >= _maxDeathTime)
        {
            _despawning = true;
            _despawnTimer = _despawnDelay;
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
