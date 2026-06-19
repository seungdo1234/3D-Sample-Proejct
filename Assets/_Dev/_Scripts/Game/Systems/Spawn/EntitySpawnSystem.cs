using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 엔티티 소환 담당.
/// Player: 1개만 필요 → 어드레서블로 직접 로드·소환 (조작 활성화는 게임 시작 시).
/// Enemy: 풀링 대상 → 풀 등록 후, 게임 시작 시점부터 플레이어 주변 원형 링(카메라 밖)에서
///        모든 방향으로 주기 소환한다. 추적 이동은 각 적이 자체 구동하며, 여기서는 소환과
///        플레이어가 멀리 도망가 despawnRadius 를 벗어난 적의 재배치(카메라 밖 새 지점)만 관리한다.
/// </summary>
public class EntitySpawnSystem : GameInitializer<MainGame>
{
    [Header("# Keys")]
    [SerializeField] private string playerKey = "Player";
    [SerializeField] private string enemyKey = "Enemy";

    [Header("# Player")]
    [SerializeField] private Transform playerSpawnPoint; // 없으면 원점

    [Header("# Enemy Pool")]
    [SerializeField]
    private AddressablePoolManager.PoolSettings enemyPoolSettings = new()
    {
        initialSize = 20, spawnSize = 10, maxSize = 200
    };

    [Header("# Enemy Spawn")]
    [SerializeField] private float spawnInterval = 1f;      // 소환 묶음 간격(초)
    [SerializeField, Min(1)] private int spawnPerBatch = 3;  // 한 번에 소환할 수
    [SerializeField, Min(1)] private int maxAlive = 50;      // 동시 생존 상한 (풀 maxSize 이하로)

    [Header("# Enemy Distance")]
    [Tooltip("플레이어 기준 소환 반경 — 카메라 가시 반경보다 크게 (화면 밖 보장)")]
    [SerializeField] private float spawnRadius = 14f;
    [Tooltip("이 반경을 벗어난 적은 카메라 밖 새 지점으로 재배치 (플레이어가 도망간 경우)")]
    [SerializeField] private float despawnRadius = 22f;

    [Header("# Ground")]
    [Tooltip("스폰 지점의 실제 바닥을 찾기 위한 레이캐스트 대상 (바닥+장애물 레이어)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("바닥 탐색 레이를 스폰 XZ 위 이 높이에서 아래로 쏜다")]
    [SerializeField] private float groundProbeHeight = 50f;

    public Player Player { get; private set; }

    private bool _spawning;
    private float _timer;
    private readonly List<Enemy> _active = new();

    protected override async UniTask OnInitialize(MainGame game)
    {
        // Enemy: 풀 등록만 (소환은 게임 시작 시)
        await AddressablePoolManager.Instance.RegisterPool(enemyKey, enemyPoolSettings);

        // Player: 소환만 (컴포넌트 초기화는 게임 시작 시)
        await SpawnPlayer();

        // 게임 시작 시 Player 조작 활성화 + 적 소환 시작
        EventManager.Instance.Subscribe<OnGameStartEvent>(OnGameStart);
        // 플레이어 사망 시 소환 중지
        EventManager.Instance.Subscribe<OnPlayerDeadEvent>(OnPlayerDead);
    }

    private async UniTask SpawnPlayer()
    {
        Player = await AddressableManager.Instance.InstantiateAsync<Player>(playerKey);
        if (Player == null)
        {
            DebugUtil.DevelopmentLog($"[EntitySpawnSystem] '{playerKey}' 로드 실패", DebugUtil.LogType.Warning);
            return;
        }

        var t = Player.transform;
        t.SetParent(null); // 씬 루트로
        t.position = playerSpawnPoint ? playerSpawnPoint.position : Vector3.zero;
        Player.gameObject.SetActive(true);

        // 스폰 완료 통지 (카메라 등 구독 시스템이 트랜스폼을 받아 연결)
        EventManager.Instance.Publish(new OnPlayerSpawnedEvent { Player = t });
    }

    private void OnGameStart(OnGameStartEvent e)
    {
        Player?.Initialize();
        _spawning = Player != null; // 적 소환 시작
    }

    // 플레이어 사망 → 소환 중지 (기존 적은 그대로 두고 추가 소환만 멈춤)
    private void OnPlayerDead(OnPlayerDeadEvent e) => _spawning = false;

    // ─────────────────────────────────────────────── 적 소환 / 추적 / 재배치

    private void Update()
    {
        if (!_spawning) return;

        // 주기적 소환
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemyBatch();
        }

        // 죽은 적 추적 목록에서 제거 + 뒤처진 적 재배치 (역순: 제거 안전)
        float despawnSqr = despawnRadius * despawnRadius;
        Vector3 p = Player.transform.position;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var enemy = _active[i];
            if (enemy.IsDead)
            {
                _active.RemoveAt(i); // 이후 사망 물리/반납은 Enemy 가 자체 처리
                continue;
            }
            if (enemy.DistanceSqrTo(p) > despawnSqr)
                PlaceAroundPlayer(enemy);
        }
    }

    private void SpawnEnemyBatch()
    {
        for (int i = 0; i < spawnPerBatch; i++)
        {
            if (_active.Count >= maxAlive) return;

            var enemy = AddressablePoolManager.Instance.Spawn<Enemy>(enemyKey);
            if (enemy == null) return; // 풀 미준비/로드 실패

            enemy.Setup(Player.transform);
            PlaceAroundPlayer(enemy);
            _active.Add(enemy);
        }
    }

    // 플레이어 주변 원형 링의 랜덤 방향(전 방위) + 카메라 밖 위치로 배치
    private void PlaceAroundPlayer(Enemy enemy)
    {
        float ang = Random.value * Mathf.PI * 2f;
        Vector3 dir = new(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

        Vector3 pos = Player.transform.position + dir * spawnRadius;

        // 프리팹의 접지 오프셋(평지 기준 피벗 높이) — 표면 위에 이만큼 띄워 발을 맞춘다
        float groundOffset = enemy.transform.position.y;

        // 스폰 XZ 위쪽에서 아래로 쏴 그 자리의 실제 바닥 높이를 찾는다.
        // (장애물 위/솟은 지형에 떠 있는 문제 방지 — groundY 기준이 실제 표면이 되도록)
        Vector3 origin = new(pos.x, pos.y + groundProbeHeight, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeHeight * 2f, groundMask))
            pos.y = hit.point.y + groundOffset; // 평지(hit.y=0)면 기존과 동일, 솟은 곳이면 그만큼 위로
        else
            pos.y = groundOffset; // 폴백: 바닥 못 찾으면 프리팹 높이 유지

        enemy.Warp(pos);
    }

    private void OnDestroy()
    {
        if (EventManager.HasInstance)
        {
            EventManager.Instance.Unsubscribe<OnGameStartEvent>(OnGameStart);
            EventManager.Instance.Unsubscribe<OnPlayerDeadEvent>(OnPlayerDead);
        }
    }
}
