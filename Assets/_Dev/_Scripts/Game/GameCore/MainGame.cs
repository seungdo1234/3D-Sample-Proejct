using Cysharp.Threading.Tasks;
using UnityEngine;

public class MainGame : GameBase
{
    public bool GameWin { get; private set; }
    public bool IsGameOver { get; private set; }

    // 게임오버 시점의 최종 킬 수 (결과 팝업이 읽는 단일 소스)
    public int FinalKillCount { get; private set; }

    protected override async UniTask PreInitialize()
    {
        // 전역 준비 (이벤트 구독, 풀 워밍 등)
        EventManager.Instance.Subscribe<OnPlayerDeadEvent>(OnPlayerDead);
        await UniTask.CompletedTask;
    }

    protected override async UniTask PostInitialize()
    {
        await UniTask.CompletedTask;
    }

    protected override void OnSceneOpened()
    {
        DebugUtil.DevelopmentLog("[MainGame] 씬 오픈 — 게임 시작!");
        EventManager.Instance.Publish(new OnGameStartEvent()); // 구독 시스템들 시작 (Player 조작 활성화 등)
    }

    // 플레이어 사망 = 게임오버. 결과 저장 후 결과 팝업을 연다. (적 소환 중지는 EntitySpawnSystem 이 구독)
    private void OnPlayerDead(OnPlayerDeadEvent e)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        FinalKillCount = e.KillCount;

        // 전체 정지: 적 이동/스폰·플레이어 이동 모두 멈춤. 팝업은 SetUpdate(true)라 정상 재생.
        Time.timeScale = 0f;

        DebugUtil.DevelopmentLog($"[MainGame] 게임오버 — 최종 킬 {FinalKillCount}");
        EventManager.Instance.Publish(new OnOpenUIEvent(UIKeys.UI_GameResult));
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Time.timeScale = 1f; // 씬 종료/재시작 시 정지 해제 (다음 씬이 멈춘 채 시작하지 않도록)
        if (EventManager.HasInstance)
            EventManager.Instance.Unsubscribe<OnPlayerDeadEvent>(OnPlayerDead);
    }
}
