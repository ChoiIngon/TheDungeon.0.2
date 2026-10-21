using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 별도 그래픽 리소스 없이 타일 맵을 화면에 그려 주는 디버그용 헬퍼.
///
/// 타일 하나를 cellPixels × cellPixels 픽셀로 칠한 텍스처를 런타임에 만들고, 스프라이트 하나로 띄운다.
/// 타일마다 GameObject 를 만들지 않으므로 큰 맵이나 반복 갱신에도 가볍다.
///
/// 좌표 규약: 월드 1유닛 = 타일 1칸이며, 타일 (x, y) 는 월드 [x, x+1) × [y, y+1) 을 덮는다.
///
/// 색 규약 (참고: ChoiIngon/blog 196 DungeonGizmo)
///   None  : 검정
///   Floor : 회색
///   Wall  : 노랑
///   표시 타일(공유 벽 등)   : 하늘색
///   두 번째 표시 타일(두 겹 벽 등) : 자홍
///   강조 타일(검증 실패 등) : 빨강 (표시보다 우선)
/// </summary>
public class DungeonDebugView
{
    public static readonly Color NoneColor = Color.black;
    public static readonly Color FloorColor = new Color(0.66f, 0.66f, 0.66f);
    public static readonly Color WallColor = new Color(0.70f, 0.62f, 0.0f);
    public static readonly Color HighlightColor = Color.red;
    public static readonly Color MarkColor = new Color(0.2f, 0.75f, 1.0f);
    public static readonly Color SubMarkColor = new Color(1.0f, 0.2f, 0.85f);
    public static readonly Color GridColor = new Color(0.5f, 0.5f, 0.5f);
    public static readonly Color BorderColor = Color.white;

    /// <summary>텍스처 한 변의 최대 크기. 맵이 커지면 cellPixels 를 줄여 이 안에 맞춘다.</summary>
    private const int MaxTextureSize = 8192;

    private readonly GameObject root;
    private readonly SpriteRenderer spriteRenderer;
    private Texture2D texture;
    private Sprite sprite;

    /// <summary>타일 한 칸을 그릴 픽셀 수. 가장자리 1픽셀은 격자선이다.</summary>
    public int cellPixels = 8;

    /// <summary>격자선을 그린다.</summary>
    public bool drawGrid = true;

    public GameObject gameObject => this.root;

    /// <summary>마지막으로 그린 맵의 월드 영역.</summary>
    public Rect Bounds { get; private set; }

    public DungeonDebugView(string name = "DungeonDebugView", Transform parent = null)
    {
        this.root = new GameObject(name);
        this.root.transform.SetParent(parent, false);
        this.spriteRenderer = this.root.AddComponent<SpriteRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (null == shader)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (null != shader)
        {
            this.spriteRenderer.sharedMaterial = new Material(shader);
        }
    }

