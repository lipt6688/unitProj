using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>用与关卡相同的 Tilemap 格子逐格铺出迷宫；关闭旧「水池」地面与波次点。</summary>
[DefaultExecutionOrder(-100)]
public class DungeonBootstrapper : MonoBehaviour
{
    public static DungeonBootstrapper Instance { get; private set; }
    public bool HasBuilt { get; private set; }

    [Header("生成")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject wizardPrefab;
    [SerializeField] private int randomSeed;
    [SerializeField] private int gridWidth = 72;
    [SerializeField] private int gridHeight = 48;
    [SerializeField] private int roomPlacementTries = 14;
    [SerializeField] private int minRoomW = 10, maxRoomW = 18;
    [SerializeField] private int minRoomH = 8, maxRoomH = 14;
    [SerializeField] private int nestCount = 4;

    [Header("新增怪物外观（Monsters Creatures Fantasy）")]
    [SerializeField] private bool autoLoadFantasyMonsterAssets = true;
    [SerializeField] private RuntimeAnimatorController[] fantasyEnemyControllers;
    [SerializeField] private Sprite[] fantasyEnemyFallbackSprites;

    [Header("瓦片（与 Ground / Wall 图集一致，默认已填 Roguelike）")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase[] floorPatternTiles;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private int patternPatchSize = 5;
    [SerializeField] private bool applyFloorTintJitter = true;
    [SerializeField, Range(0f, 0.25f)] private float floorTintJitter = 0.08f;

    [Header("陷阱")]
    [SerializeField] private bool spawnTraps = true;
    [SerializeField, Range(0f, 0.2f)] private float trapDensity = 0.01f;
    [SerializeField] private int minTrapCount = 8;
    [SerializeField] private int maxTrapCount = 60;
    [SerializeField] private int trapSeparationCells = 2;
    [SerializeField] private int trapSafeRadiusFromSpawn = 3;
    [SerializeField, Range(0.1f, 1f)] private float trapRespawnDelay = 0.28f;
    [SerializeField, Range(0.5f, 1f)] private float trapTileFill = 0.92f;

    private Transform _actorsRoot;
    private static Sprite _exitSprite;
    private static Sprite _trapSprite;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
#if UNITY_EDITOR
        TryAutoLoadFantasyMonsterAssetsInEditor();
#endif

        if (!generateOnStart)
            return;
        if (enemyPrefab == null || wizardPrefab == null)
        {
            Debug.LogWarning("DungeonBootstrapper: 未指定敌人预制体，跳过生成。");
            return;
        }
        if ((floorTile == null && !HasAnyPatternFloor()) || wallTile == null)
        {
            Debug.LogError("DungeonBootstrapper: 请在 Inspector 指定 floorTile / wallTile（Roguelike 地板与墙砖）。");
            return;
        }

        var em = FindObjectOfType<EnemyManager>();
        if (em != null)
            em.enabled = false;

        Transform tilemapsRoot = GameObject.Find("Tilemaps")?.transform;
        if (tilemapsRoot == null)
        {
            Debug.LogError("DungeonBootstrapper: 未找到 Tilemaps。");
            return;
        }

        Transform ground = tilemapsRoot.Find("Ground");
        Transform oldWall = tilemapsRoot.Find("Wall");
        TilemapRenderer groundRend = ground != null ? ground.GetComponent<TilemapRenderer>() : null;

        for (int i = 0; i < tilemapsRoot.childCount; i++)
        {
            Transform c = tilemapsRoot.GetChild(i);
            if (c.name == "Ground" || c.name == "Wall")
                c.gameObject.SetActive(false);
        }

        GameObject rp = GameObject.Find("Random Pos");
        if (rp != null)
            rp.SetActive(false);

        Grid grid = tilemapsRoot.GetComponent<Grid>();
        if (grid == null)
        {
            Debug.LogError("DungeonBootstrapper: Tilemaps 上缺少 Grid。");
            return;
        }

        Transform existing = tilemapsRoot.Find("MazeFloor");
        if (existing != null)
            Destroy(existing.gameObject);
        existing = tilemapsRoot.Find("MazeWall");
        if (existing != null)
            Destroy(existing.gameObject);
        existing = tilemapsRoot.Find("MazeTraps");
        if (existing != null)
            Destroy(existing.gameObject);

        BuildDungeon(grid, tilemapsRoot, groundRend);
        HasBuilt = true;
    }

    private void BuildDungeon(Grid grid, Transform tilemapsRoot, TilemapRenderer groundRend)
    {
        if (randomSeed == 0)
            randomSeed = Random.Range(1, int.MaxValue);

        var map = DungeonGenerator.Generate(gridWidth, gridHeight, roomPlacementTries, minRoomW, maxRoomW, minRoomH, maxRoomH, nestCount, randomSeed);
        if (map.Floor == null || map.Width <= 0)
        {
            Debug.LogError("DungeonBootstrapper: 迷宫生成失败。");
            return;
        }

        float cs = grid.cellSize.x;
        float ox = -map.Width * cs * 0.5f;
        float oy = -map.Height * cs * 0.5f;
        Vector3Int originCell = grid.WorldToCell(new Vector3(ox, oy, 0f));

        GameObject floorGo = new GameObject("MazeFloor");
        floorGo.transform.SetParent(tilemapsRoot, false);
        var floorTm = floorGo.AddComponent<Tilemap>();
        var floorTr = floorGo.AddComponent<TilemapRenderer>();
        if (groundRend != null)
        {
            floorTr.sortingLayerID = groundRend.sortingLayerID;
            floorTr.sortingOrder = groundRend.sortingOrder;
        }

        GameObject wallGo = new GameObject("MazeWall");
        wallGo.transform.SetParent(tilemapsRoot, false);
        wallGo.tag = "Wall";
        var wallTm = wallGo.AddComponent<Tilemap>();
        var wallTr = wallGo.AddComponent<TilemapRenderer>();
        if (groundRend != null)
        {
            wallTr.sortingLayerID = groundRend.sortingLayerID;
            wallTr.sortingOrder = groundRend.sortingOrder + 1;
        }
        var wallRb = wallGo.AddComponent<Rigidbody2D>();
        wallRb.bodyType = RigidbodyType2D.Static;
        wallRb.simulated = true;
        var wallTc = wallGo.AddComponent<TilemapCollider2D>();
        wallTc.usedByComposite = true;
        wallGo.AddComponent<CompositeCollider2D>();

        int w = map.Width, h = map.Height;
        TileBase[] floorVariants = BuildFloorVariants();
        int patch = Mathf.Max(1, patternPatchSize);
        float jitter = Mathf.Clamp(floorTintJitter, 0f, 0.25f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector3Int cell = originCell + new Vector3Int(x, y, 0);
                if (map.Floor[x + y * w])
                {
                    floorTm.SetTile(cell, PickPatternTile(floorVariants, x, y, patch, randomSeed));
                    if (applyFloorTintJitter)
                        ApplyFloorTint(floorTm, cell, x, y, patch, randomSeed, jitter);
                }
                else
                    wallTm.SetTile(cell, wallTile);
            }
        }

        floorTm.CompressBounds();
        wallTm.CompressBounds();

        _actorsRoot = new GameObject("DungeonActors").transform;
        _actorsRoot.position = Vector3.zero;

        Vector3 spawn = grid.GetCellCenterWorld(originCell + new Vector3Int(map.SpawnCell.x, map.SpawnCell.y, 0));
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(spawn.x, spawn.y, player.transform.position.z);
            if (player.GetComponent<PlayerTrapResponder>() == null)
                player.AddComponent<PlayerTrapResponder>();
        }

