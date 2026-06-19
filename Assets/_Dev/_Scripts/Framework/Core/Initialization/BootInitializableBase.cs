using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MonoBehaviour 기반 부팅 초기화 베이스. 영구 프리팹(SDK/매니저)의 컴포넌트에 붙여
/// 인스펙터에서 Phase·Order·가중치·실패정책을 구성한다.
/// </summary>
public abstract class BootInitializableBase : MonoBehaviour, IBootInitializable
{
    [Header("# Boot Init")]
    [SerializeField] private InitPhase phase = InitPhase.Managers;
    [SerializeField] private int order = 0;
    [SerializeField] private float weight = 1f;
    [SerializeField] private bool required = true;
    [SerializeField] private int maxRetry = 0;
    [Tooltip("비우면 클래스 이름을 사용")]
    [SerializeField] private string displayName;

    public InitPhase Phase => phase;
    public int Order => order;
    public float Weight => weight;
    public bool Required => required;
    public int MaxRetry => maxRetry;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? GetType().Name : displayName;

    public abstract UniTask InitializeAsync(InitContext ctx, IProgress<float> progress);
}
