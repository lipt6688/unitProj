using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// 开局清空原 Tilemap，再铺矩形平地；否则会与手摆的椭圆水池叠在一起，看起来还是「一滩水」。
/// </summary>
[DefaultExecutionOrder(-150)]
public class FlatArenaOnPlay : MonoBehaviour
{
    [SerializeField] private bool fillOnStart = true;
    [SerializeField] private int halfWidthCells = 28;
    [SerializeField] private int halfHeightCells = 20;
    [Tooltip("关=使用 Roguelike 蓝绿等地砖；开=程序灰砖")]
    [SerializeField] private bool usePlainFloorTile = true;
    [SerializeField] private TileBase floorTile;
    [Tooltip("可选地板图样；运行时会按块随机拼接。为空时使用 floorTile 或自动生成。")]
    [SerializeField] private TileBase[] floorPatternTiles;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private bool hideRandomPosObjects = true;
    [SerializeField] private float cameraPadding = 3f;
    [SerializeField] private int patternPatchSize = 4;
    [SerializeField] private int patternSeed = 0;
    [SerializeField] private bool applyFloorTintJitter = true;
    [SerializeField, Range(0f, 0.25f)] private float floorTintJitter = 0.08f;
    [SerializeField] private bool spawnPlayerAtCenter = true;

    [Header("陷阱")]
    [SerializeField] private bool spawnTraps = true;
    [SerializeField, Range(0f, 0.2f)] private float trapDensity = 0.01f;
    [SerializeField] private int minTrapCount = 8;
    [SerializeField] private int maxTrapCount = 60;
    [SerializeField] private int trapSeparationCells = 2;
    [SerializeField] private int trapSafeRadiusFromPlayer = 3;
    [SerializeField, Range(0.1f, 1f)] private float trapRespawnDelay = 0.28f;
    [SerializeField, Range(0.5f, 1f)] private float trapTileFill = 0.92f;

    private static Sprite _trapSprite;

    private void Awake()
    {
        if (!fillOnStart)
            return;

        Transform root = GameObject.Find("Tilemaps")?.transform;
        if (root == null)
            return;

        var ground = root.Find("Ground")?.GetComponent<Tilemap>();
        var wall = root.Find("Wall")?.GetComponent<Tilemap>();
        var grid = root.GetComponent<Grid>();
        if (ground == null || wall == null || grid == null)
            return;

        TileBase edgeSource = wallTile ?? SampleAnyTile(wall);
        TileBase floorSource = usePlainFloorTile ? null : (floorTile ?? SampleAnyTile(ground));

        ground.ClearAllTiles();
        wall.ClearAllTiles();

        Transform existingTraps = root.Find("ArenaTraps");
        if (existingTraps != null)
            Destroy(existingTraps.gameObject);

        TileBase[] floorVariants = BuildFloorVariants(floorSource);
        TileBase edge = edgeSource;
        if ((floorVariants == null || floorVariants.Length == 0) || edge == null)
        {
            Debug.LogWarning("FlatArenaOnPlay: 无法得到地板/墙砖，跳过重铺。");
            return;
        }

        int seed = patternSeed != 0 ? patternSeed : Random.Range(1, int.MaxValue);
        int patch = Mathf.Max(1, patternPatchSize);
        float jitter = Mathf.Clamp(floorTintJitter, 0f, 0.25f);

        for (int x = -halfWidthCells; x <= halfWidthCells; x++)
        {
            for (int y = -halfHeightCells; y <= halfHeightCells; y++)
            {
                Vector3Int c = new Vector3Int(x, y, 0);
                bool border = x == -halfWidthCells || x == halfWidthCells ||
                              y == -halfHeightCells || y == halfHeightCells;
                ground.SetTile(c, PickPatternTile(floorVariants, x, y, patch, seed));
                if (applyFloorTintJitter)
                    ApplyFloorTint(ground, c, x, y, patch, seed, jitter);
                wall.SetTile(c, border ? edge : null);
            }
        }

        ground.CompressBounds();
        wall.CompressBounds();

        PositionPlayerAtCenter(grid);

        BuildArenaTraps(root, grid, ground.GetComponent<TilemapRenderer>(), seed);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<PlayerTrapResponder>() == null)
            player.AddComponent<PlayerTrapResponder>();

        if (hideRandomPosObjects)
        {
            GameObject rp = GameObject.Find("Random Pos");
            if (rp != null)
                rp.SetActive(false);
        }

