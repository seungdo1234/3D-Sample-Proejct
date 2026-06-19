using System;
using System.Security.Cryptography;

public static class SecureRandomUtil
{
    // 암호학적 RNG 인스턴스
    private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

    // [min, max) 범위의 정수를 반환
    public static int Range(int min, int max)
    {
        if (min >= max)
            throw new ArgumentException("min은 max보다 작아야 합니다.");

        // 범위 크기
        long range = (long)max - min;
        // bias 없이 균등 분포 보장
        long maxAcceptable = (long)uint.MaxValue / range * range - 1;
        uint value;
        do
        {
            // 4바이트(32비트) 랜덤 생성
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            value = BitConverter.ToUInt32(bytes, 0);
        } while (value > maxAcceptable);

        return (int)(min + (value % range));
    }

    // value 이하 확률 체크 (start ≤ rand < end 범위)
    public static bool Valid(int value, int start = 0, int end = 10000)
    {
        int rand = Range(start, end);
        return rand <= value;
    }

    // start ≤ rand < end 범위의 인덱스
    public static int RandomIndex(int start, int end)
    {
        return Range(start, end);
    }

    // 가중치 기반 인덱스 추출
    public static int GetRandomIndexWithWeight(int[] weight)
    {
        if (weight == null || weight.Length == 0)
            return 0;

        long totalWeight = 0;
        foreach (var w in weight)
            if (w > 0) totalWeight += w;
        if (totalWeight <= 0)
            return 0;

        // [0, totalWeight) 범위에서 랜덤
        int randomValue = Range(0, (int)totalWeight);

        for (int i = 0; i < weight.Length; i++)
        {
            if (weight[i] <= 0) continue;
            if (randomValue < weight[i])
                return i;
            randomValue -= weight[i];
        }
        return 0;
    }
}