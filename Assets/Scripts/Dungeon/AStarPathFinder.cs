using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타일 격자 위의 A* 경로 탐색. 이동은 4방향만 허용한다.
/// </summary>
public class AStarPathFinder
{
    private static readonly Vector2Int[] LOOKUP_OFFSETS = {
            new Vector2Int(-1, 0),  // left
            new Vector2Int( 0,-1),  // down
            new Vector2Int( 1, 0),  // right
            new Vector2Int( 0, 1)   // up
        };

    /// <summary>
    /// 휴리스틱 배율. 한 칸 이동의 최소 비용과 같아야 A* 가 최적 경로를 보장한다(admissible).
    /// 이 값을 MinCost 보다 크게 올리면 탐색은 빨라지지만 경로가 최적이 아닐 수 있다.
    /// </summary>
    private const int HeuristicScale = TileMap.Tile.PathCost.MinCost;

    public class Node
    {
        public TileMap.Tile tile;
        public Node parent;
        public int index { get => this.tile.index; }
        public int pathCost;
        public int expectCost;
        public int cost { get => this.pathCost + this.expectCost; }
        public int heapIndex; // For binary heap management

        public Node(TileMap.Tile tile)
        {
            this.tile = tile;
            this.pathCost = 0;
            this.expectCost = 0;
            this.heapIndex = -1;
        }
    }

    private readonly TileMap tileMap;
    private readonly Rect boundary;
    private readonly DungeonRandom random;
    private readonly BinaryHeap openNodesHeap;
    private readonly Dictionary<int, Node> openNodeDict;
    private readonly Dictionary<int, Node> closeNodeDict;

    public AStarPathFinder(TileMap tileMap, Rect pathFindBoundary, DungeonRandom random = null)
    {
        this.tileMap = tileMap;
        this.boundary = pathFindBoundary;
        this.random = random;
        this.openNodesHeap = new BinaryHeap();
        this.openNodeDict = new Dictionary<int, Node>();
        this.closeNodeDict = new Dictionary<int, Node>();
    }

    /// <summary>
    /// from 에서 to 까지의 최소 비용 경로. 경로가 없으면 빈 리스트를 돌려준다(절대 null 이 아니다).
    /// 반환값은 호출할 때마다 새로 만든 리스트이므로 호출자가 보관해도 안전하다.
    /// </summary>
    public List<TileMap.Tile> FindPath(TileMap.Tile from, TileMap.Tile to)
    {
        openNodesHeap.Clear();
        openNodeDict.Clear();
        closeNodeDict.Clear();

        if (null == from || null == to)
        {
            return new List<TileMap.Tile>();
        }

        Node currentNode = new Node(from);
        currentNode.expectCost = Heuristic(from, to);

        openNodesHeap.Add(currentNode);
        openNodeDict.Add(currentNode.index, currentNode);

        while (openNodesHeap.Count > 0)
        {
            currentNode = openNodesHeap.Pop();
            openNodeDict.Remove(currentNode.index);
            closeNodeDict.Add(currentNode.index, currentNode);

            if (to == currentNode.tile)
            {
                return ReconstructPath(currentNode);
            }

            // 같은 비용의 경로가 여럿일 때 항상 같은 방향을 먼저 보면 복도가 한쪽으로 쏠린다.
            // 탐색 시작 방향을 섞어 모양에 변화를 준다.
            int offsetIndex = (null != random) ? random.Range(0, LOOKUP_OFFSETS.Length) : 0;
            for (int i = 0; i < LOOKUP_OFFSETS.Length; i++)
            {
                var offset = LOOKUP_OFFSETS[offsetIndex];

                int x = currentNode.index % tileMap.width + offset.x;
                int y = currentNode.index / tileMap.width + offset.y;

                offsetIndex = (offsetIndex + 1) % LOOKUP_OFFSETS.Length;

                var tile = this.GetTile(x, y);
                if (tile == null)
                {
                    continue;
                }

                if (TileMap.Tile.Type.Wall == tile.type)
                {
                    continue;
                }

                if (closeNodeDict.ContainsKey(tile.index))
                {
                    continue;
                }

                int newPathCost = currentNode.pathCost + tile.cost;

                if (openNodeDict.TryGetValue(tile.index, out Node openNode))
                {
                    // Already in open set - update if better path found
                    if (newPathCost < openNode.pathCost)
                    {
                        openNode.pathCost = newPathCost;
                        openNode.parent = currentNode;
                        openNodesHeap.UpdateNode(openNode);
                    }
                }
                else
                {
                    // New node - add to open set
                    Node child = new Node(tile);
                    child.parent = currentNode;
                    child.pathCost = newPathCost;
                    child.expectCost = Heuristic(tile, to);

                    openNodesHeap.Add(child);
                    openNodeDict.Add(child.index, child);
                }
            }
        }

        return new List<TileMap.Tile>(); // 도달 불가
    }

