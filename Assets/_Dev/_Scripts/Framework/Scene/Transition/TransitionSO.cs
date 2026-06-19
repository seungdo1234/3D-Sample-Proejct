using UnityEngine;

[CreateAssetMenu(fileName = "New TransitionSO", menuName = "Transition/new TransitionSO")]
public class TransitionSO : ScriptableObject
{
    [Header("# Transition Base Settings")]
    public Vector2 referenceResolution = new Vector2(1920, 1080);
    public bool isBlockedRaycast = true;
    
    [Space(10)]
    // 트랜지션 속도
    public float transitionSpeed = 1;

    [Space(10)]
    // 트랜지션 닫히고 열릴때까지의 최소 딜레이 타임
    public float transitionMinimumDelayTime = 0.5f;
    
    [Header("# Transition Prefabs")]
    [Space(10)]
    public GameObject transitionInPrefab;
    public GameObject transitionOutPrefab;
}
