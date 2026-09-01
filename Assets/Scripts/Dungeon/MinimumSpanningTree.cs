using System.Collections.Generic;

/// <summary>
/// 크루스칼 최소 신장 트리.
/// 델로네 삼각분할이 만든 방 인접 후보 중, 모든 방을 최소 비용으로 잇는 간선 집합을 고른다.
/// </summary>
public class MinimumSpanningTree
{
    public class Edge
    {
        public Edge(TileMap.Room p1, TileMap.Room p2, float cost)
        {
            this.room1 = p1;
            this.room2 = p2;
            this.cost = cost;
        }

        public TileMap.Room room1;
        public TileMap.Room room2;
        public float cost;
    }

    private readonly Dictionary<TileMap.Room, TileMap.Room> parents = new Dictionary<TileMap.Room, TileMap.Room>();

    /// <summary>방마다 부여한 연속 id. 간선 중복 검사 키를 만드는 데 쓴다.</summary>
    private readonly Dictionary<TileMap.Room, int> roomIds = new Dictionary<TileMap.Room, int>();

    /// <summary>이미 추가된 간선 키. AddEdge 의 선형 스캔(O(E))을 없앤다.</summary>
    private readonly HashSet<long> edgeKeys = new HashSet<long>();

    public List<Edge> edges = new List<Edge>();
    public List<Edge> connections = new List<Edge>();

    public MinimumSpanningTree(List<TileMap.Room> rooms)
    {
        foreach (TileMap.Room room in rooms)
        {
            if (true == parents.ContainsKey(room))
            {
                continue; // 같은 방이 두 번 들어와도 안전하게 넘어간다.
            }

            parents.Add(room, room);
            roomIds.Add(room, roomIds.Count);
        }
    }

    public void AddEdge(Edge edge)
    {
        if (null == edge || null == edge.room1 || null == edge.room2 || edge.room1 == edge.room2)
        {
            return;
        }

        if (false == roomIds.TryGetValue(edge.room1, out int id1) ||
            false == roomIds.TryGetValue(edge.room2, out int id2))
        {
            return; // 이 트리가 모르는 방. 무시한다.
        }

        if (false == edgeKeys.Add(MakeKey(id1, id2)))
        {
            return; // 이미 같은 (무향) 간선이 있다.
        }

        edges.Add(edge);
    }

    public void BuildTree()
    {
        edges.Sort((Edge e1, Edge e2) => e1.cost.CompareTo(e2.cost));

        foreach (Edge edge in edges)
        {
            TileMap.Room srcParent = FindParent(edge.room1);
            TileMap.Room destParent = FindParent(edge.room2);

            if (srcParent != destParent)
            {
                connections.Add(edge);
                parents[srcParent] = destParent;
            }
        }
    }

    /// <summary>무향 간선을 방향 무관한 64bit 키로 만든다.</summary>
    private static long MakeKey(int id1, int id2)
    {
        int low = id1 < id2 ? id1 : id2;
        int high = id1 < id2 ? id2 : id1;
        return ((long)low << 32) | (uint)high;
    }

    /// <summary>
    /// 경로 압축 union-find. 방 개수가 늘어나도 스택이 터지지 않도록 재귀 대신 반복문을 쓴다.
    /// </summary>
    private TileMap.Room FindParent(TileMap.Room room)
    {
        TileMap.Room root = room;
        while (parents[root] != root)
        {
            root = parents[root];
        }

        // 두 번째 순회에서 지나온 노드들을 루트에 직접 붙인다.
        while (parents[room] != root)
        {
            TileMap.Room next = parents[room];
            parents[room] = root;
            room = next;
        }

        return root;
    }
}
