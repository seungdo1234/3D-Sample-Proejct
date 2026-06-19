using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 항목별 가중치와 내부 진행률(0~1)을 모아 전체 진행률(0~1)로 환산한다.
/// 병렬 배치에서도 각 항목이 독립 슬롯에 보고하므로 합산이 정확하다.
/// </summary>
public class InitProgressAggregator
{
    // overall(0~1), label
    public event Action<float, string> OnUpdated;

    private readonly float _totalWeight;
    private readonly Dictionary<IBootInitializable, float> _doneWeight = new();
    private string _lastLabel = string.Empty;

    public InitProgressAggregator(IReadOnlyList<IBootInitializable> items)
    {
        float total = 0f;
        foreach (IBootInitializable item in items)
        {
            _doneWeight[item] = 0f;
            total += Mathf.Max(0f, item.Weight);
        }
        // 0으로 나누기 방지
        _totalWeight = total <= 0f ? 1f : total;
    }

    public IProgress<float> CreateFor(IBootInitializable item) => new ItemProgress(this, item);

    private void Report(IBootInitializable item, float subProgress)
    {
        _doneWeight[item] = Mathf.Clamp01(subProgress) * Mathf.Max(0f, item.Weight);
        // UI 라벨은 Phase 기준 (예: "SDK Loading..."). 클래스 이름/DisplayName 은 로그용.
        _lastLabel = item.Phase.ToLoadingLabel();

        float sum = 0f;
        foreach (KeyValuePair<IBootInitializable, float> kv in _doneWeight)
            sum += kv.Value;

        OnUpdated?.Invoke(Mathf.Clamp01(sum / _totalWeight), _lastLabel);
    }

    // 각 항목에 전달되는 진행률 프록시. Report 호출 시 자기 슬롯만 갱신.
    private class ItemProgress : IProgress<float>
    {
        private readonly InitProgressAggregator _owner;
        private readonly IBootInitializable _item;

        public ItemProgress(InitProgressAggregator owner, IBootInitializable item)
        {
            _owner = owner;
            _item = item;
        }

        public void Report(float value) => _owner.Report(_item, value);
    }
}
