using UnityEngine;

/// <summary>
/// 리지드바디 기반 이동. 이동 방향은 InputController 이벤트로 받고, 속도는 스탯에서 가져온다.
/// 감지는 Update, 적용은 FixedUpdate(=FixedTick) 라 방향을 캐싱해서 사용.
/// </summary>
public class PlayerMovement
{
    private Rigidbody _rb;
    private PlayerStatusHandler _status;
    private PlayerInputController _input;
    private Vector3 _moveDir;

    public void Initialize(Player player)
    {
        _rb = player.Rigidbody;
        _status = player.Status;
        _input = player.Input;
        _input.OnMoveInput += OnMove;
    }

    private void OnMove(Vector3 dir) => _moveDir = dir;

    public void FixedTick()
    {
        if (_moveDir.sqrMagnitude < 0.0001f) return;
        Vector3 delta = _moveDir * (_status.MoveSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(_rb.position + delta);
    }

    public void Dispose()
    {
        if (_input != null) _input.OnMoveInput -= OnMove;
    }
}
