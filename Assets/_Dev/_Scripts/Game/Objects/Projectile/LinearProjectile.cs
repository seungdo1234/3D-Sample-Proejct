using UnityEngine;

/// <summary>
/// 직선 등속 투사체. 발사 방향으로 일정 속도로 직진한다.
/// 포물선 등 다른 궤적은 BaseProjectile 을 상속해 Integrate 만 다르게 구현한다.
/// </summary>
public class LinearProjectile : BaseProjectile
{
    private Vector3 _velocity;

    protected override void OnFired(Vector3 direction) => _velocity = direction * speed;

    protected override Vector3 Integrate(float dt) => _position + _velocity * dt;
}
