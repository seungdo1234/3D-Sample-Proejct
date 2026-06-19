using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// SDK 초기화 샘플(플레이스홀더). "Bootstrap" 라벨이 붙은 SDK 프리팹의 컴포넌트로 둔다.
/// Phase 를 Sdk 로 두면 매니저보다 먼저 초기화된다. 실제 프로젝트에서는 이 클래스를 복사해
/// 광고/애널리틱스/인증 SDK 의 비동기 초기화를 넣고, 필요하면 결과를 ctx.Set(...) 으로 저장해
/// 매니저(Phase.Managers)에서 사용한다.
/// </summary>
public class SampleSdkInitializer : BootInitializableBase
{
    [Header("# Sample SDK")]
    [Tooltip("실제 SDK 초기화 대신 흉내낼 지연 시간(초). 실제 구현 시 제거")]
    [SerializeField] private float fakeDurationSeconds = 1f;

    public override async UniTask InitializeAsync(InitContext ctx, IProgress<float> progress)
    {
        // TODO: 여기에 실제 SDK 초기화 호출을 작성.
        // 예) var result = await SomeSdk.InitializeAsync(); ctx.Set(result);

        // 아래는 진행률 연출용 더미 로직 — 실제 구현에서는 SDK 콜백/진행률로 교체.
        float elapsed = 0f;
        while (elapsed < fakeDurationSeconds)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ctx.CancellationToken);
            elapsed += Time.unscaledDeltaTime;
            progress.Report(fakeDurationSeconds <= 0f ? 1f : elapsed / fakeDurationSeconds);
        }

        progress.Report(1f);
    }
}