        CameraController cam = FindObjectOfType<CameraController>();
        if (cam != null)
        {
            Vector3Int mn = new Vector3Int(-halfWidthCells, -halfHeightCells, 0);
            Vector3Int mx = new Vector3Int(halfWidthCells, halfHeightCells, 0);
            Vector3 w0 = grid.CellToWorld(mn);
            Vector3 w1 = grid.CellToWorld(mx) + new Vector3(grid.cellSize.x, grid.cellSize.y, 0f);
            cam.SetWorldBounds(
                Mathf.Min(w0.x, w1.x) - cameraPadding,
                Mathf.Max(w0.x, w1.x) + cameraPadding,
                Mathf.Min(w0.y, w1.y) - cameraPadding,
                Mathf.Max(w0.y, w1.y) + cameraPadding);
        }
    }

    private void BuildArenaTraps(Transform root, Grid grid, TilemapRenderer groundRenderer, int seed)
    {
        if (!spawnTraps)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3Int playerCell = player != null ? grid.WorldToCell(player.transform.position) : Vector3Int.zero;

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = -halfWidthCells + 1; x <= halfWidthCells - 1; x++)
        {
            for (int y = -halfHeightCells + 1; y <= halfHeightCells - 1; y++)
            {
                Vector3Int c = new Vector3Int(x, y, 0);
                if (Mathf.Abs(c.x - playerCell.x) + Mathf.Abs(c.y - playerCell.y) <= trapSafeRadiusFromPlayer)
                    continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count == 0)
            return;

        int upper = Mathf.Min(maxTrapCount, candidates.Count);
        int desired = Mathf.RoundToInt(candidates.Count * trapDensity);
        int targetCount = Mathf.Clamp(desired, Mathf.Min(minTrapCount, upper), upper);
        if (targetCount <= 0)
            return;

        System.Random rng = new System.Random(seed ^ 0x6E4A91D3);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int t = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = t;
        }

        List<Vector2Int> selected = new List<Vector2Int>(targetCount);
        int minGap = Mathf.Max(0, trapSeparationCells);
        for (int i = 0; i < candidates.Count && selected.Count < targetCount; i++)
        {
            Vector2Int cand = candidates[i];
            if (minGap > 0 && IsNearAnyTrap(cand, selected, minGap))
                continue;
            selected.Add(cand);
        }

        if (selected.Count == 0)
        {
            Debug.LogWarning("FlatArenaOnPlay: trap candidate selection produced 0 traps.");
            return;
        }

        GameObject trapsRoot = new GameObject("ArenaTraps");
        trapsRoot.transform.SetParent(root, false);

        int sortingOrder = (groundRenderer != null ? groundRenderer.sortingOrder : 0) + 2;
        float cellSize = grid.cellSize.x;
        float visualScale = Mathf.Max(0.2f, cellSize * trapTileFill);

        for (int i = 0; i < selected.Count; i++)
        {
            Vector2Int c = selected[i];
            Vector3 cellCenter = grid.GetCellCenterWorld(new Vector3Int(c.x, c.y, 0));

            GameObject trap = new GameObject("Trap_" + c.x + "_" + c.y);
            trap.transform.SetParent(trapsRoot.transform, false);
            trap.transform.position = new Vector3(cellCenter.x, cellCenter.y, 0f);
            trap.transform.localScale = new Vector3(visualScale, visualScale, 1f);

            SpriteRenderer sr = trap.AddComponent<SpriteRenderer>();
            sr.sprite = TrapVisualFactory.GetSpikeSprite();
            sr.color = Color.white;
            if (groundRenderer != null)
                sr.sortingLayerID = groundRenderer.sortingLayerID;
            sr.sortingOrder = sortingOrder;

            BoxCollider2D box = trap.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = Vector2.one * 0.92f;

            TrapHazard hazard = trap.AddComponent<TrapHazard>();
            hazard.playerRespawnDelay = trapRespawnDelay;
        }

        Debug.Log("FlatArenaOnPlay: generated traps = " + selected.Count);
    }

    private void PositionPlayerAtCenter(Grid grid)
    {
        if (!spawnPlayerAtCenter || grid == null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Vector3 center = grid.GetCellCenterWorld(Vector3Int.zero);
        Vector3 p = player.transform.position;
        player.transform.position = new Vector3(center.x, center.y, p.z);
    }

    private static bool IsNearAnyTrap(Vector2Int cell, List<Vector2Int> selected, int radius)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            Vector2Int v = selected[i];
            if (Mathf.Abs(cell.x - v.x) + Mathf.Abs(cell.y - v.y) <= radius)
                return true;
        }
        return false;
    }

    private static Sprite GetTrapSprite()
    {
        if (_trapSprite != null)
            return _trapSprite;

        const int s = 16;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                t.SetPixel(x, y, Color.white);
        t.Apply();

        _trapSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _trapSprite;
    }

    private static TileBase SampleAnyTile(Tilemap map)
    {
        foreach (Vector3Int p in map.cellBounds.allPositionsWithin)
        {
            TileBase t = map.GetTile(p);
            if (t != null)
                return t;
        }
        return null;
    }

    private TileBase[] BuildFloorVariants(TileBase fallback)
    {
        List<TileBase> variants = new List<TileBase>();

        if (floorPatternTiles != null)
        {
            for (int i = 0; i < floorPatternTiles.Length; i++)
            {
                if (floorPatternTiles[i] != null)
                    variants.Add(floorPatternTiles[i]);
            }
        }

        if (variants.Count == 0)
        {
            if (usePlainFloorTile)
            {
                Color basalt = new Color(0.10f, 0.08f, 0.08f, 1f);
                variants.Add(CreatePlainFloorTile(basalt, 0));
                variants.Add(CreatePlainFloorTile(new Color(0.16f, 0.11f, 0.10f, 1f), 1));
                variants.Add(CreatePlainFloorTile(new Color(0.27f, 0.12f, 0.07f, 1f), 2));
                variants.Add(CreatePlainFloorTile(new Color(0.74f, 0.22f, 0.06f, 1f), 3));
            }
            else if (fallback != null)
            {
                variants.Add(fallback);
            }
        }

        if (variants.Count == 1)
            AddTintedTileVariants(variants, variants[0]);

        return variants.ToArray();
    }

    private static TileBase PickPatternTile(TileBase[] variants, int x, int y, int patch, int seed)
    {
        if (variants.Length == 1)
            return variants[0];

        int blockX = Mathf.FloorToInt((float)x / patch);
        int blockY = Mathf.FloorToInt((float)y / patch);

        int coarseHash = StableHash(blockX, blockY, seed);
        int idx = PositiveModulo(coarseHash, variants.Length);

        float jitter = Hash01(x, y, seed + 97);
        if (jitter > 0.84f)
            idx = PositiveModulo(idx + 1 + (coarseHash >> 4), variants.Length);

        return variants[idx];
    }

    private static void ApplyFloorTint(Tilemap tilemap, Vector3Int cell, int x, int y, int patch, int seed, float jitter)
    {
        int blockX = Mathf.FloorToInt((float)x / patch);
        int blockY = Mathf.FloorToInt((float)y / patch);

        float blockV = Hash01(blockX, blockY, seed + 31) * 2f - 1f;
        float microV = Hash01(x, y, seed + 79) * 2f - 1f;
        float delta = (blockV * 0.65f + microV * 0.35f) * jitter;

        float v = Mathf.Clamp(1f + delta, 0.72f, 1.28f);
        tilemap.RemoveTileFlags(cell, TileFlags.LockColor);
        tilemap.SetColor(cell, new Color(v, v, v, 1f));
    }

    private static void AddTintedTileVariants(List<TileBase> variants, TileBase source)
    {
        if (!(source is Tile srcTile))
            return;

        Tile v1 = CloneTile(srcTile, new Color(0.82f, 0.58f, 0.42f, 1f));
        Tile v2 = CloneTile(srcTile, new Color(1.16f, 0.66f, 0.34f, 1f));
        Tile v3 = CloneTile(srcTile, new Color(0.95f, 0.43f, 0.24f, 1f));
        variants.Add(v1);
        variants.Add(v2);
        variants.Add(v3);
    }

    private static Tile CloneTile(Tile src, Color color)
    {
        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = src.sprite;
        t.color = color;
        t.flags = src.flags;
        t.colliderType = src.colliderType;
        t.transform = src.transform;
        t.gameObject = src.gameObject;
        return t;
    }

    private static Tile CreatePlainFloorTile(Color c, int style)
    {
        const int n = 16;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v;
                switch (style)
                {
                    case 1:
                        v = (((x / 2) + (y / 2)) % 2 == 0) ? 0.05f : -0.01f;
                        break;
                    case 2:
                        v = ((x + y) % 5 == 0) ? 0.06f : (((x - y) % 7 == 0) ? -0.03f : 0f);
                        break;
                    case 3:
                        v = ((x % 4 == 0) || (y % 4 == 0)) ? 0.03f : -0.01f;
                        break;
                    default:
                        v = ((x + y) % 4 == 0) ? 0.04f : 0f;
                        break;
                }

                tex.SetPixel(
                    x,
                    y,
                    new Color(
                        Mathf.Clamp01(c.r + v),
                        Mathf.Clamp01(c.g + v),
                        Mathf.Clamp01(c.b + v),
                        1f));
            }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s;
        t.colliderType = Tile.ColliderType.None;
        return t;
    }

    private static int StableHash(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed;
            h ^= x * 374761393;
            h = (h << 13) | (int)((uint)h >> 19);
            h = h * 1274126177;
            h ^= y * 668265263;
            h = (h << 11) | (int)((uint)h >> 21);
            h *= 461845907;
            return h;
        }
    }

    private static float Hash01(int x, int y, int seed)
    {
        int h = StableHash(x, y, seed);
        uint u = (uint)h;
        return (u & 0x00FFFFFFu) / 16777215f;
    }

    private static int PositiveModulo(int value, int mod)
    {
        int r = value % mod;
        return r < 0 ? r + mod : r;
    }
}
