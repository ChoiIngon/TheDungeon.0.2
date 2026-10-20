using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// <see cref="TileMap"/> 생성만 따로 떼어 검증하는 테스트 컴포넌트. (Scenes/TileMapTest.unity)
///
/// 사용법
///   - seed 가 0 이 아니면 그 시드로 던전 하나를 만들어 <see cref="DungeonDebugView"/> 로 그린다.
///   - seed 가 0 이고 testCount 가 0 보다 크면 무작위 시드로 testCount 번 생성하고 결과를 리포트한다.
///   - 둘 다 0 이면 무작위 시드로 하나를 만들어 그린다.
///   플레이 시작 시 자동 실행되며, 인스펙터 컨텍스트 메뉴로 에디트 모드에서도 실행할 수 있다.
///
/// 실패 판정 (<see cref="Inspect"/>)
///   1. TileMap.TryBuild 자체가 실패했다.
///   2. 다른 타일과 이어지지 않고 동떨어진 바닥/벽 타일이 있다. (빈 공간이 아닌 타일의 8방향 연결 성분이 둘 이상)
///   3. 다른 바닥과 4방향으로 이어지지 않은 바닥 섬이 있다. (걸어서 갈 수 없는 바닥)
///   4. (checkWallLeak) 바닥이 빈 공간이나 맵 밖에 바로 닿아 있다. (벽이 빠진 자리)
/// 실패한 타일은 화면에서 빨간색으로 표시된다.
/// </summary>
public class TileMapGenerationTest : MonoBehaviour
{
    [Header("Generation")]
    [Min(2)]
    public int roomCount = 10;

    [Min(TileMap.MinRoomSize)]
    public int minRoomSize = TileMap.MinRoomSize;

    [Min(TileMap.MinRoomSize)]
    public int maxRoomSize = 7;

    [Header("Test")]
    [Tooltip("0 이 아니면 이 시드로 던전 하나를 만들어 그린다. testCount 보다 우선한다.")]
    public int seed = 0;

    [Min(0)]
    [Tooltip("seed 가 0 일 때, 무작위 시드로 이 횟수만큼 반복 생성하고 결과를 리포트한다.")]
    public int testCount = 0;

    [Tooltip("바닥이 빈 공간(또는 맵 밖)에 바로 닿아 있으면, 즉 벽이 빠져 있으면 실패로 본다.")]
    public bool checkWallLeak = true;

    [Min(1)]
    [Tooltip("반복 테스트 중 한 프레임에 처리할 생성 횟수. 크면 빨리 끝나지만 화면이 멈춘 듯 보인다.")]
    public int iterationsPerFrame = 20;

    [Tooltip("플레이 시작 시 자동으로 실행한다.")]
    public bool runOnStart = true;

    [Header("View")]
    [Min(1)]
    public int cellPixels = 8;

    [Tooltip("비워 두면 Camera.main 을 쓴다.")]
    public Camera targetCamera;

    /// <summary>리포트에 실패 시드를 몇 개까지 나열할지.</summary>
    private const int MaxListedFailures = 100;

    private const string ViewName = "DungeonDebugView";

    /// <summary>한 번의 생성 결과.</summary>
    public class Result
    {
        public int seed;
        public bool passed;
        public double elapsedMilliseconds;
        public string failureReason;
        public TileMap.Tile.Type[,] grid;
        public List<Vector2Int> badTiles = new List<Vector2Int>();
    }

    private DungeonDebugView view;
    private Coroutine running;
    private string report = string.Empty;

    /// <summary>마지막으로 화면에 그린 단일 생성의 시드. 아직 없으면 0.</summary>
    public int LastSeed { get; private set; }

    private void Start()
    {
        if (true == runOnStart)
        {
            Run();
        }
    }

    private void OnDestroy()
    {
        if (null != this.view)
        {
            this.view.Destroy();
            this.view = null;
        }
    }

    /// <summary>인스펙터 설정에 따라 단일 생성 또는 반복 테스트를 실행한다.</summary>
    [ContextMenu("Run")]
    public void Run()
    {
        if (0 != this.seed)
        {
            GenerateSingle(this.seed);
        }
        else if (0 < this.testCount)
        {
            RunRepeatTest(this.testCount);
        }
        else
        {
            GenerateSingle(NewRandomSeed(new System.Random()));
        }
    }

