using System;
using System.Collections.Generic;
using UnityEngine;
using static TileMap;

/// <summary>
/// Bowyer-Watson 증분 삽입 방식의 델로네 삼각분할.
/// 방(Room)의 중심점들을 입력으로 받아 방 사이의 인접 관계 후보를 만든다.
/// </summary>
public class DelaunayTriangulation
{
    /// <summary>좌표 비교 허용 오차. 방 중심이 격자에 정렬되어 공원점이 흔하게 생기므로 필요하다.</summary>
    public const float Epsilon = 1e-4f;

    public class Point
    {
        public Room room;

        public Point(Room room)
        {
            this.room = room;
        }

        public Vector2 position
        {
            get { return this.room.rect.center; }
        }
    }

    public class Edge : IEquatable<Edge>
    {
        public Point v0;
        public Point v1;

        public float cost
        {
            get
            {
                if (null == v0 || null == v1)
                {
                    return 0.0f;
                }

                return Vector2.Distance(v0.position, v1.position);
            }
        }

        public Edge(Point v0, Point v1)
        {
            this.v0 = v0;
            this.v1 = v1;
        }

        public override bool Equals(object other)
        {
            return Equals(other as Edge);
        }

        public bool Equals(Edge edge)
        {
            if (null == edge)
            {
                return false;
            }

            return (Approximately(this.v0.position, edge.v0.position) && Approximately(this.v1.position, edge.v1.position))
                || (Approximately(this.v0.position, edge.v1.position) && Approximately(this.v1.position, edge.v0.position));
        }

        /// <summary>
        /// Equals() 가 좌표 기반이고 방향을 가리지 않으므로 해시도 같은 규칙을 따라야 한다.
        /// XOR 은 교환 법칙이 성립하므로 (v0,v1) 과 (v1,v0) 이 같은 해시를 낸다.
        /// </summary>
        public override int GetHashCode()
        {
            return Quantize(v0.position) ^ Quantize(v1.position);
        }
    }

    public class Circle
    {
        public Vector3 center;
        public float radius;

        public Circle(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }

