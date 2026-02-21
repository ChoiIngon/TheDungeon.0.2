using System.Collections.Generic;
using UnityEngine;

public class WeightRandom<T>
{
    private struct Element
    {
        public int weight;
        public int cumulative;
        public T value;
    }

    private List<Element> elements = new List<Element>();
    public int TotalWeight { get; private set; } = 0;

    public WeightRandom()
    {
        this.TotalWeight = 0;
    }

    public void Add(int weight, T value)
    {
        if (0 >= weight)
        {
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

        int randomValue = UnityEngine.Random.Range(1, TotalWeight + 1);
        int index = BinarySearch(randomValue);
        return elements[index].value;
    }

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