    [ContextMenu("Generate Random Single")]
    public void GenerateRandomSingle()
    {
        GenerateSingle(NewRandomSeed(new System.Random()));
    }

    /// <summary>지정한 시드로 던전 하나를 만들어 그린다.</summary>
    public Result GenerateSingle(int seed)
    {
        StopRunning();

        Result result = RunOnce(seed);
        Draw(result);
        this.LastSeed = seed;

        if (true == result.passed)
        {
            this.report = $"seed {seed}: PASS ({result.elapsedMilliseconds:F2} ms, {result.grid.GetLength(0)}x{result.grid.GetLength(1)})";
            Debug.Log($"[TileMapGenerationTest] {this.report}");
        }
        else
        {
            this.report = $"seed {seed}: FAIL - {result.failureReason} ({result.elapsedMilliseconds:F2} ms)";
            Debug.LogError($"[TileMapGenerationTest] {this.report}");
        }

        return result;
    }

    /// <summary>무작위 시드로 count 번 생성하고 리포트한다. 플레이 중이면 여러 프레임에 나눠 실행한다.</summary>
    public void RunRepeatTest(int count)
    {
        StopRunning();

        IEnumerator routine = RepeatTestRoutine(count);
        if (true == Application.isPlaying)
        {
            this.running = StartCoroutine(routine);
            return;
        }

        // 에디트 모드에서는 코루틴이 돌지 않으므로 한 번에 끝까지 실행한다.
        while (true == routine.MoveNext())
        {
        }
    }

    private IEnumerator RepeatTestRoutine(int count)
    {
        var seedSource = new System.Random();

        int passCount = 0;
        double totalMilliseconds = 0.0;
        double maxMilliseconds = 0.0;
        int maxSeed = 0;
        var failures = new List<Result>();
        Result firstDrawableFailure = null;
        Result lastPass = null;

        for (int i = 0; i < count; i++)
        {
            Result result = RunOnce(NewRandomSeed(seedSource));

            totalMilliseconds += result.elapsedMilliseconds;
            if (result.elapsedMilliseconds > maxMilliseconds)
            {
                maxMilliseconds = result.elapsedMilliseconds;
                maxSeed = result.seed;
            }

            if (true == result.passed)
            {
                passCount++;
                lastPass = result;
            }
            else
            {
                if (null == firstDrawableFailure && null != result.grid)
                {
                    firstDrawableFailure = result;
                }

                // 그리지 않을 결과는 배열을 들고 있을 이유가 없다.
                if (firstDrawableFailure != result)
                {
                    result.grid = null;
                    result.badTiles = null;
                }

                failures.Add(result);
            }

            if (0 == (i + 1) % this.iterationsPerFrame)
            {
                this.report = $"testing... {i + 1} / {count} (fail {failures.Count})";
                yield return null;
            }
        }

        this.running = null;

        // 실패가 있으면 첫 번째로 그릴 수 있는 실패를, 없으면 마지막 성공을 보여 준다.
        Draw(firstDrawableFailure ?? lastPass);

        var builder = new StringBuilder();
        builder.AppendLine($"TileMap generation test: {count} runs (roomCount {roomCount}, roomSize {minRoomSize}~{maxRoomSize})");
        builder.AppendLine($"  pass {passCount}, fail {failures.Count}");
        builder.AppendLine($"  max time : {maxMilliseconds:F2} ms (seed {maxSeed})");
        builder.AppendLine($"  avg time : {(0 < count ? totalMilliseconds / count : 0.0):F2} ms");

        if (0 < failures.Count)
        {
            builder.AppendLine($"  failed seeds:");
            for (int i = 0; i < failures.Count && i < MaxListedFailures; i++)
            {
                builder.AppendLine($"    {failures[i].seed} : {failures[i].failureReason}");
            }

            if (MaxListedFailures < failures.Count)
            {
                builder.AppendLine($"    ... and {failures.Count - MaxListedFailures} more");
            }

            if (null != firstDrawableFailure)
            {
                builder.AppendLine($"  showing seed {firstDrawableFailure.seed} (red = bad tiles)");
            }
        }

        this.report = builder.ToString();
        if (0 < failures.Count)
        {
            Debug.LogError($"[TileMapGenerationTest] {this.report}");
        }
        else
        {
            Debug.Log($"[TileMapGenerationTest] {this.report}");
        }
    }

