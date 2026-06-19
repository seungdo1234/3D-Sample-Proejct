/// <summary>
/// UI 전용 식별자. string 과 구분되는 별도 타입이라
/// OnOpenUIEvent 등에는 UIKeys.* (생성된 UI 키)만 넘길 수 있다.
/// (일반 리소스 KEY.* 문자열은 UIKey 자리에 들어가지 않음 → enum 의 닫힌 집합 안전성 유지)
/// </summary>
public readonly struct UIKey
{
    public readonly string Value;
    public UIKey(string value) => Value = value;

    // AddressableManager.InstantiateAsync(string) 등에 그대로 전달되도록 단방향 변환만 제공
    public static implicit operator string(UIKey key) => key.Value;

    public override string ToString() => Value;
}
