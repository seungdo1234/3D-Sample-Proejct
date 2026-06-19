using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class GameInitializer<T> : MonoBehaviour, IGameInitializable
    where T : GameBase
{
    [SerializeField] private int initOrder = 0;
    public int InitOrder => initOrder;

    protected T Game { get; private set; }

    public async UniTask InGameInitialize(GameBase game)
    {
        Game = game as T;
        if (Game == null)
        {
            DebugUtil.DevelopmentLog($"[{name}] 게임 타입 불일치 — 기대: {typeof(T).Name}", DebugUtil.LogType.Warning);
            return;
        }
        await OnInitialize(Game);
    }

    protected abstract UniTask OnInitialize(T game);
}
