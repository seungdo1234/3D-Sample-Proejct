using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Initialize 씬의 진입점. 앱 시작 시 Bootstrapper.EnsureReady() 를 구동하면서
/// 진행률을 로딩 UI 에 표시하고, 완료되면 다음 씬(기본 Lobby)으로 트랜지션한다.
///
/// 실제 부팅 로직(생성·SDK 초기화·매니저 초기화)은 Bootstrapper 가 전담한다.
/// 다른 씬을 에디터에서 직접 Play 하면 GameBase 가 같은 EnsureReady() 를 UI 없이 await 한다.
/// </summary>
public class InitSceneDirector : MonoBehaviour
{
    [Header("# Flow")]
    [SerializeField] private SceneType nextScene = SceneType.Lobby;
    [SerializeField] private bool useTransition = true;

    [Header("# UX")]
    [Tooltip("프로그래스 바가 깜빡이지 않도록 보장하는 최소 표시 시간(초)")]
    [SerializeField] private float minDisplayTime = 0.5f;
    [Tooltip("IInitProgressView 를 구현한 UI (선택)")]
    [SerializeField] private MonoBehaviour progressView;

    private CancellationTokenSource _cts;

    private void Start() => BootAsync().Forget();

    private async UniTaskVoid BootAsync()
    {
        _cts = new CancellationTokenSource();

        var view = progressView as IInitProgressView;
        if (progressView != null && view == null)
            DebugUtil.DevelopmentLog("[InitSceneDirector] progressView 가 IInitProgressView 를 구현하지 않습니다.", DebugUtil.LogType.Warning);

        // Bootstrapper 진행률/에러 구독 (이 씬에서만 UI 표시)
        Action<float, string> onProgress = (value, label) => view?.SetProgress(value, label);
        Action<IBootInitializable, Exception> onError = (item, error) => view?.ShowError(item.DisplayName, error);
        Bootstrapper.OnProgress += onProgress;
        Bootstrapper.OnFatalError += onError;

        float startTime = Time.realtimeSinceStartup;
        bool ok = true;

        try
        {
            await Bootstrapper.EnsureReady();
        }
        catch (Exception e)
        {
            ok = false;
            DebugUtil.DevelopmentLog($"[InitSceneDirector] 부팅 실패 — 씬 전환 중단: {e.Message}", DebugUtil.LogType.Error);
        }
        finally
        {
            Bootstrapper.OnProgress -= onProgress;
            Bootstrapper.OnFatalError -= onError;
        }

        if (!ok) return;

        // 최소 표시 시간 보장
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minDisplayTime)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(minDisplayTime - elapsed),
                ignoreTimeScale: true, cancellationToken: _cts.Token);
        }

        if (!LoadSceneManager.HasInstance)
        {
            DebugUtil.DevelopmentLog(
                "[InitSceneDirector] LoadSceneManager 가 없습니다 — Bootstrap 라벨 프리팹에 매니저가 포함됐는지 확인하세요.",
                DebugUtil.LogType.Error);
            return;
        }

        LoadSceneManager.Instance.LoadSceneWithTransition(nextScene, useTransitionOut: useTransition);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
