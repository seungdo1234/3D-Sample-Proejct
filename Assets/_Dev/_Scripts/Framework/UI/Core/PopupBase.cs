using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum PopUpAnimationType
{
    None,   // 연출 없이 즉시
    PopUp,  // 스케일 인/아웃 (OutBack/InBack)
    SlideY  // 세로 슬라이드
}

/// <summary>
/// 팝업 공통 베이스. UIBase 위에 열기/닫기 연출, 입력 잠금(touchLock),
/// 닫기 버튼 자동 바인딩을 얹는다. 콘텐츠 로직은 OnOpenContent/OnCloseContent 에서,
/// 데이터 바인딩 등 초기화는 InitializeChild 에서 처리한다.
/// </summary>
public abstract class PopupBase<T> : UIBase<T> where T : GameBase
{
    [Header("# Base Settings")]
    // 연출 대상 Rect. 비워두면 이 컴포넌트가 붙은 오브젝트의 RectTransform 사용.
    [SerializeField] protected RectTransform animateTarget;
    [SerializeField] private PopUpAnimationType animationType = PopUpAnimationType.PopUp;
    [SerializeField] protected float animationDuration = 0.3f;
    // 연출 중 입력을 막는 풀스크린 Image (선택). 닫기 버튼 오작동/연타 방지.
    [SerializeField] protected Image touchLock;

    [Header("# PopUp Settings")]
    // 닫을 때 줄어들 목표 스케일 (0 = 완전히 사라짐, 0.5 = 절반만 줄고 사라짐 느낌)
    [SerializeField, Range(0f, 1f)] private float closeScale = 0.5f;

    [Header("# SlideY Settings")]
    // 슬라이드 시작 Y (화면 밖). 인스펙터에서 지정.
    [SerializeField] private float startHeightY;

    [Header("# Back Button")]
    // 누르면 자동으로 CloseUI 가 연결되는 버튼들.
    [SerializeField] protected Button[] exitButtons;

    private float originalPosY;
    private Tween animationTween;

    // ── 서브클래스 훅 ────────────────────────────────────────────────────────
    protected abstract void InitializeChild(T game); // 초기 1회 (데이터/리스너)
    protected abstract void OnOpenContent();          // 열기 콘텐츠 로직 (강제)
    protected abstract void OnCloseContent();         // 닫기 콘텐츠 로직 (강제)

    protected virtual void OnBeforeOpen() { }
    protected virtual void OnAfterOpen() { }
    protected virtual void OnBeforeClose() { }
    protected virtual void OnAfterClose() { } // 로비 복귀 등은 보통 여기서

    // ── 초기화 ──────────────────────────────────────────────────────────────
    protected override void InitializeGame(T game)
    {
        if (animateTarget == null)
            animateTarget = transform as RectTransform;

        if (animationType == PopUpAnimationType.SlideY)
            CacheSlideOriginY();

        if (exitButtons != null)
        {
            foreach (Button btn in exitButtons)
            {
                if (btn == null) continue;
                btn.onClick.AddListener(CloseUI);
            }
        }

        InitializeChild(game);
    }

    // 기준 해상도(예: 720x1280) 대비 현재 화면 비율에 따라 슬라이드 종료 Y 를 보정.
    // 화면이 더 세로로 길어지면 도착 위치를 위로 올려준다.
    private void CacheSlideOriginY()
    {
        float designedPosY = animateTarget.anchoredPosition.y;
        var resolution = GameManager.Instance?.ResolutionSystem;

        if (resolution != null)
        {
            float baseAspect = resolution.BaseScreenSize.x / resolution.BaseScreenSize.y;
            float currentAspect = (float)resolution.CurrentScreenSize.x / resolution.CurrentScreenSize.y;

            // 양수면 현재 화면이 더 홀쭉함(세로가 더 김) → Y 를 위로 보정
            float aspectDiff = baseAspect - currentAspect;
            float heightOffset = aspectDiff * resolution.BaseScreenSize.y * 0.45f;
            originalPosY = designedPosY + heightOffset;
        }
        else
        {
            originalPosY = designedPosY;
        }
    }

