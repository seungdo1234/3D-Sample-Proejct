using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class GameResolutionSystem : MonoBehaviour
{
    // [설계 해상도] 예: 1080 x 1920 (9:16)
    [SerializeField] private Vector2Int baseResolution = new Vector2Int(720, 1280);

    // 실제 적용된 해상도 (참고용)
    [SerializeField] private Vector2Int currentScreenSize;
    public Vector2Int CurrentScreenSize => currentScreenSize;
    public Vector2 BaseScreenSize => baseResolution;
    
    // 현재 기기가 설계 비율보다 넓은가? (태블릿/폴드 펼침 상태)
    public bool IsWideRatio { get; private set; }
    public float BaseScreenAspect { get; private set; }
    public float CurrentScreenAspect { get; private set; }
    
    /// <summary>
    /// 기준 해상도 대비 현재 해상도의 스케일 비율
    /// (CanvasScaler의 Match 설정과 동일한 로직)
    /// </summary>
    public float ResolutionScale => IsWideRatio 
        ? (float)currentScreenSize.y / baseResolution.y 
        : (float)currentScreenSize.x / baseResolution.x;
    
    /// <summary>
    /// 입력받은 오프셋을 현재 해상도 스케일에 맞춰 반환
    /// </summary>
    public float GetScaledOffset(float offset) => offset * ResolutionScale;
    public Vector2 GetScaledOffset(Vector2 offset) => offset * ResolutionScale;
    public Vector3 GetScaledOffset(Vector3 offset) => offset * ResolutionScale;

    private List<CanvasScaler> sceneCanvasScalerList = new List<CanvasScaler>();
    private List<CanvasScaler> globalCanvasScalerList = new List<CanvasScaler>();
    private List<SetSafeArea> sceneSafeAreaList = new List<SetSafeArea>();
    private List<SetSafeArea> globalSafeAreaList = new List<SetSafeArea>();
    
    // 해상도 변경 감지용 변수
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    private void Start()
    {
        // 초기 설정
        CheckAndApplyResolution(true);
  
        // TODO
        // LoadSceneManager.Instance.OnBeforeMoveSceneEvent += () =>
        // {
        //     sceneCanvasScalerList?.Clear();
        //     sceneSafeAreaList?.Clear();
        // };
    }

    private void Update()
    {
        // [핵심] 런타임 중 해상도 변경 감지 (폴드 대응의 핵심)
        // 안드로이드 이벤트가 가끔 씹히거나, 해상도 갱신 타이밍이 늦을 수 있어 Update에서 감시
        if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
        {
            // 해상도가 변하고 있다면 안정화될 때까지 잠시 대기 후 적용 (코루틴 호출)
            if (_changeResolutionCoroutine != null) StopCoroutine(_changeResolutionCoroutine);
            _changeResolutionCoroutine = StartCoroutine(CoWaitAndApplyResolution());
            
            // 즉시 값 갱신하여 중복 호출 방지
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }
    }

    private Coroutine _changeResolutionCoroutine;
    
    private IEnumerator CoWaitAndApplyResolution()
    {
        // 폴드 애니메이션 등으로 인해 해상도 값이 튀는 것을 방지하기 위해
        // 값이 변한 뒤 잠시 대기 (약 0.5초 혹은 프레임 대기)
        // 화면 회전/폴딩 애니메이션이 보통 0.3~0.5초 정도 걸립니다.
        yield return new WaitForSeconds(0.5f);

        // 확실한 갱신을 위해 프레임 끝까지 대기
        yield return new WaitForEndOfFrame();

        CheckAndApplyResolution();

        // TODO
        // 런타임 해상도 변경(폴드/회전) 시에만 발행 — 초기화는 OpenSceneEvent에서 처리
        // EventManager.Instance.Publish(new OnResolutionChangedEvent());
    }

    /// <summary>
    /// 현재 해상도와 설계 해상도를 비교하여 설정 적용
    /// </summary>
    /// <param name="force">강제 적용 여부</param>
    private void CheckAndApplyResolution(bool force = false)
    {
        currentScreenSize = new Vector2Int(Screen.width, Screen.height);
        
        // 1. 설계 비율 (예: 1080/1920 = 0.5625)
        BaseScreenAspect = (float)baseResolution.x / baseResolution.y;
        
        // 2. 현재 기기 비율 (예: 폴드 펼침 1768/2208 = 0.8)
        CurrentScreenAspect = (float)currentScreenSize.x / currentScreenSize.y;

        // 3. 비율 비교
        // 현재 화면이 설계보다 더 '뚱뚱(Wide)'하면 -> Height 기준 (좌우 여백 생기거나 UI 확장)
        // 현재 화면이 설계보다 더 '홀쭉(Tall)'하면 -> Width 기준 (위아래 여백 생기거나 UI 확장)
        IsWideRatio = CurrentScreenAspect >= BaseScreenAspect;

        // 4. 스케일러 적용
        SetAllCanvasScaler(globalCanvasScalerList);
        SetAllCanvasScaler(sceneCanvasScalerList);

        // 5. SafeArea 재적용
        RefreshAllSafeArea(globalSafeAreaList);
        RefreshAllSafeArea(sceneSafeAreaList);

        DebugUtil.DevelopmentLog($"[Resolution] Changed to {currentScreenSize.x}x{currentScreenSize.y} | IsWide: {IsWideRatio}");
    }

    public void RegisterCanvasScaler(CanvasScaler canvasScaler, bool isGlobal)
    {
        if(canvasScaler == null) return;
        
        if (isGlobal)
        {
            if(globalCanvasScalerList.Contains(canvasScaler)) return;
            globalCanvasScalerList.Add(canvasScaler);
        }
        else
        {
            if(sceneCanvasScalerList.Contains(canvasScaler)) return;
            sceneCanvasScalerList.Add(canvasScaler);
        }
        SetCanvasScaler(canvasScaler);;
    }

    private void SetCanvasScaler(CanvasScaler canvasScaler)
    {
        if (canvasScaler == null) return;

        // 기준 해상도는 항상 설계 해상도로 고정
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = baseResolution;

        // [핵심 로직]
        // IsWideRatio(태블릿/폴드펼침) -> Match Height (1) : 위아래가 잘리면 안됨, 좌우는 넓게 씀
        // !IsWideRatio(폰/폴드접음) -> Match Width (0) : 좌우가 잘리면 안됨, 위아래는 길게 씀
        canvasScaler.matchWidthOrHeight = IsWideRatio ? 1f : 0f;
    }

    public void RegisterSafeArea(SetSafeArea safeArea, bool isGlobal)
    {
        if (safeArea == null) return;

        var list = isGlobal ? globalSafeAreaList : sceneSafeAreaList;
        if (list.Contains(safeArea)) return;
        list.Add(safeArea);
        safeArea.SetSafeAreaRect();
    }

    private void RefreshAllSafeArea(List<SetSafeArea> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null) { list.RemoveAt(i); continue; }
            list[i].SetSafeAreaRect();
        }
    }

    private void SetAllCanvasScaler(List<CanvasScaler> canvasScalerList)
    {
        for (int i = canvasScalerList.Count - 1; i >= 0; i--)
        {
            if (canvasScalerList[i] == null)
            {
                canvasScalerList.RemoveAt(i);
                continue;
            }
            SetCanvasScaler(canvasScalerList[i]);
        }
    }
    

    private void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidApplication.onConfigurationChanged += OnAndroidConfigChanged;
#endif
    }

    private void OnDisable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidApplication.onConfigurationChanged -= OnAndroidConfigChanged;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnAndroidConfigChanged(AndroidConfiguration config)
    {
        // 이벤트가 들어오면 코루틴을 통해 안전하게 처리
        // (Update에서도 감지하지만, 이벤트가 더 빠를 수 있으므로 이중 안전장치)
        if (_changeResolutionCoroutine != null) StopCoroutine(_changeResolutionCoroutine);
        _changeResolutionCoroutine = StartCoroutine(CoWaitAndApplyResolution());
    }
#endif
}