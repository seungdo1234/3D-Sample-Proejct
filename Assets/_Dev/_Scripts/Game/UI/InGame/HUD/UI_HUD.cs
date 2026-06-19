using TMPro;
using UnityEngine;

public class UI_HUD : UIBase<MainGame>
{
    [Header("# HUD")]
    [SerializeField] private TextMeshProUGUI killTxt;

    protected override void InitializeGame(MainGame game)
    {
        killTxt.SetText("Kill: {0}", 0); // 초기 표시
        EventManager.Instance.Subscribe<OnKillCountChangedEvent>(OnKillCountChanged);
    }

    // SetText(format, int) 오버로드는 내부 char 버퍼만 써서 string 할당 없음(GC 0)
    private void OnKillCountChanged(OnKillCountChangedEvent e) => killTxt.SetText("Kill: {0}", e.Count);

    private void OnDestroy()
    {
        if (EventManager.HasInstance) // 종료/씬 언로드 중 유령 싱글톤 생성 방지
            EventManager.Instance.Unsubscribe<OnKillCountChangedEvent>(OnKillCountChanged);
    }
}
