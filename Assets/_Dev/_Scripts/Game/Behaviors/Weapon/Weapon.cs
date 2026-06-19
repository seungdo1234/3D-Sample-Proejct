using UnityEngine;

/// <summary>
/// 씬에 배치되는 무기(총 GO에 부착). 총구 정보와 씬 의존 요소(에임 라인, 추후 VFX/사운드)를 보유.
/// 로직은 Hand가 구동하고, 여기선 UpdateAimLine()/Fire() 호출만 받는다. 스탯은 일단 여기 두고 추후 SO로 분리 가능.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("References")]
    [Tooltip("총구 끝(위치만 사용 — 발사 방향은 몸통 정면)")]
    [SerializeField] private Transform muzzle;
    [Tooltip("비워두면 런타임에 자동 생성")]
    [SerializeField] private LineRenderer aimLine;
    [Tooltip("발사 시 재생할 머즐 플래시 파티클")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Aim Line")]
    [SerializeField] private float aimRange = 50f;
    [Tooltip("에임 레이를 막는 레이어")]
    [SerializeField] private LayerMask aimBlockMask = ~0;
    [SerializeField] private Color aimColor = Color.red;
    [Tooltip("총구 쪽(시작) 굵기")]
    [SerializeField] private float aimStartWidth = 0.035f;
    [Tooltip("끝(충돌 지점) 굵기 — 끝으로 갈수록 가늘어지는 빔 느낌")]
    [SerializeField] private float aimEndWidth = 0.012f;
    [Tooltip("총구 쪽 불투명도(0~1)")]
    [Range(0f, 1f)][SerializeField] private float aimStartAlpha = 0.85f;
    [Tooltip("끝 쪽 불투명도(0~1) — 낮을수록 멀리서 흐려짐")]
    [Range(0f, 1f)][SerializeField] private float aimEndAlpha = 0.12f;

    [Header("Impact Dot")]
    [Tooltip("벽/적에 닿는 지점에 표시할 닷")]
    [SerializeField] private Color impactColor = new Color(1f, 0.25f, 0.15f, 1f);
    [SerializeField] private float impactSize = 0.18f;

    [Header("Projectile")]
    [Tooltip("발사할 투사체의 어드레서블/풀 키")]
    [SerializeField] private string projectileKey = "Projectile";
    [Tooltip("발사 확산 반각(도). 0이면 정확히 에임 방향. 중앙 가중 랜덤으로 부채꼴")]
    [SerializeField] private float spreadAngle = 3f;

    [Header("Stats")]
    [Tooltip("초당 발사 수 (연사 속도)")]
    [SerializeField] private float fireRate = 8f;

    public Transform Muzzle => muzzle;

    /// <summary>발사 간 간격(초). fireRate가 0 이하면 사실상 발사 불가.</summary>
    public float FireInterval => fireRate > 0f ? 1f / fireRate : float.MaxValue;

    private Transform _impactDot;
    private static Texture2D _dotTexture; // 절차적 생성 소프트 닷, 인스턴스 간 공유

    private void Awake()
    {
        if (aimLine == null) aimLine = CreateAimLine();
        _impactDot = CreateImpactDot();
    }

    private void OnDestroy()
    {
        // 닷은 부모가 없어 자동 파괴되지 않으므로 직접 정리
        if (_impactDot != null) Destroy(_impactDot.gameObject);
    }

    /// <summary>
    /// 총구 차폐 판정: 몸통(bodyRef)~총구 사이가 벽/적으로 막혔는가.
    /// 총구는 몸통보다 앞에 있어, 벽에 붙으면 총구가 벽 너머로 나갈 수 있다. 이때 총구에서
    /// 쏜 총알은 벽 너머에서 출발해 벽을 무시하고, 에임도 벽 뒤(총구 위치)에 그려진다.
    /// 그래서 이 간극이 막히면 총알을 날리지 않고 에임도 숨긴다.
    /// 검사는 forward가 아니라 '몸통→총구' 방향으로 한다 — 총구가 비스듬히/모서리로 벽을
    /// 넘어가는 경우까지 정확히 잡기 위함(forward로 쏘면 그 벽을 놓친다). (막는 대상 = aimBlockMask)
    /// </summary>
    private bool IsMuzzleBlocked(Vector3 bodyRef, out RaycastHit hit)
    {
        hit = default;
        if (muzzle == null) return false;
        Vector3 toMuzzle = muzzle.position - bodyRef;
        float dist = toMuzzle.magnitude;
        return dist > 1e-4f &&
               Physics.Raycast(bodyRef, toMuzzle / dist, out hit, dist, aimBlockMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>지정한 몸통 기준점에서 직선 에임을 그림. 첫 충돌 지점까지만. (방향은 Hand가 결정)</summary>
    public void UpdateAimLine(Vector3 bodyRef, Vector3 dir)
    {
        if (aimLine == null) return;

        // 총구가 벽 너머로 나간 상태: 에임을 그리면 벽 뒤에 떠 어색하므로 아예 표시하지 않는다.
        if (IsMuzzleBlocked(bodyRef, out _))
        {
            aimLine.enabled = false;
            if (_impactDot != null) _impactDot.gameObject.SetActive(false);
            return;
        }
        aimLine.enabled = true;

        // 원점은 항상 총구. 총알·에임은 총에서 나간다.
        Vector3 origin = muzzle != null ? muzzle.position : bodyRef;
        bool hasHit = Physics.Raycast(origin, dir, out RaycastHit hit, aimRange, aimBlockMask, QueryTriggerInteraction.Ignore);
        Vector3 end = hasHit ? hit.point : origin + dir * aimRange;

        aimLine.SetPosition(0, origin);
        aimLine.SetPosition(1, end);

        // 충돌한 지점에만 임팩트 닷 표시 (표면에 살짝 띄워 Z-파이팅 방지)
        if (_impactDot == null) return;
        if (hasHit)
        {
            _impactDot.gameObject.SetActive(true);
            _impactDot.position = hit.point + hit.normal * 0.01f;
            _impactDot.rotation = Quaternion.LookRotation(hit.normal); // Sprites/Default는 양면 → 방향 무관하게 보임
            _impactDot.localScale = Vector3.one * impactSize;
        }
        else
        {
            _impactDot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 발사. 머즐 플래시 + 총구에서 투사체 스폰(에임 방향에 확산 적용). TODO: 사운드
    ///
    /// 총구는 몸통보다 앞에 있어, 적/벽이 몸통~총구 간극에 들어온 초근접 상황에선 총구에서
    /// 스폰한 총알이 그 대상을 지나쳐(앞에서 출발) 뚫고 나간다. 이땐 총알을 날리지 않는다:
    /// 적이면 그 자리에서 즉시 직격(붙으면 바로 데미지), 벽이면 연출(머즐 플래시)만 낸다.
    /// </summary>
    public void Fire(Vector3 bodyRef, Vector3 aimDir)
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzle == null) return;

        Vector3 dir = ApplySpread(aimDir);

        // 총구가 벽/적 너머로 나간 상태: 총구 스폰 총알은 그 대상을 뚫으므로 총알을 날리지 않는다.
        if (IsMuzzleBlocked(bodyRef, out RaycastHit gapHit))
        {
            // 적 → 명중점에서 즉시 직격. 벽(피해 대상 아님) → 연출만, 데미지/총알 없음.
            var go = gapHit.rigidbody != null ? gapHit.rigidbody.gameObject : gapHit.collider.gameObject;
            if (go.TryGetComponent(out IDamageable _))
            {
                var hitShot = AddressablePoolManager.Instance.Spawn<BaseProjectile>(projectileKey);
                if (hitShot != null) hitShot.HitInstant(gapHit, dir);
            }
            return;
        }

        var projectile = AddressablePoolManager.Instance.Spawn<BaseProjectile>(projectileKey);
        if (projectile != null) projectile.Fire(muzzle.position, dir);
    }

    // 에임 방향을 수평면에서 ±spreadAngle 안으로 랜덤하게 틀어 부채꼴 생성(중앙 가중 → 보통은 에임 근처).
    private Vector3 ApplySpread(Vector3 aimDir)
    {
        if (spreadAngle <= 0f) return aimDir;
        float t = (Random.value + Random.value) * 0.5f; // 삼각분포: 0.5(중앙)에 더 자주 모임
        float angle = (t * 2f - 1f) * spreadAngle;       // -반각 ~ +반각
        return Quaternion.AngleAxis(angle, Vector3.up) * aimDir;
    }

    // 에임 라인용 LineRenderer 자동 생성 (총 GO 자식)
    private LineRenderer CreateAimLine()
    {
        var go = new GameObject("AimLine");
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.numCapVertices = 4; // 끝을 둥글게 — 빔 느낌
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default")); // 정점 색 반영(알파 블렌드)

        // 총구는 굵게 → 끝은 가늘게 (빔이 뻗어나가는 느낌)
        lr.widthMultiplier = 1f;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, aimStartWidth), new Keyframe(1f, aimEndWidth));

        // 총구는 진하게 → 끝은 흐리게 (FPS 레이저 사이트 느낌)
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(aimColor, 0f), new GradientColorKey(aimColor, 1f) },
            new[] { new GradientAlphaKey(aimStartAlpha, 0f), new GradientAlphaKey(aimEndAlpha, 1f) });
        lr.colorGradient = grad;
        return lr;
    }

    // 충돌 지점 표시용 닷 생성 (절차적 소프트 원 텍스처를 입힌 쿼드)
    private Transform CreateImpactDot()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ImpactDot";
        Destroy(go.GetComponent<Collider>()); // 콜라이더 불필요
        // 반동 애니메이션을 받는 총의 자식으로 두면 부모를 따라 들리므로, 부모 없이 두고 월드 위치만 직접 갱신
        go.transform.SetParent(null, false);

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var mat = new Material(Shader.Find("Sprites/Default")); // 양면 + 알파 블렌드
        mat.mainTexture = GetDotTexture();
        mat.color = impactColor;
        mr.material = mat;

        go.SetActive(false); // 충돌 시에만 표시
        return go.transform;
    }

    // 가운데가 진하고 가장자리로 부드럽게 사라지는 원형 텍스처를 코드로 생성
    private static Texture2D GetDotTexture()
    {
        if (_dotTexture != null) return _dotTexture;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - dist);
                a *= a; // 가장자리를 더 부드럽게
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _dotTexture = tex;
        return tex;
    }
}
