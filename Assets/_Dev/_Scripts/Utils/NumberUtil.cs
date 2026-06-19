using TMPro;

public static class NumberUtil
{
    /// <summary>
    /// 숫자를 축약 표기로 변환합니다.
    /// 10,000 이상 → K / 10,000,000 이상 → M
    /// </summary>
    public static string ToShortString(int value)
    {
        if (value >= 10_000_000)
            return $"{value / 1_000_000}M";
        if (value >= 10_000)
            return $"{value / 1_000}K";
        return value.ToString();
    }

    /// <summary>
    /// 축약 숫자를 "{owned}/{required}" 형식으로 반환합니다.
    /// owned가 부족하면 owned 부분에 red rich text 태그를 적용합니다.
    /// </summary>
    public static string ToShortCostString(int owned, int required)
    {
        string ownedStr    = ToShortString(owned);
        string requiredStr = ToShortString(required);
        return owned >= required
            ? $"{ownedStr}/{requiredStr}"
            : $"<color=#FF4D4D>{ownedStr}</color>/{requiredStr}";
    }

    /// <summary>
    /// TMP 텍스트에 축약 숫자를 string 할당 없이 설정합니다.
    /// </summary>
    public static void SetShortText(TextMeshProUGUI text, int value)
    {
        if (value >= 10_000_000f)
            text.SetText("{0}M", (int)(value / 1_000_000f));
        else if (value >= 10_000f)
            text.SetText("{0}K", (int)(value / 1_000f));
        else
            text.SetText("{0}", (int)value);
    }
}
