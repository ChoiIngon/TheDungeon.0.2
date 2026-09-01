using System.Collections.Generic;

/// <summary>
/// 타일별 시야 상태.
///
/// 로그라이크의 "본 적 있는 곳은 기억하지만 지금 보이는 것은 아니다"를 표현한다.
/// </summary>
public enum TileVisibility
{
    /// <summary>한 번도 본 적 없다. 화면에 아무것도 그리지 않는다.</summary>
    Unknown = 0,

    /// <summary>본 적은 있지만 지금은 보이지 않는다. 어둡게 그린다. 몬스터/아이템은 그리지 않는다.</summary>
    Remembered = 1,

    /// <summary>지금 보인다. 그대로 그린다.</summary>
    Visible = 2,
}

/// <summary>
/// 던전 전체의 시야 상태를 담는 격자.
///
/// 상태가 바뀐 타일만 <see cref="Dirty"/> 에 모아 두기 때문에, 뷰는 매번 전체 타일을 훑지 않고
/// 바뀐 것만 갱신하면 된다. 던전이 커질수록 이 차이가 커진다.
/// </summary>
public class VisibilityMap
{
    private readonly TileVisibility[] states;
    private readonly List<int> dirty = new List<int>();

    public int Width { get; }
    public int Height { get; }

    /// <summary>마지막 <see cref="BeginRecompute"/> 이후 상태가 바뀐 타일 인덱스. 중복이 들어 있을 수 있다.</summary>
    public IReadOnlyList<int> Dirty => this.dirty;

    public VisibilityMap(int width, int height)
    {
        this.Width = width;
        this.Height = height;
        this.states = new TileVisibility[width * height];
    }

    public TileVisibility Get(int index)
    {
        if (0 > index || index >= this.states.Length)
        {
            return TileVisibility.Unknown;
        }

        return this.states[index];
    }

    public TileVisibility Get(int x, int y)
    {
        if (0 > x || x >= this.Width || 0 > y || y >= this.Height)
        {
            return TileVisibility.Unknown;
        }

        return this.states[y * this.Width + x];
    }

    public bool IsVisible(int x, int y) => TileVisibility.Visible == Get(x, y);

    public bool IsExplored(int x, int y) => TileVisibility.Unknown != Get(x, y);

    /// <summary>
    /// 시야를 다시 계산하기 직전에 부른다.
    /// 지금 보이던 타일을 전부 "기억"으로 내리고, 이어지는 MarkVisible() 이 다시 올려 준다.
    /// </summary>
    public void BeginRecompute()
    {
        this.dirty.Clear();

        for (int i = 0; i < this.states.Length; i++)
        {
            if (TileVisibility.Visible != this.states[i])
            {
                continue;
            }

            this.states[i] = TileVisibility.Remembered;
            this.dirty.Add(i);
        }
    }

    public void MarkVisible(int x, int y)
    {
        if (0 > x || x >= this.Width || 0 > y || y >= this.Height)
        {
            return;
        }

        int index = y * this.Width + x;
        if (TileVisibility.Visible == this.states[index])
        {
            return;
        }

        this.states[index] = TileVisibility.Visible;
        this.dirty.Add(index);
    }

    /// <summary>디버그/치트용. 던전 전체를 본 것으로 만든다.</summary>
    public void RevealAll()
    {
        this.dirty.Clear();

        for (int i = 0; i < this.states.Length; i++)
        {
            if (TileVisibility.Unknown != this.states[i])
            {
                continue;
            }

            this.states[i] = TileVisibility.Remembered;
            this.dirty.Add(i);
        }
    }
}