        BuildTrapCells(grid, tilemapsRoot, groundRend, map, originCell, cs);

        foreach (Vector2Int nc in map.NestCells)
        {
            Vector3 pos = grid.GetCellCenterWorld(originCell + new Vector3Int(nc.x, nc.y, 0));
            GameObject nestGo = new GameObject("MonsterNest");
            nestGo.transform.SetParent(_actorsRoot);
            nestGo.transform.position = pos;
            MonsterNest nest = nestGo.AddComponent<MonsterNest>();
            nest.BindPrefabs(enemyPrefab, wizardPrefab);
            nest.BindEnemyVisualVariants(fantasyEnemyControllers, fantasyEnemyFallbackSprites);
        }

        Vector3 exitW = grid.GetCellCenterWorld(originCell + new Vector3Int(map.ExitCell.x, map.ExitCell.y, 0));
        GameObject exitGo = new GameObject("LevelExit");
        exitGo.transform.SetParent(_actorsRoot);
        exitGo.transform.position = exitW;
        float ex = cs * 2f;
        exitGo.transform.localScale = new Vector3(ex, ex, 1f);
        var col = exitGo.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;
        exitGo.AddComponent<LevelExit>();
        var exitSr = exitGo.AddComponent<SpriteRenderer>();
        exitSr.sprite = GetExitSprite();
        exitSr.sortingOrder = (groundRend != null ? groundRend.sortingOrder : 0) + 3;

