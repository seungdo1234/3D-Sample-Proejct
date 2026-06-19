using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 캔버스 컨트롤러. 그 자체로 하나의 시스템(IGameInitializable)이라
/// 시스템 컨테이너에 담겨 GameBase 가 초기화한다.
///
/// 생성된 UI_REGISTRY 를 읽어 "내 그룹(owner)"의 UI 만 자동으로 프리로드/오픈한다.
/// (enum / GetAddressableKey 스위치 / 별도 SpawnConfig SO 없음)
/// </summary>
public abstract class BaseCanvasController<TGame> : MonoBehaviour, IGameInitializable
    where TGame : GameBase
{
    [Header("# System Init Order")]
    [SerializeField] private int initOrder = 0;
    public int InitOrder => initOrder;

    [Header("# 이 캔버스가 담당할 UI 그룹")]
    [SerializeField] private CanvasGroupType owner;

    protected TGame game;

    private readonly Dictionary<string, UIBase<TGame>> _spawned = new();
    private readonly Dictionary<string, Task<UIBase<TGame>>> _loading = new();
    private readonly HashSet<string> _ownedKeys = new();
    private readonly HashSet<string> _initialized = new();

    /// <summary>씬별 언락 조건 판정. 기본은 모두 허용.</summary>
    protected virtual bool CheckContent(ContentType content) => true;

    public async UniTask InGameInitialize(GameBase gameBase)
    {
        game = gameBase as TGame;
        gameBase.MainCanvas = GetComponent<Canvas>();

        // 1. Collect my group's keys + figure out preload / open-on-start targets
        var preloadTasks = new List<UniTask>();
        var openOnStartKeys = new List<string>();
        foreach (var row in UI_REGISTRY.Rows)
        {
            if (row.owner != owner) continue;
            _ownedKeys.Add(row.key);

            bool shouldPreload = row.alwaysSpawn
                || (row.requireContent != ContentType.None && CheckContent(row.requireContent));
            if (shouldPreload)
                preloadTasks.Add(LoadAsync(row.key).AsUniTask());

            // 프리로드와 별개로, 시작 시 오픈은 openOnStart 인 것만 (예: HUD)
            if (row.openOnStart)
                openOnStartKeys.Add(row.key);
        }

        await UniTask.WhenAll(preloadTasks);

        // 2. Initialize preloaded UIs (they are loaded hidden) — 오픈은 안 함
        foreach (var key in _ownedKeys)
            if (_spawned.TryGetValue(key, out var ui) && ui != null)
                TryInitialize(key, ui);

        // 3. Open only the open-on-start UIs (e.g. HUD); 나머지는 OnOpenUIEvent 대기
        foreach (var key in openOnStartKeys)
            if (_spawned.TryGetValue(key, out var ui) && ui != null)
                ui.OpenUI();

        EventManager.Instance.Subscribe<OnOpenUIEvent>(OnOpenUI);
    }

    protected virtual void OnDestroy()
    {
        if (EventManager.HasInstance)
            EventManager.Instance.Unsubscribe<OnOpenUIEvent>(OnOpenUI);
    }

    private void OnOpenUI(OnOpenUIEvent e)
    {
        if (!_ownedKeys.Contains(e.key)) return; // 내 소관 아니면 무시
        OpenUIAsync(e.key).Forget();
    }

    private async UniTaskVoid OpenUIAsync(string key)
    {
        var ui = await LoadAsync(key);
        if (ui == null) return;

        TryInitialize(key, ui);
        ui.OpenUI(); // OpenUI handles SetActive + SetAsLastSibling
    }

    private void TryInitialize(string key, UIBase<TGame> ui)
    {
        if (!_initialized.Add(key)) return;
        ui.InGameInitialize(game).Forget();
    }

    // ── 로딩 (중복 로드 방지) ─────────────────────────────────────────────────
    private async Task<UIBase<TGame>> LoadAsync(string key)
    {
        if (_spawned.TryGetValue(key, out var existing)) return existing;
        if (_loading.TryGetValue(key, out var ongoing)) return await ongoing;

        var task = LoadInternal(key);
        _loading[key] = task;
        try { return await task; }
        finally { _loading.Remove(key); }
    }

    private async Task<UIBase<TGame>> LoadInternal(string key)
    {
        var ui = await AddressableManager.Instance.InstantiateAsync<UIBase<TGame>>(key, transform);
        if (ui != null)
        {
            _spawned[key] = ui;
            ui.gameObject.SetActive(false); // loaded hidden; OpenUI shows it
        }
        else DebugUtil.DevelopmentLog($"[Canvas] '{key}' load failed", DebugUtil.LogType.Warning);
        return ui;
    }
}
