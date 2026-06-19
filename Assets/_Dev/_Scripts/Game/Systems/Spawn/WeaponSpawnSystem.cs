using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 무기/전투 관련 풀링 리소스 담당.
/// Projectile: 풀에 등록만 (소환은 발사 시 Weapon.Fire 에서).
/// 투척물·탄피 등 전투 리소스가 늘면 여기에 함께 등록한다.
/// </summary>
public class WeaponSpawnSystem : GameInitializer<MainGame>
{
    [Header("# Keys")]
    [SerializeField] private string projectileKey = "Projectile";

    [Header("# Projectile Pool")]
    [SerializeField]
    private AddressablePoolManager.PoolSettings projectilePoolSettings = new()
    {
        initialSize = 30, spawnSize = 15, maxSize = 300
    };

    protected override async UniTask OnInitialize(MainGame game)
    {
        // Projectile: 풀 등록만 (소환 X)
        await AddressablePoolManager.Instance.RegisterPool(projectileKey, projectilePoolSettings);
    }
}