        CameraController cam = FindObjectOfType<CameraController>();
        if (cam != null)
        {
            float pad = 4f;
            cam.SetWorldBounds(ox - pad, ox + map.Width * cs + pad, oy - pad, oy + map.Height * cs + pad);
        }

        if (UIManager.instance != null && UIManager.instance.waveText != null)
            UIManager.instance.waveText.text = "抵达绿色出口";
    }

    private bool HasAnyPatternFloor()
    {
        if (floorPatternTiles == null)
            return false;

        for (int i = 0; i < floorPatternTiles.Length; i++)
        {
            if (floorPatternTiles[i] != null)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoLoadFantasyMonsterAssetsInEditor();
    }

    private void TryAutoLoadFantasyMonsterAssetsInEditor()
    {
        if (!autoLoadFantasyMonsterAssets)
            return;

        if (HasAnyFantasyAssetAssigned())
            return;

        string[] controllerPaths = new string[]
        {
            "Assets/Monsters Creatures Fantasy/Animations/Flying eye/Flight_0.controller",
            "Assets/Monsters Creatures Fantasy/Animations/Goblin/Idle_0.controller",
            "Assets/Monsters Creatures Fantasy/Animations/Mushroom/Idle_0.controller",
            "Assets/Monsters Creatures Fantasy/Animations/Skeleton/Idle_0.controller"
        };

        string[] spritePaths = new string[]
        {
            "Assets/Monsters Creatures Fantasy/Sprites/Flying eye/Flight.png",
            "Assets/Monsters Creatures Fantasy/Sprites/Goblin/Idle.png",
            "Assets/Monsters Creatures Fantasy/Sprites/Mushroom/Idle.png",
            "Assets/Monsters Creatures Fantasy/Sprites/Skeleton/Idle.png"
        };

        List<RuntimeAnimatorController> controllers = new List<RuntimeAnimatorController>();
        for (int i = 0; i < controllerPaths.Length; i++)
        {
            RuntimeAnimatorController ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPaths[i]);
            if (ctrl != null)
                controllers.Add(ctrl);
        }

        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < spritePaths.Length; i++)
        {
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[i]);
            if (sprite != null)
                sprites.Add(sprite);
        }

        fantasyEnemyControllers = controllers.ToArray();
        fantasyEnemyFallbackSprites = sprites.ToArray();
    }

    private bool HasAnyFantasyAssetAssigned()
    {
        if (fantasyEnemyControllers != null)
        {
            for (int i = 0; i < fantasyEnemyControllers.Length; i++)
            {
                if (fantasyEnemyControllers[i] != null)
                    return true;
            }
        }

        if (fantasyEnemyFallbackSprites != null)
        {
            for (int i = 0; i < fantasyEnemyFallbackSprites.Length; i++)
            {
                if (fantasyEnemyFallbackSprites[i] != null)
                    return true;
            }
        }

        return false;
    }
