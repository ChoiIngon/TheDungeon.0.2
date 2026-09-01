using UnityEngine;

/// <summary>
/// 던전 위에 서 있는 존재. 플레이어와 몬스터가 모두 이 타입이다.
///
/// 순수 C# 이며 MonoBehaviour 가 아니다. 시뮬레이션(좌표, HP, 턴)과 표현(GameObject, 애니메이션)을
/// 분리해 두면 층 저장/복원과 테스트가 쉬워지고, 뷰가 없는 상태로도 규칙을 돌려볼 수 있다.
/// 화면에 보이는 몸은 <see cref="ActorView"/> 가 이 좌표를 따라간다.
/// </summary>
public class CrawlerActor
{
    /// <summary>8방향. 인덱스 순서는 시계 방향이며 입력/AI 양쪽에서 공유한다.</summary>
    public static readonly Vector2Int[] Directions = new Vector2Int[]
    {
        new Vector2Int( 0, +1), // 북
        new Vector2Int(+1, +1), // 북동
        new Vector2Int(+1,  0), // 동
        new Vector2Int(+1, -1), // 남동
        new Vector2Int( 0, -1), // 남
        new Vector2Int(-1, -1), // 남서
        new Vector2Int(-1,  0), // 서
        new Vector2Int(-1, +1), // 북서
    };

    public string Name = "이름 없음";

    public int X;
    public int Y;

    /// <summary>턴당 얻는 에너지. <see cref="TurnScheduler.ActionCost"/>(12)가 보통 속도다.</summary>
    public int Speed = TurnScheduler.ActionCost;

    /// <summary>쌓인 행동력. 스케줄러가 관리한다.</summary>
    public int Energy;

    public int MaxHp = 12;
    public int Hp = 12;

    /// <summary>근접 공격력. 실제 피해는 1 ~ AttackPower 사이에서 굴린다.</summary>
    public int AttackPower = 3;

    /// <summary>복도에서의 시야 반경(타일). 방 안에서는 방 전체가 보이므로 이 값이 쓰이지 않는다.</summary>
    public int SightRadius = 7;

    public bool IsPlayer;

    public IActorBrain Brain;

    public bool IsDead => 0 >= this.Hp;

    public Vector2Int Position => new Vector2Int(this.X, this.Y);

    /// <summary>체스판 거리(대각선을 1로 센다). 8방향으로 움직이므로 이 거리가 실제 걸음 수와 같다.</summary>
    public static int Distance(CrawlerActor a, CrawlerActor b)
    {
        return Distance(a.X, a.Y, b.X, b.Y);
    }

    public static int Distance(int ax, int ay, int bx, int by)
    {
        return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(ay - by));
    }

    /// <summary>타일 좌표를 던전의 월드 좌표로 변환한다.</summary>
    public Vector3 GetWorldPosition(float height = 0.0f)
    {
        return new Vector3(this.X * Dungeon.TileSize, height, this.Y * Dungeon.TileSize);
    }
}
