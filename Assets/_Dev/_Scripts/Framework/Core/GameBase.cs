using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class GameBase : MonoBehaviour, ISceneReady
{
    [Header("# System Container")]
    [SerializeField] private string systemContainerKey;
    [SerializeField] private bool autoInitOnStart = true;

    public CancellationTokenSource GameQuitTokenSource { get; private set; }

    // 캔버스 컨트롤러가 초기화 시 자신의 Canvas 를 등록
    public Canvas MainCanvas { get; set; }

    // 모든 시스템 초기화가 끝난 시점에 발행 (UI 등 후속 훅용)
    public event Action OnSystemsInitialized;

    private GameObject _container;
    private readonly List<IGameInitializable> _systems = new();
    private bool _isInitialized;

    // 하위 컨트롤러가 구현하는 훅
    protected abstract UniTask PreInitialize();   // 컨테이너 로드 이전 전역 준비
    protected abstract UniTask PostInitialize();  // 모든 시스템 초기화 이후 마무리
    protected abstract void OnSceneOpened();      // 실제 플레이 시작 (StartScene 호출 시)

    // ── ISceneReady ──────────────────────────────────────────────────────────
    public async UniTask WaitForInitialization() => await InitializeGame();
    public void StartScene() => OnSceneOpened();

    // ── 생명주기 ──────────────────────────────────────────────────────────────
    private void Start()
    {
        // 트랜지션이 구동 중이면 LoadSceneManager 가 초기화/시작 타이밍을 잡으므로 자가 부팅을 양보.
        // (에디터에서 이 씬을 직접 Play 하면 트랜지션이 없어 자가 부팅 → 단독 실행 가능)
        bool drivenByTransition = LoadSceneManager.HasInstance
                               && LoadSceneManager.Instance.IsTransitioning;

        if (autoInitOnStart && !drivenByTransition) BootSelf().Forget();
    }

    protected virtual void OnDestroy()
    {
        GameQuitTokenSource?.Cancel();
        GameQuitTokenSource?.Dispose();
    }

    // ── 내부 초기화 파이프라인 ────────────────────────────────────────────────

    // isInitialized 가드: LoadSceneManager 와 자체 Start() 양쪽에서 불려도 1회만 실행
    private async UniTaskVoid BootSelf()
    {
        await InitializeGame();
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        StartScene();
    }

    public async UniTask InitializeGame()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        DebugUtil.DevelopmentLog($"[{gameObject.name}] 게임 초기화 시작");

        // Load DontDestroy managers first (once), then wait a frame — see Bootstrapper
        await Bootstrapper.EnsureReady();

        GameQuitTokenSource = new CancellationTokenSource();

        await PreInitialize();
        await SpawnSystemContainer();
        await InitializeSystems();
        await PostInitialize();

        OnSystemsInitialized?.Invoke();

        DebugUtil.DevelopmentLog($"[{gameObject.name}] 게임 초기화 완료");
    }

    private async UniTask SpawnSystemContainer()
    {
        if (string.IsNullOrEmpty(systemContainerKey))
        {
            DebugUtil.DevelopmentLog("[GameBase] systemContainerKey 가 비어있습니다.", DebugUtil.LogType.Warning);
            return;
        }

        // AddressableManager 를 통해 생성 → 씬 전환 시 자동 해제 추적됨
        _container = await AddressableManager.Instance
            .InstantiateAsync<GameObject>(systemContainerKey, transform);

        if (_container == null)
        {
            DebugUtil.DevelopmentLog($"[GameBase] '{systemContainerKey}' 컨테이너 로드 실패", DebugUtil.LogType.Warning);
            return;
        }

        // 비활성 자식 포함 수집
        _systems.AddRange(_container.GetComponentsInChildren<IGameInitializable>(true));
        DebugUtil.DevelopmentLog($"[GameBase] 시스템 {_systems.Count}개 수집 완료");
    }

    private async UniTask InitializeSystems()
    {
        if (_systems.Count == 0) return;

        // InitOrder 오름차순 정렬 — 같은 값은 병렬, 다른 값은 그룹 배리어로 순차
        _systems.Sort((a, b) => a.InitOrder.CompareTo(b.InitOrder));

        int i = 0;
        while (i < _systems.Count)
        {
            int order = _systems[i].InitOrder;
            var batch = new List<UniTask>();

            while (i < _systems.Count && _systems[i].InitOrder == order)
            {
                batch.Add(_systems[i].InGameInitialize(this));
                i++;
            }

            await UniTask.WhenAll(batch); // 그룹 내 병렬 → 끝나면 다음 그룹
        }
    }
}
