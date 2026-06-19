using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneType { Initialize, Lobby, Game }

public class LoadSceneManager : Singleton<LoadSceneManager>
{
    [SerializeField] private TransitionCanvas transitionCanvas; 
    
    private SceneType currentScene;
    public SceneType CurrentScene => currentScene;

    public event Action OnBeforeMoveSceneEvent;
    public event Action OnAfterMoveSceneEvent;

    private bool runningTransition;
    private CancellationTokenSource cts;

    // 트랜지션 구동 중 여부. GameBase 가 자가 부팅 양보 판단에 사용.
    public bool IsTransitioning => runningTransition;
    
    protected override void Awake()
    {
        base.Awake();

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        currentScene = (SceneType)currentSceneIndex;
        cts = new CancellationTokenSource();
    }


    
    public void LoadSingleSceneImmediate(SceneType sceneType)
    {
        OnBeforeMoveSceneEvent?.Invoke();
        DOTween.KillAll();
        currentScene = sceneType;
        SceneManager.LoadScene((int)sceneType, LoadSceneMode.Single);
        OnAfterMoveSceneEvent?.Invoke();
    }

    /// <summary>
    /// 외부에서 호출하는 메인 함수
    /// useTransitionOut : true면 열리는 애니메이션 재생, false면 로드 후 즉시 컷(Off)
    /// </summary>
    public void LoadSceneWithTransition(SceneType sceneType, float startDelay = 0f, bool useTransitionOut = true)
    {
        if (runningTransition)
        {
            Debug.LogWarning($"{sceneType} => Already loading a scene.");
            return;
        }

        TransitionProcess(sceneType, startDelay, useTransitionOut).Forget();
    }

    private async UniTaskVoid TransitionProcess(SceneType sceneType, float startDelay, bool useTransitionOut)
    {
        runningTransition = true;
        DOTween.KillAll();

        // 1. 시작 딜레이 대기
        if (startDelay > 0)
            await UniTask.Delay(TimeSpan.FromSeconds(startDelay), ignoreTimeScale: true, cancellationToken: cts.Token);

        // 2. [Transition IN] 커튼 닫기 (화면 가림)
        float inDuration = transitionCanvas.TransitionIn();
        
        // 애니메이션이 끝날 때까지 대기
        if (inDuration > 0)
            await UniTask.Delay(TimeSpan.FromSeconds(inDuration), ignoreTimeScale: true, cancellationToken: cts.Token);

        // 3. 씬 비동기 로드
        
        OnBeforeMoveSceneEvent?.Invoke();
        currentScene = sceneType;

        transitionCanvas.EnableBlackBackground();
        await SceneManager.LoadSceneAsync((int)sceneType, LoadSceneMode.Single).ToUniTask(cancellationToken: cts.Token);
        
        OnAfterMoveSceneEvent?.Invoke();
        Resources.UnloadUnusedAssets();

        // 4. 로드된 씬의 GameBase(ISceneReady) 탐색 — 트랜지션이 초기화/시작 타이밍을 직접 구동
        ISceneReady activeController = FindSceneReadyController();

        // 5. [Initialize] 커튼 뒤에서 리소스 로드 대기. GameBase 없는 씬(빈 Initialize 등)은 최소 딜레이 폴백
        if (activeController != null)
        {
            await activeController.WaitForInitialization();
        }
        else
        {
            float minDelay = transitionCanvas.TransitionMinimumDelayTime;
            if (minDelay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(minDelay), ignoreTimeScale: true, cancellationToken: cts.Token);
        }

        // 6. 프레임 안정화 (렉 방지)
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: cts.Token);

        // 7. [Transition OUT] 커튼 열기 — 게임 시작 전에 화면을 먼저 연다
        if (useTransitionOut)
        {
            float outDuration = transitionCanvas.TransitionOut();

            if (outDuration > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(outDuration), ignoreTimeScale: true, cancellationToken: cts.Token);
        }

        // 8. 게임 시작 — 커튼이 열린 뒤 OnSceneOpened 호출 (OnGameStartEvent 등)
        activeController?.StartScene();

        // 9. 트랜지션 끄기 (Out 애니메이션이 없었으면 여기서 즉시 꺼짐 - Cut 효과)
        transitionCanvas.TransitionOff();
        runningTransition = false;

    }

    // 씬 로드 직후 활성 GameBase 를 찾는다. 없으면 null (빈 씬 등).
    private ISceneReady FindSceneReadyController() => FindFirstObjectByType<GameBase>();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        cts?.Cancel();
        cts?.Dispose();
    }
}