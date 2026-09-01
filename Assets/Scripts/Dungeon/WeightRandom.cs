using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가중치 기반 랜덤 선택. 누적 가중치 + 이진 탐색.
/// 재현 가능한 생성을 위해 난수원은 외부에서 주입받는다.
/// </summary>
public class WeightRandom<T>
{
    private struct Element
    {
        public int weight;
        public int cumulative;
        public T value;
    }

    private readonly List<Element> elements = new List<Element>();
    private readonly DungeonRandom random;

    public int TotalWeight { get; private set; } = 0;

    public WeightRandom(DungeonRandom random)
    {
        this.random = random;
        this.TotalWeight = 0;
    }

    public void Add(int weight, T value)
    {
        if (0 >= weight)
        {
            // 조용히 무시하면 설정 실수를 놓치게 되므로 알린다.
            Debug.LogWarning($"WeightRandom: weight({weight}) 는 0 보다 커야 한다. value({value}) 를 무시한다.");
            return;
        }

        this.TotalWeight += weight;
        elements.Add(new Element { weight = weight, cumulative = TotalWeight, value = value });
    }

    public T Random()
    {
        if (0 == elements.Count)
        {
            throw new System.InvalidOperationException("WeightRandom has no elements");
        }

        int randomValue = random.Range(1, TotalWeight + 1);
        int index = BinarySearch(randomValue);
        return elements[index].value;
    }

    /// <summary>cumulative 가 targetWeight 이상인 첫 원소의 인덱스를 찾는다.</summary>
    private int BinarySearch(int targetWeight)
    {
        int lo = 0;
        int hi = elements.Count - 1;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (elements[mid].cumulative >= targetWeight)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }
}