#endif

    private void BuildTrapCells(Grid grid, Transform tilemapsRoot, TilemapRenderer groundRend, DungeonGenerator.Result map, Vector3Int originCell, float cellSize)
    {
        if (!spawnTraps)
            return;

        List<Vector2Int> candidates = new List<Vector2Int>();
        int w = map.Width;
        int h = map.Height;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!map.Floor[x + y * w])
                    continue;

                Vector2Int c = new Vector2Int(x, y);
                if (c == map.SpawnCell || c == map.ExitCell)
                    continue;
                if (ManhattanDistance(c, map.SpawnCell) <= trapSafeRadiusFromSpawn)
                    continue;
                if (IsNearAnyNest(c, map.NestCells, 2))
                    continue;

                candidates.Add(c);
            }
        }

        if (candidates.Count == 0)
            return;

        int upper = Mathf.Min(maxTrapCount, candidates.Count);
        int desired = Mathf.RoundToInt(candidates.Count * trapDensity);
        int targetCount = Mathf.Clamp(desired, Mathf.Min(minTrapCount, upper), upper);
        if (targetCount <= 0)
            return;

        System.Random rng = new System.Random(randomSeed ^ 0x2A93B5D7);
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
            Debug.LogWarning("DungeonBootstrapper: trap candidate selection produced 0 traps.");
            return;
        }

        GameObject trapsRoot = new GameObject("MazeTraps");
        trapsRoot.transform.SetParent(tilemapsRoot, false);

        int sortingOrder = (groundRend != null ? groundRend.sortingOrder : 0) + 2;
        float visualScale = Mathf.Max(0.2f, cellSize * trapTileFill);

        for (int i = 0; i < selected.Count; i++)
        {
            Vector2Int c = selected[i];
            Vector3 world = grid.GetCellCenterWorld(originCell + new Vector3Int(c.x, c.y, 0));

            GameObject trap = new GameObject("Trap_" + c.x + "_" + c.y);
            trap.transform.SetParent(trapsRoot.transform, false);
            trap.transform.position = new Vector3(world.x, world.y, 0f);
            trap.transform.localScale = new Vector3(visualScale, visualScale, 1f);

            SpriteRenderer sr = trap.AddComponent<SpriteRenderer>();
            sr.sprite = TrapVisualFactory.GetSpikeSprite();
            sr.color = Color.white;
            if (groundRend != null)
                sr.sortingLayerID = groundRend.sortingLayerID;
            sr.sortingOrder = sortingOrder;

            BoxCollider2D box = trap.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = Vector2.one * 0.92f;

            TrapHazard hazard = trap.AddComponent<TrapHazard>();
            hazard.playerRespawnDelay = trapRespawnDelay;
        }

        Debug.Log("DungeonBootstrapper: generated traps = " + selected.Count);
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static bool IsNearAnyNest(Vector2Int cell, List<Vector2Int> nests, int radius)
    {
        if (nests == null)
            return false;

        for (int i = 0; i < nests.Count; i++)
        {
            if (ManhattanDistance(cell, nests[i]) <= radius)
                return true;
        }

        return false;
    }

    private static bool IsNearAnyTrap(Vector2Int cell, List<Vector2Int> selected, int radius)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            if (ManhattanDistance(cell, selected[i]) <= radius)
                return true;
        }

        return false;
    }

    private TileBase[] BuildFloorVariants()
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

        if (variants.Count == 0 && floorTile != null)
            variants.Add(floorTile);

        if (variants.Count == 1)
            AddTintedTileVariants(variants, variants[0]);

        return variants.ToArray();
    }

    private static TileBase PickPatternTile(TileBase[] variants, int x, int y, int patch, int seed)
    {
        if (variants.Length == 0)
            return null;
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

        variants.Add(CloneTile(srcTile, new Color(0.90f, 0.96f, 0.90f, 1f)));
        variants.Add(CloneTile(srcTile, new Color(1.10f, 1.06f, 1.00f, 1f)));
        variants.Add(CloneTile(srcTile, new Color(0.96f, 0.92f, 1.04f, 1f)));
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

    private static Sprite GetExitSprite()
    {
        if (_exitSprite != null)
            return _exitSprite;
        const int s = 32;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s * 0.5f) / (s * 0.5f);
                float dy = (y - s * 0.5f) / (s * 0.5f);
                if (dx * dx + dy * dy < 0.85f)
                    t.SetPixel(x, y, new Color(0.15f, 0.65f, 0.28f, 0.95f));
                else
                    t.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        t.Apply();
        _exitSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _exitSprite;
    }

    private static Sprite GetTrapSprite()
    {
        if (_trapSprite != null)
            return _trapSprite;

        const int s = 16;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                t.SetPixel(x, y, Color.white);
            }
        }
        t.Apply();
        _trapSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _trapSprite;
    }
}
