/// <summary>
/// 부팅 초기화 단계. 값이 작을수록 먼저 초기화된다.
/// (객체 "생성"은 이 단계 이전에 일괄로 끝나므로 여기엔 없다 — 생성/초기화 분리)
/// 같은 Phase 안에서는 IBootInitializable.Order 로 세부 순서를 잡는다.
/// </summary>
public enum InitPhase
{
    // SDK 초기화 (광고/애널리틱스/인증 등). 매니저보다 먼저 끝나야 함.
    Sdk = 0,

    // 매니저 초기화. SDK 결과(InitContext)에 의존할 수 있어 SDK 이후에 실행.
    Managers = 100,

    // 공용 리소스 프리로드 등 마무리 작업 (선택).
    Post = 200,
}

/// <summary>
/// 로딩 화면에 표시할 단계별 라벨. 프로젝트마다 문구를 바꾸려면 여기만 수정한다.
/// </summary>
public static class InitPhaseExtensions
{
    public static string ToLoadingLabel(this InitPhase phase) => phase switch
    {
        InitPhase.Sdk      => "SDK Loading...",
        InitPhase.Managers => "System Loading...",
        InitPhase.Post     => "Finalizing...",
        _                  => "Loading...",
    };
}
