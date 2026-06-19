using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 인게임 카메라 담당. 플레이어 스폰 이벤트를 받아 vcam 의 Tracking Target 을 연결한다.
/// 구독은 OnEnable 에서 (컨테이너 인스턴스화 시점) — EntitySpawnSystem 발행보다 먼저 보장.
/// 추후 흔들림·줌·구역 전환 등 카메라 연출이 늘면 여기에 모은다.
/// </summary>
public class GameCameraSystem : GameInitializer<MainGame>
{
    [Header("# Camera")]
    [SerializeField] private CinemachineCamera vcam; // 컨테이너 프리팹 안의 vcam (씬 Brain 이 자동 인식)

    protected override UniTask OnInitialize(MainGame game) => UniTask.CompletedTask;

    private void OnEnable()
    {
        EventManager.Instance.Subscribe<OnPlayerSpawnedEvent>(OnPlayerSpawned);
    }

    private void OnDisable()
    {
        if (EventManager.HasInstance)
            EventManager.Instance.Unsubscribe<OnPlayerSpawnedEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(OnPlayerSpawnedEvent e)
    {
        if (vcam == null)
        {
            DebugUtil.DevelopmentLog("[GameCameraSystem] vcam 미할당", DebugUtil.LogType.Warning);
            return;
        }
        vcam.Follow = e.Player; // Tracking Target 연결
    }
}