    /// <summary>
    /// 타일 배열을 그린다. grid 의 인덱스는 [x, y] 이다(<see cref="TileMap.ToTypeGrid"/> 와 같다).
    /// highlights 에 담긴 좌표는 타입과 상관없이 강조색으로 칠한다.
    /// marks / subMarks 는 검증 실패가 아닌 참고용 표시이며, 각각 MarkColor / SubMarkColor 로 칠한다.
    /// </summary>
    public void Draw(TileMap.Tile.Type[,] grid, IEnumerable<Vector2Int> highlights = null, IEnumerable<Vector2Int> marks = null, IEnumerable<Vector2Int> subMarks = null)
    {
        if (null == grid)
        {
            Clear();
            return;
        }

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        if (0 == width || 0 == height)
        {
            Clear();
            return;
        }

        int cell = Mathf.Max(1, Mathf.Min(this.cellPixels, MaxTextureSize / Mathf.Max(width, height)));
        bool grid1px = this.drawGrid && 3 <= cell;

        HashSet<Vector2Int> highlightSet = (null != highlights) ? new HashSet<Vector2Int>(highlights) : null;
        HashSet<Vector2Int> markSet = (null != marks) ? new HashSet<Vector2Int>(marks) : null;
        HashSet<Vector2Int> subMarkSet = (null != subMarks) ? new HashSet<Vector2Int>(subMarks) : null;

        int textureWidth = width * cell + (grid1px ? 1 : 0);
        int textureHeight = height * cell + (grid1px ? 1 : 0);
        var pixels = new Color32[textureWidth * textureHeight];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var position = new Vector2Int(x, y);
                Color32 color = GetTileColor(grid[x, y]);
                if (null != highlightSet && highlightSet.Contains(position))
                {
                    color = HighlightColor;
                }
                else if (null != subMarkSet && subMarkSet.Contains(position))
                {
                    color = SubMarkColor;
                }
                else if (null != markSet && markSet.Contains(position))
                {
                    color = MarkColor;
                }

                for (int py = 0; py < cell; py++)
                {
                    int row = (y * cell + py) * textureWidth;
                    for (int px = 0; px < cell; px++)
                    {
                        // 셀의 왼쪽/아래 1픽셀이 격자선이다. 오른쪽/위 끝은 아래의 테두리가 닫는다.
                        bool isGridLine = grid1px && (0 == px || 0 == py);
                        pixels[row + x * cell + px] = isGridLine ? (Color32)GridColor : color;
                    }
                }
            }
        }

        if (true == grid1px)
        {
            // 맵 바깥 테두리. 참고 이미지처럼 맵 경계를 분명히 보여 준다.
            for (int px = 0; px < textureWidth; px++)
            {
                pixels[px] = BorderColor;
                pixels[(textureHeight - 1) * textureWidth + px] = BorderColor;
            }

            for (int py = 0; py < textureHeight; py++)
            {
                pixels[py * textureWidth] = BorderColor;
                pixels[py * textureWidth + textureWidth - 1] = BorderColor;
            }
        }

        ReleaseTexture();

        this.texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        this.texture.name = "DungeonDebugView";
        this.texture.filterMode = FilterMode.Point;
        this.texture.wrapMode = TextureWrapMode.Clamp;
        this.texture.SetPixels32(pixels);
        this.texture.Apply(false, false);

        // pixelsPerUnit = cell 이므로 타일 1칸 = 1유닛. 피벗은 왼쪽 아래(0,0).
        this.sprite = Sprite.Create(this.texture, new Rect(0, 0, textureWidth, textureHeight), Vector2.zero, cell, 0, SpriteMeshType.FullRect);
        this.sprite.name = "DungeonDebugView";
        this.spriteRenderer.sprite = this.sprite;

        this.Bounds = new Rect(0, 0, width, height);
    }

    /// <summary>타일 맵을 그린다.</summary>
    public void Draw(TileMap tileMap, IEnumerable<Vector2Int> highlights = null)
    {
        Draw(tileMap?.ToTypeGrid(), highlights);
    }

    /// <summary>카메라를 맵 전체가 보이도록 맞춘다(직교 투영으로 바꾼다).</summary>
    public void FitCamera(Camera camera, float margin = 1.0f)
    {
        if (null == camera || 0 == this.Bounds.width || 0 == this.Bounds.height)
        {
            return;
        }

        Vector3 center = this.root.transform.TransformPoint(this.Bounds.center);
        camera.orthographic = true;
        camera.transform.SetPositionAndRotation(new Vector3(center.x, center.y, center.z - 10.0f), Quaternion.identity);

        float halfHeight = this.Bounds.height * 0.5f + margin;
        float halfWidth = this.Bounds.width * 0.5f + margin;
        camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, camera.aspect));
    }

    public void Clear()
    {
        this.spriteRenderer.sprite = null;
        ReleaseTexture();
        this.Bounds = new Rect();
    }

    public void Destroy()
    {
        Clear();
        if (null != this.spriteRenderer.sharedMaterial)
        {
            DestroyObject(this.spriteRenderer.sharedMaterial);
        }

        DestroyObject(this.root);
    }

    private static Color32 GetTileColor(TileMap.Tile.Type type)
    {
        switch (type)
        {
            case TileMap.Tile.Type.Floor: return FloorColor;
            case TileMap.Tile.Type.Wall: return WallColor;
            default: return NoneColor;
        }
    }

    private void ReleaseTexture()
    {
        if (null != this.sprite)
        {
            DestroyObject(this.sprite);
            this.sprite = null;
        }

        if (null != this.texture)
        {
            DestroyObject(this.texture);
            this.texture = null;
        }
    }

    private static void DestroyObject(Object obj)
    {
        if (true == Application.isPlaying)
        {
            Object.Destroy(obj);
        }
        else
        {
            Object.DestroyImmediate(obj);
        }
    }
}