    /// <summary>한 번 생성하고 검사한다. 시간은 생성(TryBuild + 배열 변환)만 잰다.</summary>
    public Result RunOnce(int seed)
    {
        var result = new Result();
        result.seed = seed;

        var config = new TileMap.Config(this.roomCount, this.minRoomSize, this.maxRoomSize);
        var stopwatch = Stopwatch.StartNew();
        bool built = TileMap.TryBuild(config, new DungeonRandom(seed), out TileMap tileMap, out string buildFailure);
        if (true == built)
        {
            result.grid = tileMap.ToTypeGrid();
        }
        stopwatch.Stop();

        result.elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

        if (false == built)
        {
            result.passed = false;
            result.failureReason = $"TryBuild failed: {buildFailure}";
            return result;
        }

        result.passed = Inspect(result.grid, this.checkWallLeak, result.badTiles, out result.failureReason);
        return result;
    }

    /// <summary>
    /// 타일 배열이 하나로 이어져 있는지 검사한다. grid 의 인덱스는 [x, y] 이다.
    /// 문제가 된 타일 좌표는 badTiles 에 담긴다(null 이면 담지 않는다).
    /// </summary>
    public static bool Inspect(TileMap.Tile.Type[,] grid, bool checkWallLeak, List<Vector2Int> badTiles, out string failureReason)
    {
        failureReason = null;
        var reasons = new List<string>();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // 1. 빈 공간이 아닌 타일(바닥 + 벽)은 8방향으로 하나의 덩어리여야 한다.
        //    벽은 모서리에서 대각선으로만 맞닿기도 하므로 8방향으로 잇는다.
        int[,] solidLabels = LabelComponents(grid, type => TileMap.Tile.Type.None != type, Directions8, out List<int> solidSizes);
        if (0 == solidSizes.Count)
        {
            failureReason = "no tile";
            return false;
        }

        if (1 < solidSizes.Count)
        {
            int main = IndexOfMax(solidSizes);
            int isolatedFloor = 0;
            int isolatedWall = 0;
            ForEachTile(width, height, (x, y) =>
            {
                int label = solidLabels[x, y];
                if (0 > label || main == label)
                {
                    return;
                }

                if (TileMap.Tile.Type.Floor == grid[x, y])
                {
                    isolatedFloor++;
                }
                else
                {
                    isolatedWall++;
                }

                badTiles?.Add(new Vector2Int(x, y));
            });

            reasons.Add($"{solidSizes.Count - 1} detached group(s): floor {isolatedFloor}, wall {isolatedWall}");
        }

        // 2. 바닥은 4방향으로 하나의 덩어리여야 한다. 벽에 둘러싸인 바닥 섬은 1번에 걸리지 않는다.
        int[,] floorLabels = LabelComponents(grid, type => TileMap.Tile.Type.Floor == type, Directions4, out List<int> floorSizes);
        if (0 == floorSizes.Count)
        {
            reasons.Add("no floor");
        }
        else if (1 < floorSizes.Count)
        {
            int main = IndexOfMax(floorSizes);
            int unreachable = 0;
            ForEachTile(width, height, (x, y) =>
            {
                int label = floorLabels[x, y];
                if (0 > label || main == label)
                {
                    return;
                }

                unreachable++;
                badTiles?.Add(new Vector2Int(x, y));
            });

            reasons.Add($"{floorSizes.Count - 1} unreachable floor island(s): {unreachable} tile(s)");
        }

        // 3. 바닥이 빈 공간/맵 밖에 바로 닿으면 벽이 빠진 것이다.
        if (true == checkWallLeak)
        {
            int leaks = 0;
            ForEachTile(width, height, (x, y) =>
            {
                if (TileMap.Tile.Type.Floor != grid[x, y])
                {
                    return;
                }

                foreach (Vector2Int offset in Directions8)
                {
                    int nx = x + offset.x;
                    int ny = y + offset.y;
                    bool outside = 0 > nx || nx >= width || 0 > ny || ny >= height;
                    if (true == outside || TileMap.Tile.Type.None == grid[nx, ny])
                    {
                        leaks++;
                        badTiles?.Add(new Vector2Int(x, y));
                        return;
                    }
                }
            });

            if (0 < leaks)
            {
                reasons.Add($"{leaks} floor tile(s) touching void (missing wall)");
            }
        }

        if (0 < reasons.Count)
        {
            failureReason = string.Join(", ", reasons);
            return false;
        }

        return true;
    }

