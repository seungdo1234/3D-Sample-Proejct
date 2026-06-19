using UnityEngine;
using UnityEngine.UI;

public class TransitionCanvas : MonoBehaviour
{
    [SerializeField] private TransitionSO transitionSO;

    public float TransitionMinimumDelayTime => transitionSO.transitionMinimumDelayTime;
    
    private GameObject transitionInObject;
    private GameObject transitionOutObject;
    
    // [추가] 로딩 중 화면을 덮어줄 든든한 검은 배경
    private Image _blackBackdrop;

    private Animator _inAnimator;
    private Animator _outAnimator;
    
    private CanvasScaler canvasScaler;
    private GraphicRaycaster graphicRaycaster;
    private bool isInitialized = false;

    private void InitializeTransition()
    {
        if(isInitialized) return;
        isInitialized = true;
        
        // [1] 검은 배경(Backdrop) 동적 생성
        GameObject bgObj = new GameObject("StaticBlackBackdrop");
        bgObj.transform.SetParent(transform, false);
        
        _blackBackdrop = bgObj.AddComponent<Image>();
        _blackBackdrop.color = Color.black;
        _blackBackdrop.raycastTarget = true; // 터치 막기용

        // 꽉 채우기 (Stretch-Stretch)
        RectTransform rt = _blackBackdrop.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // 기본은 꺼둠
        _blackBackdrop.gameObject.SetActive(false);

        // 2. 프리팹 생성 및 Animator 캐싱
        if (transitionSO.transitionInPrefab)
        {
            transitionInObject = Instantiate(transitionSO.transitionInPrefab, transform);
            transitionInObject.SetActive(false);
            _inAnimator = transitionInObject.GetComponentInChildren<Animator>();
        }
        
        if (transitionSO.transitionOutPrefab)
        {
            transitionOutObject = Instantiate(transitionSO.transitionOutPrefab, transform);
            transitionOutObject.SetActive(false);
            _outAnimator = transitionOutObject.GetComponentInChildren<Animator>();
        }

        // 순서 정리: 배경이 제일 뒤, 그 위에 애니메이션
        if(_blackBackdrop) _blackBackdrop.transform.SetAsFirstSibling();
        
        // 3. 캔버스 설정
        canvasScaler = GetComponent<CanvasScaler>();
        // TODO:
        // if (canvasScaler)
        // {
        //     if (GameManager_SD.Instance != null && GameManager_SD.Instance.ResolutionSystem != null)
        //         GameManager_SD.Instance.ResolutionSystem.RegisterCanvasScaler(canvasScaler, true);
        // }
        
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        if (graphicRaycaster)
            graphicRaycaster.enabled = transitionSO.isBlockedRaycast;
    }

    public float TransitionIn()
    {
        InitializeTransition();
        if (transitionInObject == null) return 0f;

        // 시작할 때 정리
        if(_blackBackdrop) _blackBackdrop.gameObject.SetActive(false);
        if(transitionOutObject) transitionOutObject.SetActive(false);
        
        transitionInObject.SetActive(true);
        return PlayAnimation(_inAnimator);
    }

    /// <summary>
    /// [핵심] In 애니메이션이 끝나면 호출. 
    /// 애니메이션 오브젝트 대신 이 static 이미지가 화면을 가립니다.
    /// </summary>
    public void EnableBlackBackground()
    {
        InitializeTransition();
        if (_blackBackdrop != null)
        {
            _blackBackdrop.gameObject.SetActive(true);
            
            // In 오브젝트는 이제 임무를 다했으니 끕니다. (투명해지는 버그 원천 차단)
            if(transitionInObject) transitionInObject.SetActive(false);
        }
    }

    public float TransitionOut()
    {
        InitializeTransition();
        if (transitionOutObject == null) return 0f;

        // 1. Out 오브젝트(시작이 검정색)를 먼저 켭니다.
        transitionOutObject.SetActive(true);
        
        // 2. 이제 뒤에 받쳐주던 Static 배경은 필요 없으니 끕니다.
        if (_blackBackdrop) _blackBackdrop.gameObject.SetActive(false);

        return PlayAnimation(_outAnimator);
    }

    private float PlayAnimation(Animator anim)
    {
        if (anim == null) return 0f;

        // timeScale=0(게임오버 정지 등)에서도 커튼이 재생되도록 비스케일 시간으로 구동
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.speed = transitionSO.transitionSpeed;
        anim.Rebind();
        anim.Update(0f);

        var info = anim.GetCurrentAnimatorStateInfo(0);
        float speed = transitionSO.transitionSpeed <= 0 ? 1 : transitionSO.transitionSpeed;
        return info.length / speed;
    }

    public void TransitionOff()
    {
        if (_blackBackdrop) _blackBackdrop.gameObject.SetActive(false);
        if (transitionInObject) transitionInObject.SetActive(false);
        if (transitionOutObject) transitionOutObject.SetActive(false);
    }
}