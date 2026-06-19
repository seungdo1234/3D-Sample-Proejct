using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Initialize 씬의 로딩 화면. Bootstrapper 의 진행률을 받아 바/텍스트를 갱신한다.
/// InitSceneDirector 의 progressView 필드에 연결해 사용한다.
/// </summary>
public class UI_BootLoading : MonoBehaviour, IInitProgressView
{
    [Header("# Progress")]
    [Tooltip("Image Type 을 Filled 로 설정한 채움 이미지")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("# Smoothing")]
    [Tooltip("바가 목표치로 따라가는 속도(초당). 0 이하면 즉시 반영")]
    [SerializeField] private float fillSpeed = 4f;

    private float _target;

    private void Awake()
    {
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    public void SetProgress(float normalized, string label)
    {
        _target = Mathf.Clamp01(normalized);
        if (labelText != null) labelText.text = label;

        // 즉시 반영 모드
        if (fillSpeed <= 0f)
            ApplyBar(_target);
    }

    public void ShowError(string taskName, Exception error)
    {
        // 에러 UI 는 아직 미구현 — 로그만 남긴다. 필요 시 팝업/재시도 UI 로 확장.
        DebugUtil.DevelopmentLog($"[UI_BootLoading] 초기화 실패: {taskName} / {error.Message}", DebugUtil.LogType.Error);
    }

    private void Update()
    {
        if (fillSpeed <= 0f || fillImage == null) return;

        float current = Mathf.MoveTowards(fillImage.fillAmount, _target, fillSpeed * Time.unscaledDeltaTime);
        ApplyBar(current);
    }

    private void ApplyBar(float value)
    {
        if (fillImage != null) fillImage.fillAmount = value;
        if (percentText != null) percentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