    private static readonly Vector2Int[] Directions4 =
    {
        new Vector2Int( 0, +1),
        new Vector2Int(+1,  0),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
    };

    private static readonly Vector2Int[] Directions8 =
    {
        new Vector2Int(-1, +1), new Vector2Int( 0, +1), new Vector2Int(+1, +1),
        new Vector2Int(-1,  0),                         new Vector2Int(+1,  0),
        new Vector2Int(-1, -1), new Vector2Int( 0, -1), new Vector2Int(+1, -1),
    };

    /// <summary>
    /// include 를 만족하는 타일을 연결 성분별로 번호 매긴다.
    /// 결과 배열의 값은 성분 번호(0부터)이며, 대상이 아닌 타일은 -1 이다. sizes[i] 는 i 번 성분의 타일 수다.
    /// </summary>
    private static int[,] LabelComponents(TileMap.Tile.Type[,] grid, System.Func<TileMap.Tile.Type, bool> include, Vector2Int[] directions, out List<int> sizes)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        var labels = new int[width, height];
        sizes = new List<int>();
        var queue = new Queue<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = -1;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (0 <= labels[x, y] || false == include(grid[x, y]))
                {
                    continue;
                }

                int label = sizes.Count;
                int size = 0;
                labels[x, y] = label;
                queue.Enqueue(new Vector2Int(x, y));

                while (0 < queue.Count)
                {
                    Vector2Int current = queue.Dequeue();
                    size++;

                    foreach (Vector2Int offset in directions)
                    {
                        int nx = current.x + offset.x;
                        int ny = current.y + offset.y;
                        if (0 > nx || nx >= width || 0 > ny || ny >= height)
                        {
                            continue;
                        }

                        if (0 <= labels[nx, ny] || false == include(grid[nx, ny]))
                        {
                            continue;
                        }

                        labels[nx, ny] = label;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                sizes.Add(size);
            }
        }

        return labels;
    }

    private static int IndexOfMax(List<int> values)
    {
        int index = 0;
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] > values[index])
            {
                index = i;
            }
        }

        return index;
    }

    private static void ForEachTile(int width, int height, System.Action<int, int> action)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                action(x, y);
            }
        }
    }

    /// <summary>시드 0 은 "무작위" 를 뜻하므로 만들지 않는다.</summary>
    private static int NewRandomSeed(System.Random source)
    {
        return source.Next(1, int.MaxValue);
    }

    private void Draw(Result result)
    {
        DungeonDebugView debugView = GetView();
        if (null == result || null == result.grid)
        {
            debugView.Clear();
            return;
        }

        debugView.cellPixels = this.cellPixels;
        debugView.Draw(result.grid, result.badTiles);
        debugView.FitCamera(null != this.targetCamera ? this.targetCamera : Camera.main);
    }

    private DungeonDebugView GetView()
    {
        if (null != this.view && null != this.view.gameObject)
        {
            return this.view;
        }

        // 에디트 모드에서 만든 뒤 스크립트가 다시 컴파일되면 참조만 사라지고 오브젝트는 남는다. 정리하고 새로 만든다.
        Transform leftover = this.transform.Find(ViewName);
        if (null != leftover)
        {
            DestroyImmediate(leftover.gameObject);
        }

        this.view = new DungeonDebugView(ViewName, this.transform);

        // 에디트 모드에서 만든 뷰가 씬에 저장되지 않게 한다.
        this.view.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        return this.view;
    }

    private void StopRunning()
    {
        if (null != this.running)
        {
            StopCoroutine(this.running);
            this.running = null;
        }
    }

    private void OnGUI()
    {
        if (true == string.IsNullOrEmpty(this.report))
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, Mathf.Min(700, Screen.width - 20), Screen.height - 20));
        GUILayout.Box(this.report, GUILayout.ExpandWidth(false));
        GUILayout.EndArea();
    }
}
