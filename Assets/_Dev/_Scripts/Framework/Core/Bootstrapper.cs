using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

/// <summary>
/// 전역 부팅. "Bootstrap" 라벨이 앱 붙은 모든 영구 프리팹(SDK 초기화자, 매니저 등)을
/// Addressables 로 생성(Instantiate + DontDestroyOnLoad)한 뒤, 그 안의 IBootInitializable 을
/// Phase·Order 순으로 초기화한다 (SDK → 매니저).
///
/// EnsureReady 는 1회만 실행되고 캐시되므로, 어느 씬에서 진입하든(Initialize 씬 / 에디터 직접 Play)
/// 동일한 "완전 초기화" 상태를 보장한다. 진행률은 OnProgress 로 발행되며, Initialize 씬의
/// InitSceneDirector 만 구독해 로딩 UI 에 표시하고 다른 씬은 조용히 await 한다.
/// </summary>
public static class Bootstrapper
{
    // 이 라벨이 붙은 모든 프리팹을 부팅 시 생성한다.
    private const string BootstrapLabel = "Bootstrap";

    // overall(0~1), label
    public static event Action<float, string> OnProgress;
    // 필수 항목 실패 시 (부팅 중단됨)
    public static event Action<IBootInitializable, Exception> OnFatalError;

    public static bool IsReady { get; private set; }

    private static UniTask? _ready;

    /// <summary> 부팅을 1회 실행하고, 이후 호출은 같은 완료 태스크를 await 한다. </summary>
    // Preserve(): 캐싱한 UniTask 를 여러 씬에서 반복 await 할 수 있게 함
    public static UniTask EnsureReady() => _ready ??= RunAsync().Preserve();

    private static async UniTask RunAsync()
    {
        var ctx = new InitContext(CancellationToken.None);

        // 1. [생성] 라벨이 붙은 영구 프리팹 전부 Instantiate + DontDestroyOnLoad
        List<GameObject> roots = await InstantiateBootPrefabs();

        // 2. IBootInitializable 수집 (비활성 자식 포함)
        var initializables = new List<IBootInitializable>();
        foreach (GameObject go in roots)
            initializables.AddRange(go.GetComponentsInChildren<IBootInitializable>(true));

        // 3. Phase·Order 정렬 후 초기화 (SDK → 매니저)
        await RunInitialization(initializables, ctx);

        // 4. 프레임 안정화 (Awake/Start 정착)
        await UniTask.NextFrame();

        IsReady = true;
        OnProgress?.Invoke(1f, "Done");
    }

    private static async UniTask<List<GameObject>> InstantiateBootPrefabs()
    {
        var roots = new List<GameObject>();

        var locHandle = Addressables.LoadResourceLocationsAsync(BootstrapLabel, typeof(GameObject));
        var locations = await locHandle.Task;

        if (locHandle.Status != AsyncOperationStatus.Succeeded || locations.Count == 0)
        {
            Debug.LogError($"[Bootstrapper] '{BootstrapLabel}' 라벨이 붙은 프리팹을 찾지 못했습니다. " +
                           "SDK/매니저 프리팹에 라벨을 지정했는지 확인하세요.");
            Addressables.Release(locHandle);
            return roots;
        }

        foreach (var loc in locations)
        {
            var handle = Addressables.InstantiateAsync(loc);
            GameObject go = await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && go != null)
            {
                Object.DontDestroyOnLoad(go);
                roots.Add(go);
            }
            else
            {
                Debug.LogError($"[Bootstrapper] '{loc.PrimaryKey}' 생성 실패");
            }
        }

        Addressables.Release(locHandle);
        DebugUtil.DevelopmentLog($"[Bootstrapper] 영구 프리팹 {roots.Count}개 생성 완료");
        return roots;
    }

    private static async UniTask RunInitialization(List<IBootInitializable> items, InitContext ctx)
    {
        if (items.Count == 0) return;

        // Phase 우선, 같은 Phase 면 Order
        items.Sort((a, b) =>
        {
            int byPhase = ((int)a.Phase).CompareTo((int)b.Phase);
            return byPhase != 0 ? byPhase : a.Order.CompareTo(b.Order);
        });

        var aggregator = new InitProgressAggregator(items);
        aggregator.OnUpdated += (value, label) => OnProgress?.Invoke(value, label);

        // 같은 Phase + 같은 Order 끼리 병렬 배치, 다르면 순차 배리어
        int i = 0;
        while (i < items.Count)
        {
            InitPhase phase = items[i].Phase;
            int order = items[i].Order;
            var batch = new List<UniTask>();

            while (i < items.Count && items[i].Phase == phase && items[i].Order == order)
            {
                batch.Add(RunSingle(items[i], ctx, aggregator));
                i++;
            }

            await UniTask.WhenAll(batch); // 필수 항목 실패 시 여기서 예외 전파 → 부팅 중단
        }
    }

    // 단일 항목 초기화 + 재시도 + 실패 정책
    private static async UniTask RunSingle(IBootInitializable item, InitContext ctx, InitProgressAggregator aggregator)
    {
        IProgress<float> progress = aggregator.CreateFor(item);
        int attempt = 0;

        while (true)
        {
            try
            {
                DebugUtil.DevelopmentLog($"[Bootstrapper] 초기화 시작: {item.DisplayName}");
                await item.InitializeAsync(ctx, progress);
                progress.Report(1f); // 완료 보장
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                attempt++;
                if (attempt <= item.MaxRetry)
                {
                    DebugUtil.DevelopmentLog(
                        $"[Bootstrapper] '{item.DisplayName}' 실패, 재시도 {attempt}/{item.MaxRetry}: {e.Message}",
                        DebugUtil.LogType.Warning);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f * attempt), ignoreTimeScale: true);
                    continue;
                }

                if (item.Required)
                {
                    DebugUtil.DevelopmentLog(
                        $"[Bootstrapper] 필수 '{item.DisplayName}' 실패 → 부팅 중단: {e}",
                        DebugUtil.LogType.Error);
                    OnFatalError?.Invoke(item, e);
                    throw;
                }

                DebugUtil.DevelopmentLog(
                    $"[Bootstrapper] 선택 '{item.DisplayName}' 실패 → 건너뜀: {e.Message}",
                    DebugUtil.LogType.Warning);
                progress.Report(1f); // 건너뛰어도 진행률은 채운다
                return;
            }
        }
    }
}
