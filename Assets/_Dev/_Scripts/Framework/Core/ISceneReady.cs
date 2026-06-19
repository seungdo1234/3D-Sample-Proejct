using Cysharp.Threading.Tasks;

public interface ISceneReady
{
    UniTask WaitForInitialization();
    void StartScene();
}