    // ── 열기 / 닫기 ─────────────────────────────────────────────────────────
    public override void OpenUI()
    {
        if (gameObject.activeSelf) return;

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 항상 최상단으로

        OnBeforeOpen();
        OnOpenContent();

        if (touchLock) touchLock.gameObject.SetActive(true);

        InternalAnimate(true, () =>
        {
            if (touchLock) touchLock.gameObject.SetActive(false);
            OnAfterOpen();
        });
    }

    public override void CloseUI()
    {
        if (!gameObject.activeSelf) return;

        OnBeforeClose();
        OnCloseContent();

        if (touchLock) touchLock.gameObject.SetActive(true);

        InternalAnimate(false, () =>
        {
            if (touchLock) touchLock.gameObject.SetActive(false);
            OnAfterClose();
        });
    }

    // ── 공개 연출 트리거 (콜백 필요한 외부 호출용) ────────────────────────────
    /// <summary>팝업 열기 애니메이션만 재생.</summary>
    public void AnimateOpen(Action callback = null)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (touchLock) touchLock.gameObject.SetActive(true);

        InternalAnimate(true, () =>
        {
            if (touchLock) touchLock.gameObject.SetActive(false);
            callback?.Invoke();
        });
    }

    /// <summary>팝업 닫기 애니메이션만 재생.</summary>
    public void AnimateClose(Action callback = null)
    {
        if (touchLock) touchLock.gameObject.SetActive(true);

        InternalAnimate(false, () =>
        {
            if (touchLock) touchLock.gameObject.SetActive(false);
            callback?.Invoke();
        });
    }

    // ── 내부 공통 애니메이션 ──────────────────────────────────────────────────
    private void InternalAnimate(bool isOpening, Action callback)
    {
        SetExitBtnInteractable(false);

        if (isOpening) gameObject.SetActive(true);

        // 이전 연출 정리 (Kill 시 OnComplete 는 호출되지 않음 → 상태 누수 방지를 위해
        // 새 연출이 끝나는 FinishAnimate 에서 반드시 상태를 복구한다.)
        animationTween?.Kill();
        animationTween = null;
        if (animateTarget != null) animateTarget.DOKill();

        Ease ease = isOpening ? Ease.OutBack : Ease.InBack;

        switch (animationType)
        {
            case PopUpAnimationType.PopUp:
            {
                float startVal = isOpening ? 0f : 1f;
                float endVal = isOpening ? 1f : closeScale;

                animateTarget.localScale = Vector3.one * startVal;
                animationTween = animateTarget
                    .DOScale(endVal, animationDuration)
                    .SetEase(ease)
                    .SetUpdate(true) // 일시정지(timeScale 0) 중에도 동작
                    .OnComplete(() => FinishAnimate(isOpening, callback));
                break;
            }

            case PopUpAnimationType.SlideY:
            {
                float startVal = isOpening ? startHeightY : originalPosY;
                float endVal = isOpening ? originalPosY : startHeightY;

                var pos = animateTarget.anchoredPosition;
                animateTarget.anchoredPosition = new Vector2(pos.x, startVal);
                animationTween = animateTarget
                    .DOAnchorPosY(endVal, animationDuration)
                    .SetEase(isOpening ? Ease.OutQuart : Ease.InQuart)
                    .SetUpdate(true)
                    .OnComplete(() => FinishAnimate(isOpening, callback));
                break;
            }

            default: // None
                FinishAnimate(isOpening, callback);
                break;
        }
    }

    private void FinishAnimate(bool isOpening, Action callback)
    {
        animationTween = null;
        SetExitBtnInteractable(true);

        callback?.Invoke();

        if (!isOpening)
            gameObject.SetActive(false);
    }

    private void SetExitBtnInteractable(bool isInteractable)
    {
        if (exitButtons == null) return;
        foreach (Button btn in exitButtons)
        {
            if (btn != null) btn.interactable = isInteractable;
        }
    }

    protected virtual void OnDestroy()
    {
        animationTween?.Kill();
        if (animateTarget != null) animateTarget.DOKill();
    }
}
