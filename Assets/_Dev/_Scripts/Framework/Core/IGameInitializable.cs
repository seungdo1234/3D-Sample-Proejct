using Cysharp.Threading.Tasks;

public interface IGameInitializable
{
    int InitOrder { get; }
    UniTask InGameInitialize(GameBase game);
}
