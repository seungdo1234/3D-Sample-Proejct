using UnityEngine;
using UnityEngine.UI;

public class UI_Lobby : UIBase<LobbyGame>
{
    [Header("# Lobby")]
    [SerializeField] private Button gameStartBtn;

    protected override void InitializeGame(LobbyGame game)
    {
        if (gameStartBtn != null)
            gameStartBtn.onClick.AddListener(OnClickGameStart);
    }

    // 게임 시작 → 트랜지션과 함께 Game 씬으로 이동
    private void OnClickGameStart()
        => LoadSceneManager.Instance.LoadSceneWithTransition(SceneType.Game);
}
    