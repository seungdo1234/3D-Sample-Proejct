using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 어드레서블 기반 오브젝트 풀.
/// 프리팹은 1회 로드 후 Instantiate 복제로 재사용. 대상은 PoolObject 상속(재사용 = obj as T).
/// 인스턴스가 PoolKey 를 들고 있어 Return(obj) 에 키 인자 불필요.
/// string 키 단일 / 성장정책 initial·spawn·max(소프트캡) / 씬 스코프(매니저 파괴 시 함께 정리).
/// </summary>
public class AddressablePoolManager : Singleton<AddressablePoolManager>
{
    [Serializable]
    public class PoolSettings
    {
        public bool isCanvasPool = false;
        public int initialSize = 5;
        public int spawnSize = 5;
        public int maxSize = 100;
    }

    private class Pool
    {
        public string key;
        public PoolObject prefab;
        public PoolSettings settings;
        public Transform parent;
        public int spawnedCount;
        public readonly Queue<PoolObject> idle = new();
    }

    [Tooltip("isCanvasPool 풀의 부모 캔버스 어드레서블 키. 로드 실패 시 PoolParent 로 대체")]
    [SerializeField] private string poolCanvasKey = "PoolCanvas";

    private Transform _poolParent;
    private Transform _poolCanvas;
    private int _epoch; // 씬 전환 시 증가 → 진행 중이던 로드를 무효화
    private readonly Dictionary<string, Pool> _pools = new();
    private readonly Dictionary<string, Task<Pool>> _loading = new(); // 동시 로드 중복 방지

    private void Start()
    {
        // 씬 전환 시 풀 정리. 풀 객체는 씬 소속이라 씬 언로드로 자동 파괴되고, 매니저는 참조만 비운다.
        if (LoadSceneManager.Instance != null)
            LoadSceneManager.Instance.OnBeforeMoveSceneEvent += ClearForSceneMove;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (LoadSceneManager.HasInstance)
            LoadSceneManager.Instance.OnBeforeMoveSceneEvent -= ClearForSceneMove;
    }

    // 씬 전환 시 호출. 객체 파괴는 씬 언로드에 맡기고, 매니저의 풀 관련 상태만 초기화한다.
    private void ClearForSceneMove()
    {
        _epoch++;          // 진행 중이던 CreatePool 을 무효화
        _pools.Clear();
        _loading.Clear();
        _poolParent = null; // 씬과 함께 파괴됨 → 다음 등록 시 재생성
        _poolCanvas = null;
    }

    // PoolParent 를 현재 씬에 지연 생성 (매니저 자식 아님 → 씬 언로드로 자동 정리)
    private void EnsurePoolParent()
    {
        if (_poolParent == null)
            _poolParent = new GameObject("PoolParent").transform;
    }

    // 캔버스 풀의 부모 캔버스를 현재 씬에 지연 소환
    private async UniTask<Transform> EnsurePoolCanvasAsync()
    {
        if (_poolCanvas != null) return _poolCanvas;

        // AddressableManager 가 씬 스폰으로 추적 → 씬 전환 시 자동 해제
        var canvas = await AddressableManager.Instance.InstantiateAsync<Transform>(poolCanvasKey);
        if (canvas != null)
        {
            canvas.SetParent(null, false); // 현재 씬 루트로 이동
            canvas.gameObject.SetActive(true);
        }
        _poolCanvas = canvas;
        return _poolCanvas;
    }

    public bool HasPool(string key) => _pools.ContainsKey(key);

    // ─────────────────────────────────────────────── 등록 / 스폰 / 반납

    /// <summary> 설정대로 풀 로드·등록 (initialSize 만큼 프리워밍) </summary>
    public async UniTask RegisterPool(string key, PoolSettings settings)
        => await GetOrCreatePool(key, settings);

    /// <summary> 스폰. 미등록이면 기본 설정으로 자동 등록 </summary>
    public async UniTask<T> SpawnAsync<T>(string key, Transform parent = null) where T : PoolObject
    {
        var pool = await GetOrCreatePool(key, null);
        return pool != null ? Take<T>(pool, parent) : null;
    }

    /// <summary> 동기 스폰. 풀이 이미 있어야 함(RegisterPool/SpawnAsync 선행) </summary>
    public T Spawn<T>(string key, Transform parent = null) where T : PoolObject
    {
        if (_pools.TryGetValue(key, out var pool)) return Take<T>(pool, parent);

        DebugUtil.DevelopmentLog($"[Pool] '{key}' not ready — call RegisterPool/SpawnAsync first", DebugUtil.LogType.Warning);
        return null;
    }

