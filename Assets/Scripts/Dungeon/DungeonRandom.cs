/// <summary>
/// 던전 생성 전용 난수 공급자.
///
/// UnityEngine.Random 은 프로세스 전역 상태이기 때문에, 생성 도중 다른 스크립트(파티클,
/// 애니메이션 등)가 난수를 소비하면 같은 시드로도 다른 던전이 나온다.
/// 생성 파이프라인은 이 클래스의 인스턴스만 사용해서 재현성을 보장한다.
///
/// Range()의 경계 규약은 UnityEngine.Random 과 동일하게 맞춰 두었다.
///  - Range(int, int)   : max 배타적
///  - Range(float, float): max 포함
/// </summary>
public class DungeonRandom
{
    private readonly System.Random random;

    /// <summary>이 인스턴스를 만들 때 사용한 시드. 로그/재현에 사용한다.</summary>
    public int Seed { get; }

    public DungeonRandom(int seed)
    {
        this.Seed = seed;
        this.random = new System.Random(seed);
    }

    /// <summary>[minInclusive, maxExclusive) 범위의 정수. min &gt;= max 이면 min 을 돌려준다.</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
        {
            int swap = minInclusive;
            minInclusive = maxExclusive;
            maxExclusive = swap;
        }

        if (minInclusive >= maxExclusive)
        {
            return minInclusive;
        }

        return random.Next(minInclusive, maxExclusive);
    }

    /// <summary>[minInclusive, maxInclusive] 범위의 실수.</summary>
    public float Range(float minInclusive, float maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            float swap = minInclusive;
            minInclusive = maxInclusive;
            maxInclusive = swap;
        }

        return minInclusive + (float)random.NextDouble() * (maxInclusive - minInclusive);
    }

    /// <summary>percent 확률(0~100)로 true.</summary>
    public bool Chance(float percent)
    {
        return Range(0.0f, 100.0f) < percent;
    }
}
