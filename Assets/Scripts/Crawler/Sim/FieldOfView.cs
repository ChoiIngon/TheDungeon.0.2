using UnityEngine;

/// <summary>
/// 재귀 그림자 투사(recursive shadowcasting) 기반 시야 계산.
///
/// 원점에서 8분면(octant)을 하나씩 훑으면서, 시야를 막는 타일이 만드는 그림자 각도를 잘라 나간다.
/// 레이를 한 줄씩 쏘는 방식과 달리 대칭적이고 구멍이 생기지 않으며, 타일 하나를 한 번씩만 검사한다.
///
/// 여기에 NetHack 의 "방은 통째로 밝다" 규칙을 얹었다.
///  - 방 안(또는 문간)에 서 있으면 그 방 전체가 보인다.
///  - 복도에서는 <see cref="CrawlerActor.SightRadius"/> 만큼만 보인다.
/// 이 규칙이 있어야 방에 들어서는 순간 구조가 한눈에 들어오고, 복도는 답답한 맛이 산다.
/// </summary>
public class FieldOfView
{
    // 8분면 변환 행렬. octant 인덱스마다 (deltaX, deltaY) 를 실제 좌표 증분으로 바꾼다.
    private static readonly int[] TransformXX = { 1, 0, 0, -1, -1, 0, 0, 1 };
    private static readonly int[] TransformXY = { 0, 1, -1, 0, 0, -1, 1, 0 };
    private static readonly int[] TransformYX = { 0, 1, -1, 0, 0, 1, -1, 0 };
    private static readonly int[] TransformYY = { 1, 0, 0, 1, -1, 0, 0, -1 };

    private readonly CrawlerWorld world;

    public FieldOfView(CrawlerWorld world)
    {
        this.world = world;
    }

    /// <summary>
    /// (originX, originY) 에서 본 시야를 visibility 에 기록한다.
    /// 이전 시야는 "기억" 상태로 내려간다.
    /// </summary>
    public void Compute(int originX, int originY, int radius, VisibilityMap visibility)
    {
        visibility.BeginRecompute();
        visibility.MarkVisible(originX, originY);

        for (int octant = 0; octant < 8; octant++)
        {
            CastLight(octant, 1, 1.0f, 0.0f, originX, originY, radius, visibility);
        }

        RevealRoom(originX, originY, visibility);
    }

    /// <summary>
    /// 한 8분면에 대해 row 번째 줄부터 그림자를 투사한다.
    ///
    /// start / end 는 아직 그림자에 가려지지 않은 각도 구간의 기울기다.
    /// 시야를 막는 타일을 만나면 그 타일이 가리는 구간을 잘라내고, 남은 구간을 재귀로 넘긴다.
    /// </summary>
    private void CastLight(int octant, int row, float start, float end, int originX, int originY, int radius, VisibilityMap visibility)
    {
        if (start < end)
        {
            return;
        }

        int radiusSquared = radius * radius;
        float nextStart = start;
        bool blocked = false;

        for (int distance = row; distance <= radius && false == blocked; distance++)
        {
            int deltaY = -distance;

            for (int deltaX = -distance; deltaX <= 0; deltaX++)
            {
                int currentX = originX + deltaX * TransformXX[octant] + deltaY * TransformXY[octant];
                int currentY = originY + deltaX * TransformYX[octant] + deltaY * TransformYY[octant];

                // 이 타일이 차지하는 각도 구간. 타일의 왼쪽 모서리와 오른쪽 모서리 기울기다.
                float leftSlope = (deltaX - 0.5f) / (deltaY + 0.5f);
                float rightSlope = (deltaX + 0.5f) / (deltaY - 0.5f);

                if (start < rightSlope)
                {
                    continue; // 아직 훑고 있는 구간의 오른쪽 바깥이다.
                }

                if (end > leftSlope)
                {
                    break; // 구간을 완전히 지나쳤다.
                }

                // 사각형이 아니라 원으로 자른다. 모서리까지 보이면 시야가 네모나게 보인다.
                if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                {
                    visibility.MarkVisible(currentX, currentY);
                }

                bool blocksSight = this.world.BlocksSight(currentX, currentY);

                if (true == blocked)
                {
                    if (true == blocksSight)
                    {
                        nextStart = rightSlope; // 벽이 이어지고 있다. 그림자를 넓힌다.
                        continue;
                    }

                    // 벽이 끝났다. 그 너머부터 다시 보이기 시작한다.
                    blocked = false;
                    start = nextStart;
                    continue;
                }

                if (true == blocksSight && distance < radius)
                {
                    // 벽을 처음 만났다. 벽 왼쪽의 아직 보이는 구간을 재귀로 마저 처리한다.
                    blocked = true;
                    CastLight(octant, distance + 1, start, leftSlope, originX, originY, radius, visibility);
                    nextStart = rightSlope;
                }
            }
        }
    }

    /// <summary>
    /// 원점이 방 안(또는 방의 문간)이면 그 방 전체를 보이게 한다.
    /// 방의 벽까지 포함해야 방 윤곽이 끊기지 않고 그려진다.
    /// </summary>
    private void RevealRoom(int originX, int originY, VisibilityMap visibility)
    {
        TileMap.Tile tile = this.world.GetTile(originX, originY);
        if (null == tile || null == tile.room)
        {
            return;
        }

        Rect rect = tile.room.rect;
        for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
        {
            for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
            {
                visibility.MarkVisible(x, y);
            }
        }
    }

    /// <summary>
    /// 두 지점 사이에 시야가 통하는지 본다(브레젠험 직선).
    ///
    /// 몬스터가 플레이어를 봤는지 판정하는 용도다. 한 지점에서 전체 시야를 계산하는 것보다 훨씬 싸다.
    /// 양 끝점은 검사하지 않는다. 자기 자신이나 목표가 문 위에 서 있어도 서로 보여야 하기 때문이다.
    /// </summary>
    public bool HasLineOfSight(int fromX, int fromY, int toX, int toY)
    {
        int deltaX = Mathf.Abs(toX - fromX);
        int deltaY = Mathf.Abs(toY - fromY);
        int stepX = (fromX < toX) ? 1 : -1;
        int stepY = (fromY < toY) ? 1 : -1;
        int error = deltaX - deltaY;

        int x = fromX;
        int y = fromY;

        while (true)
        {
            if (x == toX && y == toY)
            {
                return true;
            }

            int doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            if (x == toX && y == toY)
            {
                return true;
            }

            if (true == this.world.BlocksSight(x, y))
            {
                return false;
            }
        }
    }
}
