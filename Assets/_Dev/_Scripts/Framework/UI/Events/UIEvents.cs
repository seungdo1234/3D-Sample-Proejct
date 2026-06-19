/// <summary>
/// UI 를 여는 제네릭 이벤트. enum 대신 생성된 UIKey 를 운반한다.
/// 예: EventManager.Instance.Publish(new OnOpenUIEvent(UIKeys.UI_HUD));
/// </summary>
public struct OnOpenUIEvent
{
    public UIKey key;
    public OnOpenUIEvent(UIKey key) => this.key = key;
}
