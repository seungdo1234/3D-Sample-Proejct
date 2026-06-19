using UnityEngine;

/// <summary>
/// UI 프리팹의 루트에 붙이는 메타 태그.
/// "이 UI는 어디 소속이고, 항상 띄우는지, 어떤 컨텐츠에서 풀리는지"를
/// 프리팹 자신이 들고 있다. AddressableKeyGenerator 가 Generate 시점에
/// 이 값을 읽어 UIKeys / UI_REGISTRY 로 베이크한다.
/// (= 조건의 집은 프리팹, 생성기는 수확기)
/// </summary>
[DisallowMultipleComponent]
public class UIMetaTag : MonoBehaviour
{
    [Tooltip("이 UI 가 속한 캔버스 그룹")]
    public CanvasGroupType owner;

    [Tooltip("시작 시 미리 생성 + Initialize 할지 (프리로드, 오픈은 안 함)")]
    public bool isAlwaysSpawn;

    [Tooltip("프리로드 후 시작과 동시에 오픈할지 (예: HUD). 끄면 OnOpenUIEvent 가 올 때 오픈)")]
    public bool openOnStart;

    [Tooltip("이 UI 가 풀리는 컨텐츠 조건 (None 이면 조건 없음)")]
    public ContentType requireContent = ContentType.None;
}
