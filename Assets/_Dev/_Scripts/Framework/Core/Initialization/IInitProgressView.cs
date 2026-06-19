using System;

/// <summary>
/// 부팅 진행률을 표시하는 UI 가 구현한다. Bootstrapper 와 UI 를 분리하는 계약.
/// </summary>
public interface IInitProgressView
{
    // normalized: 전체 진행률 0~1, label: 현재 태스크 표시 이름.
    void SetProgress(float normalized, string label);

    // 필수 태스크 실패로 부팅이 중단됐을 때.
    void ShowError(string taskName, Exception error);
}
