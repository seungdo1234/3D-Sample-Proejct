using UnityEngine;

/// <summary>
/// 적 연출 전담(로직 없음). 애니메이터 트리거(Run/Death)와 피격 플래시를 담당한다.
/// 플래시는 머터리얼 복제 없이 MaterialPropertyBlock 으로 _BaseColor(URP)만 덮어써 GC·배칭 손실이 없다.
/// 순수 C#, Enemy가 생성·초기화·Tick. Animator/Renderer는 자식 GO에서 런타임 자동 탐색. (PlayerVisual 대응)
/// </summary>
public class EnemyVisual
{
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Animator _animator;
    private Renderer[] _renderers;
    private Color[] _baseColors;
    private MaterialPropertyBlock _mpb;

    private Color _flashColor;
    private float _flashDuration;
    private float _flashTimer;

    public void Initialize(Enemy enemy, Color flashColor, float flashDuration)
    {
        _animator = enemy.GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogWarning("[EnemyVisual] 하위에 Animator가 없어 Run/Death 연출이 동작하지 않음", enemy);

        _flashColor = flashColor;
        _flashDuration = flashDuration;

        _renderers = enemy.GetComponentsInChildren<Renderer>();
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].sharedMaterial != null ? _renderers[i].sharedMaterial.GetColor(BaseColorId) : Color.white;
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary> 활성화(스폰) 시 — Run 트리거 </summary>
    public void PlayRun()
    {
        if (_animator == null) return;
        _animator.ResetTrigger(DeathHash);
        _animator.SetTrigger(RunHash);
    }

    /// <summary> 사망 시 — Death 트리거 </summary>
    public void PlayDeath()
    {
        if (_animator == null) return;
        _animator.ResetTrigger(RunHash);
        _animator.SetTrigger(DeathHash);
    }

    /// <summary> 피격 플래시 시작 </summary>
    public void Flash() => _flashTimer = _flashDuration;

    /// <summary> 풀 재사용 시 원색 복원 </summary>
    public void ResetVisual()
    {
        _flashTimer = 0f;
        ApplyFlashColor(0f);
    }

    public void Tick()
    {
        if (_flashTimer <= 0f) return;
        _flashTimer -= Time.deltaTime;
        ApplyFlashColor(Mathf.Clamp01(_flashTimer / _flashDuration));
    }

    // t=1 완전 플래시색 → t=0 원색
    private void ApplyFlashColor(float t)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, Color.Lerp(_baseColors[i], _flashColor, t));
            _renderers[i].SetPropertyBlock(_mpb);
        }
    }
}