    /// <summary>4방향 이동이므로 맨해튼 거리가 최적 휴리스틱이다.</summary>
    private static int Heuristic(TileMap.Tile from, TileMap.Tile to)
    {
        int dx = (int)Mathf.Abs(to.rect.x - from.rect.x);
        int dy = (int)Mathf.Abs(to.rect.y - from.rect.y);
        return (dx + dy) * HeuristicScale;
    }

    private static List<TileMap.Tile> ReconstructPath(Node endNode)
    {
        List<TileMap.Tile> path = new List<TileMap.Tile>();

        Node current = endNode;
        while (current != null)
        {
            path.Add(current.tile);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private TileMap.Tile GetTile(int x, int y)
    {
        if (boundary.xMin > x || x >= boundary.xMax)
        {
            return null;
        }

        if (boundary.yMin > y || y >= boundary.yMax)
        {
            return null;
        }

        return tileMap.GetTile(x, y);
    }

    /// <summary>
    /// Binary heap implementation for efficient node prioritization
    /// </summary>
    private class BinaryHeap
    {
        private List<Node> heap = new List<Node>();

        public int Count => heap.Count;

        public void Add(Node node)
        {
            node.heapIndex = heap.Count;
            heap.Add(node);
            BubbleUp(node.heapIndex);
        }

        public Node Pop()
        {
            if (heap.Count == 0)
                return null;

            Node root = heap[0];
            root.heapIndex = -1;

            if (heap.Count == 1)
            {
                heap.Clear();
                return root;
            }

            Node lastNode = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            heap[0] = lastNode;
            lastNode.heapIndex = 0;

            BubbleDown(0);
            return root;
        }

        public void UpdateNode(Node node)
        {
            if (node.heapIndex < 0)
                return;

            // Check if we need to bubble up or down
            int parentIndex = (node.heapIndex - 1) / 2;
            if (node.heapIndex > 0 && CompareNodes(node, heap[parentIndex]) < 0)
            {
                BubbleUp(node.heapIndex);
            }
            else
            {
                BubbleDown(node.heapIndex);
            }
        }

        public void Clear()
        {
            heap.Clear();
        }

        private void BubbleUp(int index)
        {
            Node node = heap[index];
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                Node parentNode = heap[parentIndex];

                if (CompareNodes(node, parentNode) >= 0)
                    break;

                heap[index] = parentNode;
                parentNode.heapIndex = index;
                index = parentIndex;
            }

            heap[index] = node;
            node.heapIndex = index;
        }

        private void BubbleDown(int index)
        {
            Node node = heap[index];
            int heapCount = heap.Count;

            while (true)
            {
                int leftChildIndex = 2 * index + 1;
                int rightChildIndex = 2 * index + 2;
                int smallestIndex = index;

                if (leftChildIndex < heapCount && CompareNodes(heap[leftChildIndex], node) < 0)
                    smallestIndex = leftChildIndex;

                if (rightChildIndex < heapCount &&
                    CompareNodes(heap[rightChildIndex], heap[smallestIndex]) < 0)
                    smallestIndex = rightChildIndex;

                if (smallestIndex == index)
                    break;

                Node smallestNode = heap[smallestIndex];
                heap[index] = smallestNode;
                smallestNode.heapIndex = index;
                index = smallestIndex;
            }

            heap[index] = node;
            node.heapIndex = index;
        }

        private int CompareNodes(Node a, Node b)
        {
            int costComparison = a.cost.CompareTo(b.cost);
            if (costComparison != 0)
                return costComparison;

            return a.expectCost.CompareTo(b.expectCost);
        }
    }
}
