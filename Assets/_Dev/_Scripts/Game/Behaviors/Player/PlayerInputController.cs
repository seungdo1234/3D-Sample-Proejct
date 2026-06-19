using System;
using UnityEngine;

/// <summary>
/// 입력 감지 전담. 감지만 하고 방향을 로컬 이벤트로 발행한다 (Player.Update 가 Tick 호출).
/// </summary>
public class PlayerInputController
{
    public event Action<Vector3> OnMoveInput; // 이동 방향 (월드 xz)
    public event Action<Vector3> OnLookInput; // 마우스가 가리키는 월드 지점
    public event Action OnFirePressed; // 좌클릭 누른 순간 (즉발 + 연사 시작)
    public event Action OnFireReleased; // 좌클릭 뗀 순간 (연사 종료)

    private Transform _playerTr;
    private Camera _camera;
    private float _muzzleHeight; // 정지 포즈 기준 총구 높이 오프셋(플레이어 기준). 달릴 때 스쿼시로 총구 Y가 떨려도 평면이 안 흔들리게 고정값 사용.
    private Vector3 _lastMousePos; // 직전 스크린 마우스 좌표 (안 바뀌면 재투영 생략)
    private bool _hasMouseSample;

    public void Initialize(Player player)
    {
        _playerTr = player.transform;
        _camera = Camera.main;
        Transform muzzle = player.Weapon != null ? player.Weapon.Muzzle : null;
        _muzzleHeight = muzzle != null ? muzzle.position.y - _playerTr.position.y : 0f;
    }

    public void Tick()
    {
#if !UNITY_ANDROID 
        // 이동 (레거시 Input)
        Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        OnMoveInput?.Invoke(move.sqrMagnitude > 1f ? move.normalized : move); // 없으면 zero

        // 발사 (좌클릭 누름/뗌 — 연사는 Hand가 타이머로 구동)
        if (Input.GetMouseButtonDown(0))
            OnFirePressed?.Invoke();
        if (Input.GetMouseButtonUp(0))
            OnFireReleased?.Invoke();
  #endif
    }

    // 조준 투영은 LateUpdate에서 (카메라가 플레이어를 따라간 뒤). Update에서 하면
    // 카메라가 아직 과거 위치라 정지 커서가 매 프레임 다른 월드점으로 투영돼 에임이 떨린다.
    public void LookTick()
    {
        // 회전 (마우스 → 총구 높이 평면 교차점)
        // 기울어진 카메라에서는 평면 높이가 다르면 화면 투영 위치가 어긋난다.
        // 에임 라인/총알이 그려지는 총구 높이에 평면을 맞춰야 커서와 정확히 일치.
        if (_camera == null) return;

        // 커서가 화면에서 안 움직였으면 조준 방향도 그대로(카메라가 따라오므로 상대 방향 불변).
        // 정지 커서에서 매 프레임 재투영하며 생기던 미세 떨림을 원천 차단. 마우스 좌표는 정수 픽셀이라 동치 비교로 충분.
        Vector3 mousePos = Input.mousePosition;
        if (_hasMouseSample && mousePos == _lastMousePos) return;
        _lastMousePos = mousePos;
        _hasMouseSample = true;

        Ray ray = _camera.ScreenPointToRay(mousePos);
        float planeY = _playerTr.position.y + _muzzleHeight; // 애니메이션 흔들림 배제한 안정 높이
        Plane ground = new Plane(Vector3.up, new Vector3(_playerTr.position.x, planeY, _playerTr.position.z));
        if (ground.Raycast(ray, out float enter))
            OnLookInput?.Invoke(ray.GetPoint(enter));
    }

    public void Dispose()
    {
        OnMoveInput = null;
        OnLookInput = null;
        OnFirePressed = null;
        OnFireReleased = null;
    }
}
