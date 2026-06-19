using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_GameResult : PopupBase<MainGame>
{
    [Header("# Game Result")]
    [SerializeField] private TextMeshProUGUI killValueTxt;
    [SerializeField] private Button lobbyBtn;

    // 최초 1회 초기화 (리스너 등록 등)
    protected override void InitializeChild(MainGame game)
    {
        if (lobbyBtn != null)
            lobbyBtn.onClick.AddListener(OnClickLobby);
    }

    // 열기 시 콘텐츠 갱신 (게임오버 시점의 최종 킬 수 바인딩)
    protected override void OnOpenContent()
    {
        // SetText(format, int) 오버로드는 string 할당 없음(GC 0)
        killValueTxt.SetText("Kill: {0}", game.FinalKillCount);
    }

    protected override void OnCloseContent()
    {

    }

    // 로비로 복귀 — 트랜지션과 함께 Lobby 씬으로 이동
    private void OnClickLobby()
    {
        LoadSceneManager.Instance.LoadSceneWithTransition(SceneType.Lobby);
    }
}
