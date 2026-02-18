using System.Collections.Generic;
using UnityEngine;

public class AStarPathFinder
{
    private static Vector2Int[] LOOKUP_OFFSETS = {
            new Vector2Int(-1, 0),  // left
            new Vector2Int( 0,-1),  // down
            new Vector2Int( 1, 0),  // right
            new Vector2Int( 0, 1)   // up
        };

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

    private TileMap tileMap;
    private Rect boundary;
    private BinaryHeap openNodesHeap;
    private Dictionary<int, Node> openNodeDict;
    private Dictionary<int, Node> closeNodeDict;

    public List<TileMap.Tile> path = new List<TileMap.Tile>();

    public AStarPathFinder(TileMap tileMap, Rect pathFindBoundary)
    {
        this.tileMap = tileMap;
        this.boundary = pathFindBoundary;
        this.openNodesHeap = new BinaryHeap();
        this.openNodeDict = new Dictionary<int, Node>();
        this.closeNodeDict = new Dictionary<int, Node>();
    }

    public List<TileMap.Tile> FindPath(TileMap.Tile from, TileMap.Tile to)
    {
        openNodesHeap.Clear();
        openNodeDict.Clear();
        closeNodeDict.Clear();
        path.Clear();

        Node currentNode = new Node(from);
        currentNode.expectCost = (int)Mathf.Abs(to.rect.x - from.rect.x) + 
                                 (int)Mathf.Abs(to.rect.y - from.rect.y);
        
        openNodesHeap.Add(currentNode);
        openNodeDict.Add(currentNode.index, currentNode);

        while (openNodesHeap.Count > 0)
        {
            currentNode = openNodesHeap.Pop();
            openNodeDict.Remove(currentNode.index);
            closeNodeDict.Add(currentNode.index, currentNode);

            if (to == currentNode.tile)
            {
                // Reconstruct path efficiently
                ReconstructPath(currentNode);
                return path;
            }

            int offsetIndex = UnityEngine.Random.Range(0, LOOKUP_OFFSETS.Length);
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
                    child.expectCost = (int)Mathf.Abs(to.rect.x - tile.rect.x) + 
                                       (int)Mathf.Abs(to.rect.y - tile.rect.y);

                    openNodesHeap.Add(child);
                    openNodeDict.Add(child.index, child);
                }
            }
        }

        return path; // Empty path if no route found
    }

    private void ReconstructPath(Node endNode)
    {
        path.Clear();
        Node current = endNode;
        while (current != null)
        {
            path.Add(current.tile);
            current = current.parent;
        }
        path.Reverse();
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