        /// <summary>
        /// 원 위(경계)의 점도 내부로 판정한다.
        /// Bowyer-Watson 에서 공원점을 놓치면 겹치는 삼각형이 남으므로 경계는 포함시키는 편이 안전하다.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            return Vector3.Distance(center, point) <= radius + Epsilon;
        }
    }

    public class Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Circle circumCircle;
        public Circle innerCircle;
        public List<Edge> edges;

        /// <summary>AddPoint() 안에서 제거 대상 표시용. List.Remove() 의 O(n) 탐색을 피한다.</summary>
        public bool removed;

        private Triangle(Point p1, Point p2, Point p3, Circle circumCircle)
        {
            this.a = p1.position;
            this.b = p2.position;
            this.c = p3.position;

            this.circumCircle = circumCircle;
            this.innerCircle = CalcInnerCircle();
            this.edges = new List<Edge>
            {
                new Edge(p1, p2),
                new Edge(p2, p3),
                new Edge(p3, p1)
            };
        }

        /// <summary>
        /// 삼각형을 만든다. 축퇴(같은 점 / 일직선)이거나 외접원을 구할 수 없으면 null 을 돌려준다.
        /// 생성자를 그대로 열어두면 circumCircle 이 null 인 삼각형이 만들어져 이후 전부 NRE 로 이어진다.
        /// </summary>
        public static Triangle Create(Point p1, Point p2, Point p3)
        {
            if (null == p1 || null == p2 || null == p3)
            {
                return null;
            }

            Vector2 a = p1.position;
            Vector2 b = p2.position;
            Vector2 c = p3.position;

            if (Approximately(a, b) || Approximately(b, c) || Approximately(c, a))
            {
                return null;
            }

            Circle circumCircle = CalcCircumCircle(a, b, c);
            if (null == circumCircle)
            {
                return null;
            }

            return new Triangle(p1, p2, p3, circumCircle);
        }

        public override bool Equals(object other)
        {
            return Equals(other as Triangle);
        }

        /// <summary>꼭짓점 집합이 같으면 같은 삼각형이다(순서 무관).</summary>
        public bool Equals(Triangle triangle)
        {
            if (null == triangle)
            {
                return false;
            }

            return HasVertex(triangle.a) && HasVertex(triangle.b) && HasVertex(triangle.c);
        }

        public override int GetHashCode()
        {
            // 순서 무관 Equals 와 짝을 맞추기 위해 XOR 로 결합한다.
            return Quantize(a) ^ Quantize(b) ^ Quantize(c);
        }

        public bool HasVertex(Vector3 vertex)
        {
            return Approximately(a, vertex) || Approximately(b, vertex) || Approximately(c, vertex);
        }

        /// <summary>
        /// 세 점의 외접원. 참고: 삼각형 외접원 구하기 - https://kukuta.tistory.com/444
        ///
        /// 수직이등분선의 기울기를 그대로 쓰면 수평/수직 변마다 특수 케이스가 생기고 float 동등 비교에
        /// 의존하게 된다. 여기서는 특수 케이스가 없는 외심 공식(행렬식)을 사용한다.
        /// d 가 0 이면 세 점이 일직선이라는 뜻이므로 외접원이 존재하지 않는다.
        /// </summary>
        private static Circle CalcCircumCircle(Vector2 a, Vector2 b, Vector2 c)
        {
            float d = 2.0f * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
            if (Mathf.Abs(d) < Epsilon)
            {
                return null; // 일직선. 삼각형이 아니다.
            }

            float aSqr = a.x * a.x + a.y * a.y;
            float bSqr = b.x * b.x + b.y * b.y;
            float cSqr = c.x * c.x + c.y * c.y;

            float x = (aSqr * (b.y - c.y) + bSqr * (c.y - a.y) + cSqr * (a.y - b.y)) / d;
            float y = (aSqr * (c.x - b.x) + bSqr * (a.x - c.x) + cSqr * (b.x - a.x)) / d;

            float radius = Vector2.Distance(new Vector2(x, y), a);
            if (float.IsNaN(radius) || float.IsInfinity(radius))
            {
                return null;
            }

            return new Circle(new Vector3(x, y, 0.0f), radius);
        }

        /// <summary>세 변 길이로 구하는 내접원. 새 방을 놓을 "가장 넓은 빈 공간"을 찾는 데 쓴다.</summary>
        private Circle CalcInnerCircle()
        {
            float e1 = Vector3.Distance(b, c);
            float e2 = Vector3.Distance(c, a);
            float e3 = Vector3.Distance(a, b);

            float perimeter = e1 + e2 + e3;
            if (perimeter < Epsilon)
            {
                return new Circle(a, 0.0f);
            }

            float x = (e1 * a.x + e2 * b.x + e3 * c.x) / perimeter;
            float y = (e1 * a.y + e2 * b.y + e3 * c.y) / perimeter;

            float s = perimeter / 2.0f;
            // 부동소수 오차로 음수가 될 수 있으므로 클램프한다. Sqrt(음수) 는 NaN 이다.
            float areaSqr = Mathf.Max(0.0f, s * (s - e1) * (s - e2) * (s - e3));
            float radius = Mathf.Sqrt(areaSqr) / s;

            return new Circle(new Vector3(x, y, 0.0f), radius);
        }
    }

    private Triangle superTriangle = null;
    public List<Triangle> triangles = new List<Triangle>();

    /// <summary>AddPoint() 에서 매번 재할당하지 않도록 재사용하는 작업 버퍼.</summary>
    private readonly List<Triangle> badTriangles = new List<Triangle>();
    private readonly Dictionary<Edge, int> edgeUseCount = new Dictionary<Edge, int>();

    public DelaunayTriangulation(List<Room> rooms)
    {
        superTriangle = CreateSuperTriangle(rooms);
        if (null == superTriangle)
        {
            return;
        }

        triangles.Add(superTriangle);

        foreach (var room in rooms)
        {
            AddPoint(room);
        }

        RemoveSuperTriangle();
    }

    public void AddPoint(Room room)
    {
        Vector3 point = room.rect.center;

        badTriangles.Clear();
        foreach (var triangle in triangles)
        {
            // circumCircle 은 Triangle.Create() 가 보장하지만 방어적으로 한 번 더 확인한다.
            if (null == triangle.circumCircle)
            {
                continue;
            }

            if (true == triangle.circumCircle.Contains(point))
            {
                triangle.removed = true;
                badTriangles.Add(triangle);
            }
        }

        if (0 == badTriangles.Count)
        {
            return;
        }

        // 제거 대상 삼각형들의 변 중 "한 번만 등장하는 변"이 새로 채울 다각형의 경계다.
        // 변마다 등장 횟수를 세면 삼각형 쌍을 전부 비교하는 4중 루프가 필요 없다.
        edgeUseCount.Clear();
        foreach (var triangle in badTriangles)
        {
            foreach (Edge edge in triangle.edges)
            {
                edgeUseCount.TryGetValue(edge, out int count);
                edgeUseCount[edge] = count + 1;
            }
        }

        triangles.RemoveAll(triangle => triangle.removed);

        Point inserted = new Point(room);
        foreach (var pair in edgeUseCount)
        {
            if (1 != pair.Value)
            {
                continue;
            }

            Triangle triangle = Triangle.Create(pair.Key.v0, pair.Key.v1, inserted);
            if (null == triangle)
            {
                continue;
            }

            triangles.Add(triangle);
        }
    }

    public void RemoveSuperTriangle()
    {
        if (null == superTriangle)
        {
            return;
        }

        triangles.RemoveAll(triangle =>
               triangle.HasVertex(superTriangle.a)
            || triangle.HasVertex(superTriangle.b)
            || triangle.HasVertex(superTriangle.c));
    }

    /// <summary>
    /// 모든 입력 점을 감싸는 초대형 삼각형.
    /// 입력 영역보다 훨씬 크게 잡는 이유는, super triangle 의 변 위에 점이 놓이면
    /// 삼각형이 아니라 직선이 되어 델로네 삼각분할을 적용할 수 없기 때문이다.
    /// </summary>
    private Triangle CreateSuperTriangle(List<Room> rooms)
    {
        if (null == rooms || 0 == rooms.Count)
        {
            return null;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Room room in rooms)
        {
            Vector2 point = room.rect.center;
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        // 모든 점이 한 자리에 겹쳐 있으면 폭이 0 이라 삼각형을 만들 수 없다. 최소 크기를 보장한다.
        float dx = Mathf.Max(maxX - minX, 1.0f);
        float dy = Mathf.Max(maxY - minY, 1.0f);

        Room a = new Room(0, minX - dx, minY - dy, 0, 0);
        Room b = new Room(0, minX - dx, maxY + dy * 3, 0, 0);
        Room c = new Room(0, maxX + dx * 3, minY - dy, 0, 0);

        return Triangle.Create(new Point(a), new Point(b), new Point(c));
    }

    private static bool Approximately(Vector2 lhs, Vector2 rhs)
    {
        return Mathf.Abs(lhs.x - rhs.x) < Epsilon && Mathf.Abs(lhs.y - rhs.y) < Epsilon;
    }

    /// <summary>Epsilon 이내의 좌표가 같은 해시 버킷에 들어가도록 격자에 스냅한다.</summary>
    private static int Quantize(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x / Epsilon);
        int y = Mathf.RoundToInt(position.y / Epsilon);
        return x * 73856093 ^ y * 19349663;
    }
}
