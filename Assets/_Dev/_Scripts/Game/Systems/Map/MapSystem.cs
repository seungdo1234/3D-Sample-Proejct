using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary> 장애물 종류 한 항목 (별도 어드레서블 키 + 등장 가중치) </summary>
[Serializable]
public struct ObstacleType
{
    public string key;            // 어드레서블/풀 키 (예: Obstacle_Rock)
    public float weight;          // 등장 가중치
    public bool randomYRotation;  // 생성 시 Y축 랜덤 회전 여부
}

/// <summary>
/// 무한 맵 담당. N×N 타일을 고정 개수로 두고 플레이어를 따라 칸 단위로 재배치한다.
/// 칸이 바뀌면 윈도우 밖으로 벗어난 타일만 재활용(이동)하므로, 그 타일의 장애물만 갱신된다.
/// 장애물은 셀 좌표를 시드로 결정적 배치 → 같은 칸 = 항상 같은 배치.
/// 맵이 먼저 깔려야 하므로 initOrder 를 EntitySpawnSystem 보다 앞에 둔다.
/// </summary>
public class MapSystem : GameInitializer<MainGame>
{
    [Header("# Keys")]
    [SerializeField] private string mapKey = "Map"; // MapChunk 컴포넌트를 가진 타일 프리팹

    [Header("# Grid")]
    [SerializeField] private float tileSize = 30f;          // 타일 한 변 크기 (월드 유닛)
    [SerializeField, Min(1)] private int gridRadius = 1;    // 중심 기준 반경 (1→3×3, 2→5×5)

    [Header("# Tile Mesh")]
    [SerializeField] private float baseMeshSize = 10f;      // 스케일 1일 때 메시 한 변 크기 (Unity Plane = 10)

    [Header("# Obstacles")]
    [SerializeField] private ObstacleType[] obstacleTypes;
    [SerializeField, Min(1)] private int divisions = 4;     // 타일 분할 수 (div×div 슬롯)
    [SerializeField, Range(0f, 1f)] private float spawnChance = 0.4f; // 슬롯당 배치 확률
    [SerializeField] private float clearRadius = 5f;        // 원점 주변 비울 반경 (시작 지점)
    [SerializeField]
    private AddressablePoolManager.PoolSettings obstaclePoolSettings = new()
    {
        initialSize = 10, spawnSize = 10, maxSize = 200
    };

    private Transform _player;
    private MapChunk[] _chunks;
    private Vector2Int _currentCell;
    private bool _chunksReady;
    private bool _running;

    // 재활용 Relayout 작업 버퍼 (GC 회피용 재사용)
    private readonly HashSet<Vector2Int> _needed = new();
    private readonly List<MapChunk> _free = new();

    protected override async UniTask OnInitialize(MainGame game)
    {
        var settings = await BuildObstacleSettings(); // 풀 등록 + 가중 테이블

        int side = gridRadius * 2 + 1;
        _chunks = new MapChunk[side * side];

        // 타일 묶음 / 장애물 묶음 (장애물 루트는 스케일 1 — 타일 스케일 상속 방지)
        var chunkRoot = new GameObject("Chunks").transform;
        chunkRoot.SetParent(transform, false);
        var obstacleRoot = new GameObject("Obstacles").transform;
        obstacleRoot.SetParent(transform, false);

        float scale = tileSize / baseMeshSize; // tileSize 30 / 메시 10 → 스케일 3

        for (int i = 0; i < _chunks.Length; i++)
        {
            var chunk = await AddressableManager.Instance.InstantiateAsync<MapChunk>(mapKey);
            if (chunk == null)
            {
                DebugUtil.DevelopmentLog($"[MapSystem] '{mapKey}' 로드 실패", DebugUtil.LogType.Warning);
                return;
            }
            chunk.transform.SetParent(chunkRoot, false);
            chunk.transform.localScale = new Vector3(scale, 1f, scale); // 평면은 X·Z 만 의미
            chunk.name = $"Map_{i / side}_{i % side}";
            chunk.Init(tileSize, settings, obstacleRoot);
            _chunks[i] = chunk;
        }

        _chunksReady = true;
        TryStart();
    }

    // 종류별 풀 등록 + 누적 가중치 구성
    private async UniTask<MapObstacleSettings> BuildObstacleSettings()
    {
        if (obstacleTypes == null || obstacleTypes.Length == 0)
            return new MapObstacleSettings { keys = Array.Empty<string>() };

        var keys = new string[obstacleTypes.Length];
        var randomRot = new bool[obstacleTypes.Length];
        var cum = new float[obstacleTypes.Length];
        float total = 0f;

        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            var t = obstacleTypes[i];
            await AddressablePoolManager.Instance.RegisterPool(t.key, obstaclePoolSettings);
            keys[i] = t.key;
            randomRot[i] = t.randomYRotation;
            total += Mathf.Max(0f, t.weight);
            cum[i] = total;
        }

        return new MapObstacleSettings
        {
            keys = keys, randomYRotations = randomRot, cumWeights = cum, totalWeight = total,
            divisions = divisions, spawnChance = spawnChance, clearRadius = clearRadius
        };
    }

    private void OnEnable()  => EventManager.Instance.Subscribe<OnPlayerSpawnedEvent>(OnPlayerSpawned);
    private void OnDisable()
    {
        if (EventManager.HasInstance)
            EventManager.Instance.Unsubscribe<OnPlayerSpawnedEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(OnPlayerSpawnedEvent e)
    {
        _player = e.Player;
        TryStart();
    }

    // 타일·플레이어 양쪽이 준비되면 시작 (도착 순서 무관)
    private void TryStart()
    {
        if (_running || !_chunksReady || _player == null) return;
        _running = true;
        _currentCell = ToCell(_player.position);
        Relayout();
    }

    private void Update()
    {
        if (!_running) return;

        var cell = ToCell(_player.position);
        if (cell == _currentCell) return; // 같은 칸이면 무시

        _currentCell = cell;
        Relayout();
    }

    private Vector2Int ToCell(Vector3 pos) =>
        new(Mathf.RoundToInt(pos.x / tileSize), Mathf.RoundToInt(pos.z / tileSize));

    // 현재 셀 중심 윈도우를 채우되, 이미 윈도우 안에 있는 타일은 그대로 두고
    // 벗어난 타일만 빈 셀로 이동(재활용)한다 → 변한 타일의 장애물만 갱신됨.
    private void Relayout()
    {
        _needed.Clear();
        for (int dz = -gridRadius; dz <= gridRadius; dz++)
        for (int dx = -gridRadius; dx <= gridRadius; dx++)
            _needed.Add(new Vector2Int(_currentCell.x + dx, _currentCell.y + dz));

        // 윈도우 안 타일은 유지(needed 에서 제거), 그 외는 재활용 후보
        _free.Clear();
        foreach (var chunk in _chunks)
        {
            if (chunk.HasCell && _needed.Contains(chunk.Cell)) _needed.Remove(chunk.Cell);
            else _free.Add(chunk);
        }

        // 남은 빈 셀에 재활용 타일 배정
        int fi = 0;
        foreach (var cell in _needed)
        {
            if (fi >= _free.Count) break;
            _free[fi++].SetCell(cell);
        }
    }
}
