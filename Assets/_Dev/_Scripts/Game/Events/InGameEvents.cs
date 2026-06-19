// 인게임 전역 이벤트 모음

using UnityEngine;

/// <summary> 게임 시작 시점 (씬 오픈 후 MainGame 이 발행) </summary>
public struct OnGameStartEvent { }

/// <summary> 플레이어 스폰 완료 (EntitySpawnSystem 발행, 트랜스폼 전달) </summary>
public struct OnPlayerSpawnedEvent { public Transform Player; }

/// <summary> 적 처치 (Enemy 가 사망 확정 시 발행 — 이 게임은 가해자가 플레이어뿐이라 별도 정보 없음) </summary>
public struct OnEnemyKilledEvent { }

/// <summary> 킬 카운트 변경 (Player 가 누적 후 발행, HUD 갱신용) </summary>
public struct OnKillCountChangedEvent { public int Count; }

/// <summary> 플레이어 사망 = 게임오버 (Player 가 적과 충돌 시 1회 발행, 최종 킬 수 전달) </summary>
public struct OnPlayerDeadEvent { public int KillCount; }
