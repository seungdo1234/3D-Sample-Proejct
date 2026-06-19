using System;

/// <summary>
/// UI 가 풀리는 컨텐츠 조건. 비트플래그로 조합 가능.
/// None 이면 조건 없음. 새 컨텐츠가 생기면 여기에 1 &lt;&lt; n 으로 추가한다.
/// </summary>
[Flags]
public enum ContentType
{
    None = 0,
    // 예시: 실제 컨텐츠가 생기면 채운다
    // Titan = 1 << 0,
    // Raid  = 1 << 1,
}