    /// <summary> 풀에 반납. 키는 PoolObject.PoolKey 에서 읽음 </summary>
    public void Return(PoolObject obj)
    {
        if (obj == null || obj.InPool) return; // null 또는 이미 반납됨

        if (!_pools.TryGetValue(obj.PoolKey, out var pool))
        {
            Destroy(obj.gameObject); // 풀 없음(씬 정리됨)
            return;
        }

        obj.InPool = true;
        obj.gameObject.SetActive(false);
        if (obj.transform.parent != pool.parent) obj.transform.SetParent(pool.parent, false);
        pool.idle.Enqueue(obj);
    }

    // ─────────────────────────────────────────────── 내부

    private T Take<T>(Pool pool, Transform parent) where T : PoolObject
    {
        PoolObject obj = null;
        while (pool.idle.Count > 0 && obj == null)
            obj = pool.idle.Dequeue(); // 파괴된 항목 건너뜀

        if (obj == null)
        {
            Grow(pool);
            obj = pool.idle.Count > 0 ? pool.idle.Dequeue() : InstantiateOne(pool);
        }

        obj.transform.SetParent(parent != null ? parent : pool.parent, false);
        obj.InPool = false;
        obj.gameObject.SetActive(true);
        return obj as T; // 참조 캐스팅 (핫패스 GetComponent 없음)
    }

    private void Grow(Pool pool)
    {
        int remaining = pool.settings.maxSize - pool.spawnedCount;
        int count = remaining > 0 ? Mathf.Min(pool.settings.spawnSize, remaining) : 1; // 소프트캡: 스폰 실패는 안 시킴
        for (int i = 0; i < count; i++)
            pool.idle.Enqueue(InstantiateOne(pool));
    }

    private PoolObject InstantiateOne(Pool pool)
    {
        var obj = Instantiate(pool.prefab, pool.parent);
        obj.PoolKey = pool.key;
        obj.InPool = true;
        obj.name = $"{pool.prefab.name}({++pool.spawnedCount})";
        obj.gameObject.SetActive(false);
        return obj;
    }

    private async UniTask<Pool> GetOrCreatePool(string key, PoolSettings settings)
    {
        if (_pools.TryGetValue(key, out var existing)) return existing;
        if (_loading.TryGetValue(key, out var ongoing)) return await ongoing;

        var task = CreatePool(key, settings ?? new PoolSettings());
        _loading[key] = task;
        try { return await task; }
        finally { _loading.Remove(key); }
    }

    private async Task<Pool> CreatePool(string key, PoolSettings settings)
    {
        int epoch = _epoch;

        var go = await AddressableManager.Instance.LoadAssetAsync<GameObject>(key);
        if (epoch != _epoch) return null; // 로드 중 씬 전환됨 → 폐기
        if (go == null)
        {
            DebugUtil.DevelopmentLog($"[Pool] '{key}' addressable load failed", DebugUtil.LogType.Warning);
            return null;
        }

        var prefab = go.GetComponent<PoolObject>();
        if (prefab == null)
        {
            DebugUtil.DevelopmentLog($"[Pool] '{key}' prefab has no PoolObject component", DebugUtil.LogType.Warning);
            return null;
        }

        // 부모 결정: 캔버스 풀은 캔버스 소환, 일반 풀은 PoolParent (둘 다 현재 씬 소속)
        Transform parent;
        if (settings.isCanvasPool)
        {
            var canvas = await EnsurePoolCanvasAsync();
            if (epoch != _epoch) return null; // 캔버스 소환 중 씬 전환됨 → 폐기
            EnsurePoolParent();
            parent = canvas != null ? canvas : _poolParent;
        }
        else
        {
            EnsurePoolParent();
            parent = _poolParent;
        }

        var pool = new Pool
        {
            key = key,
            prefab = prefab,
            settings = settings,
            parent = parent,
        };
        _pools[key] = pool;

        for (int i = 0; i < settings.initialSize; i++)
            pool.idle.Enqueue(InstantiateOne(pool));

        DebugUtil.DevelopmentLog($"[Pool] '{key}' registered (initial {settings.initialSize})");
        return pool;
    }
}
