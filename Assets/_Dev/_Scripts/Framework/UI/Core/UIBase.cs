using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Base for all UI. The canvas controller initializes it via InGameInitialize and
/// controls visibility via OpenUI/CloseUI. Visibility is owned by this base
/// (SetActive), so subclasses only add behavior through OnOpen/OnClose.
/// Implements IGameInitializable only to reuse the init entry point; InitOrder is unused.
/// </summary>
public abstract class UIBase<T> : MonoBehaviour, IGameInitializable where T : GameBase
{
    public int InitOrder => 0;

    protected T game;

    public UniTask InGameInitialize(GameBase gameBase)
    {
        game = gameBase as T;
        if (game != null) InitializeGame(game);
        return UniTask.CompletedTask;
    }

    protected abstract void InitializeGame(T game);

    public virtual void OpenUI()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        OnOpen();
    }

    public virtual void CloseUI()
    {
        OnClose();
        gameObject.SetActive(false);
    }

    // Subclass hooks (optional)
    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
}
