using System;

/// <summary>
/// 장애물 배치 런타임 설정. MapSystem 이 인스펙터 값으로 구성해 각 MapChunk 에 주입한다.
/// 가중 추첨용 누적 가중치(cumWeights)를 미리 계산해 둔다.
/// </summary>
public class MapObstacleSettings
{
    public string[] keys;            // 종류별 풀 키
    public bool[] randomYRotations;  // 종류별 Y축 랜덤 회전 여부 (keys 와 동일 인덱스)
    public float[] cumWeights;       // 누적 가중치 (가중 추첨용)
    public float totalWeight;

    public int divisions;        // 타일 분할 수 (div×div 슬롯)
    public float spawnChance;    // 슬롯당 배치 확률 (0~1)
    public float clearRadius;    // 원점 주변 비울 반경 (플레이어 시작 지점)

    public bool HasAny => keys != null && keys.Length > 0 && totalWeight > 0f;

    // 시드 rng 로 가중 추첨 → 종류 인덱스 반환 (키·회전여부를 같은 인덱스로 조회)
    public int PickIndex(Random rng)
    {
        float r = (float)rng.NextDouble() * totalWeight;
        for (int i = 0; i < cumWeights.Length; i++)
            if (r < cumWeights[i]) return i;
        return cumWeights.Length - 1;
    }
}
