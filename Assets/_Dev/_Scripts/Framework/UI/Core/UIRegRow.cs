/// <summary>
/// 생성된 UI_REGISTRY 의 한 행. 키 + 소속 + 프리로드/조건 메타를 담는다.
/// AddressableKeyGenerator 가 프리팹의 UIMetaTag 를 읽어 채운다.
/// </summary>
public readonly struct UIRegRow
{
    public readonly UIKey key;
    public readonly CanvasGroupType owner;
    public readonly bool alwaysSpawn;   // 프리로드(생성+Initialize)
    public readonly bool openOnStart;   // 프리로드 후 시작 시 오픈
    public readonly ContentType requireContent;

    public UIRegRow(UIKey key, CanvasGroupType owner, bool alwaysSpawn, bool openOnStart, ContentType requireContent)
    {
        this.key = key;
        this.owner = owner;
        this.alwaysSpawn = alwaysSpawn;
        this.openOnStart = openOnStart;
        this.requireContent = requireContent;
    }
}
