using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 부팅 시 초기화가 필요한 영구 객체(SDK 초기화자, 매니저 등)가 구현한다.
/// 객체 "생성"은 Bootstrapper 가 라벨로 프리팹을 Instantiate 할 때 끝나고,
/// 이 InitializeAsync 는 Phase·Order 순서로 그 뒤에 호출된다 (생성/초기화 분리).
/// </summary>
public interface IBootInitializable
{
    InitPhase Phase { get; }

    // 같은 Phase 내 세부 순서. 같은 값끼리는 병렬 실행된다.
    int Order { get; }

    // 프로그래스 가중치. 오래 걸리는 것일수록 크게 준다 (기본 1).
    float Weight { get; }

    // true 면 실패 시(재시도 소진 후) 부팅을 중단한다. false 면 로그만 남기고 건너뛴다.
    bool Required { get; }

    // 실패 시 재시도 횟수 (0 = 재시도 안 함).
    int MaxRetry { get; }

    // 로그에 표시할 이름.
    string DisplayName { get; }

    // 실제 초기화 본문. progress 로 내부 진행률(0~1)을 보고한다.
    UniTask InitializeAsync(InitContext ctx, IProgress<float> progress);
}
