using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무한 맵의 한 칸(타일). 바닥 평면 + 그 칸의 장애물을 담당한다.
/// SetCell 로 셀 좌표를 받아 위치를 스냅하고, 셀이 바뀌면 장애물을 재배치한다.
/// 배치는 셀 좌표를 시드로 한 결정적(deterministic) 지터드 그리드 →
/// 같은 칸으로 돌아오면 같은 배치, 슬롯 안에 가두므로 타일 경계 너머도 겹치지 않는다.
/// </summary>
public class MapChunk : MonoBehaviour
{
    // 공간 해싱 상수 — 서로 다른 큰 소수로 셀 좌표 비트를 섞는다.
    private const int HashPrimeX = 73856093;
    private const int HashPrimeY = 19349663;

    public Vector2Int Cell { get; private set; }
    public bool HasCell { get; private set; }

    private float _tileSize;
    private MapObstacleSettings _obstacles;
    private Transform _obstacleRoot;                  // 스케일 1 컨테이너 (타일 스케일 상속 방지)
    private readonly List<PoolObject> _spawned = new();

    public void Init(float tileSize, MapObstacleSettings obstacles, Transform obstacleRoot)
    {
        _tileSize = tileSize;
        _obstacles = obstacles;
        _obstacleRoot = obstacleRoot;
    }

    public void SetCell(Vector2Int cell)
    {
        if (HasCell && cell == Cell) return; // 같은 칸이면 갱신 불필요
        Cell = cell;
        HasCell = true;

        var pos = transform.position;
        transform.position = new Vector3(cell.x * _tileSize, pos.y, cell.y * _tileSize);

        Populate();
    }

    // 셀 좌표를 시드로 장애물을 결정적으로 재배치
    private void Populate()
    {
        ReleaseObstacles();
        if (_obstacles == null || !_obstacles.HasAny) return;

        int div = _obstacles.divisions;
        float slot = _tileSize / div;
        float half = _tileSize * 0.5f;
        float ox = Cell.x * _tileSize - half; // 타일 좌하단 코너 (월드)
        float oz = Cell.y * _tileSize - half;

        var rng = new System.Random(Hash(Cell));
        float clearSqr = _obstacles.clearRadius * _obstacles.clearRadius;

        for (int sz = 0; sz < div; sz++)
        for (int sx = 0; sx < div; sx++)
        {
            // 슬롯 순서대로 rng 를 소비해야 결정성 유지 → 확률 체크부터
            if (rng.NextDouble() >= _obstacles.spawnChance) continue;

            float cx = ox + (sx + 0.5f) * slot;
            float cz = oz + (sz + 0.5f) * slot;
            if (cx * cx + cz * cz < clearSqr) continue; // 시작 지점 비우기

            int ti = _obstacles.PickIndex(rng);
            var obj = AddressablePoolManager.Instance.Spawn<Obstacle>(_obstacles.keys[ti], _obstacleRoot);
            if (obj == null) continue;

            // 슬롯 안에 가두는 지터 한계 (반지름만큼 여백) → 음수면 슬롯보다 큼 → 스킵
            float maxJit = slot * 0.5f - obj.Radius;
            if (maxJit < 0f)
            {
                AddressablePoolManager.Instance.Return(obj);
                continue;
            }

            float jx = ((float)rng.NextDouble() * 2f - 1f) * maxJit;
            float jz = ((float)rng.NextDouble() * 2f - 1f) * maxJit;
            float ry = _obstacles.randomYRotations[ti] ? (float)rng.NextDouble() * 360f : 0f;

            obj.transform.SetPositionAndRotation(
                new Vector3(cx + jx, obj.transform.position.y, cz + jz),
                Quaternion.Euler(0f, ry, 0f));
            _spawned.Add(obj);
        }
    }

    private void ReleaseObstacles()
    {
        for (int i = 0; i < _spawned.Count; i++)
            AddressablePoolManager.Instance.Return(_spawned[i]);
        _spawned.Clear();
    }

    // 셀 좌표 → 안정적 해시 (스폰서식 해시; 인접 셀 간 상관 적음)
    private static int Hash(Vector2Int c)
    {
        unchecked { return c.x * HashPrimeX ^ c.y * HashPrimeY; }
    }
}
