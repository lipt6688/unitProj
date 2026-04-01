using UnityEngine;
using UnityEngine.Tilemaps;

namespace Vampire
{
    public class WorldMapCoordinator : MonoBehaviour
    {
        [SerializeField] private WorldMapConfig config;

        private InfiniteBackground infiniteBackground;
        private Grid grid;

        private Tilemap wallTilemap;
        private Tile wallTile;

        private int mazeW;
        private int mazeH;

        private readonly Vector2[] kingCityCentersWorld = new Vector2[3];

        public WorldMapConfig Config => config;

        public void Configure(WorldMapConfig worldMapConfig, InfiniteBackground background)
        {
            config = worldMapConfig;
            infiniteBackground = background;
            grid = infiniteBackground != null ? infiniteBackground.WorldGrid : null;
        }

        public Vector2 GetKingCityCenterWorld(MonsterWorldKind world)
        {
            return kingCityCentersWorld[(int)world];
        }

        public Rect GetKingCityInnerRectWorld(MonsterWorldKind world, float extraInsetCells = 0f)
        {
            Vector2 center = kingCityCentersWorld[(int)world];
            float cell = infiniteBackground != null ? Mathf.Max(0.01f, infiniteBackground.TileCellScale) : 1f;

            float extX = mazeW * cell * 0.5f;
            float extY = mazeH * cell * 0.5f;
            float inset = (1f + Mathf.Max(0f, extraInsetCells)) * cell;

            float xMin = center.x - extX + inset;
            float xMax = center.x + extX - inset;
            float yMin = center.y - extY + inset;
            float yMax = center.y + extY - inset;

            if (xMax <= xMin)
            {
                float mid = (xMin + xMax) * 0.5f;
                xMin = mid - 0.25f;
                xMax = mid + 0.25f;
            }
            if (yMax <= yMin)
            {
                float mid = (yMin + yMax) * 0.5f;
                yMin = mid - 0.25f;
                yMax = mid + 0.25f;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public bool IsInsideKingCityInterior(MonsterWorldKind world, Vector2 position, float extraInsetCells = 0f)
        {
            return GetKingCityInnerRectWorld(world, extraInsetCells).Contains(position);
        }

        public bool IsInsideAnyKingCityInterior(Vector2 position, float extraInsetCells = 0f)
        {
            return
                IsInsideKingCityInterior(MonsterWorldKind.Fire, position, extraInsetCells) ||
                IsInsideKingCityInterior(MonsterWorldKind.Grass, position, extraInsetCells) ||
                IsInsideKingCityInterior(MonsterWorldKind.Ice, position, extraInsetCells);
        }

        public void Rebuild()
        {
            if (config == null || infiniteBackground == null)
                return;

            grid = infiniteBackground.WorldGrid;
            if (grid == null)
                return;

            mazeW = Mathf.Max(11, config.mazeWidthCells | 1);
            mazeH = Mathf.Max(11, config.mazeHeightCells | 1);

            for (int i = 0; i < 3; i++)
            {
                Vector2 island = GetIslandCenter((MonsterWorldKind)i);
                Vector2 offset = StableUnitOffset(config.worldSeed + i * 9973) * config.kingCityOffsetFromIsland;
                kingCityCentersWorld[i] = island + offset;
            }

            EnsureWallTilemap();
            wallTilemap.ClearAllTiles();

            BuildKingCityForWorld(MonsterWorldKind.Fire, kingCityCentersWorld[0], config.worldSeed + 101234);
            BuildKingCityForWorld(MonsterWorldKind.Grass, kingCityCentersWorld[1], config.worldSeed + 202567);
            BuildKingCityForWorld(MonsterWorldKind.Ice, kingCityCentersWorld[2], config.worldSeed + 303891);
        }

        public InfiniteBackground.MapVisualTheme GetThemeForCell(int cellX, int cellY)
        {
            if (grid == null || config == null)
                return InfiniteBackground.MapVisualTheme.LavaDungeon;

            Vector3 world = grid.CellToWorld(new Vector3Int(cellX, cellY, 0)) + grid.cellSize * 0.5f;
            MonsterWorldKind w = GetWorldAtWorldPosition(new Vector2(world.x, world.y));
            return ThemeForWorld(w);
        }

        public MonsterWorldKind GetWorldAtWorldPosition(Vector2 world)
        {
            if (config == null)
                return MonsterWorldKind.Fire;

            float scale = Mathf.Max(1f, config.islandInfluenceScale);
            Vector2[] centers = { config.fireIslandCenter, config.grassIslandCenter, config.iceIslandCenter };

            int best = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float d = ((world - centers[i]) / scale).sqrMagnitude;
                float n = Hash01(Mathf.FloorToInt(world.x * 0.31f), Mathf.FloorToInt(world.y * 0.29f), config.worldSeed + i * 1337);
                float score = d - n * Mathf.Max(0f, config.islandEdgeNoise) * 0.002f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return (MonsterWorldKind)best;
        }

        public Vector2 GetRandomSpawnInKingCityRing(MonsterWorldKind world, int nonce)
        {
            if (config == null || infiniteBackground == null)
                return Vector2.zero;

            int idx = (int)world;
            Vector2 center = kingCityCentersWorld[idx];
            float cell = Mathf.Max(0.01f, infiniteBackground.TileCellScale);

            float extX = mazeW * cell * 0.5f;
            float extY = mazeH * cell * 0.5f;
            float halfExtent = Mathf.Max(extX, extY);

            float inner = halfExtent + config.spawnRingInnerCellOffset * cell;
            float outer = inner + config.spawnRingThicknessCells * cell;
            outer = Mathf.Max(inner + 0.1f, outer);

            for (int attempt = 0; attempt < 24; attempt++)
            {
                float ang = Hash01(nonce + attempt, idx * 31, config.worldSeed + 5555) * Mathf.PI * 2f;
                float t = Hash01(idx + 17, (nonce + attempt) * 5 + 3, config.worldSeed + 6661);
                float r = Mathf.Lerp(inner, outer, t);
                Vector2 p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (GetWorldAtWorldPosition(p) == world)
                    return p;
            }

            float fallbackAng = Hash01(7, idx + nonce, config.worldSeed) * Mathf.PI * 2f;
            return center + new Vector2(Mathf.Cos(fallbackAng), Mathf.Sin(fallbackAng)) * inner;
        }

        private Vector2 GetIslandCenter(MonsterWorldKind world)
        {
            switch (world)
            {
                case MonsterWorldKind.Grass: return config.grassIslandCenter;
                case MonsterWorldKind.Ice: return config.iceIslandCenter;
                default: return config.fireIslandCenter;
            }
        }

        private static InfiniteBackground.MapVisualTheme ThemeForWorld(MonsterWorldKind w)
        {
            switch (w)
            {
                case MonsterWorldKind.Grass: return InfiniteBackground.MapVisualTheme.GrasslandRuins;
                case MonsterWorldKind.Ice: return InfiniteBackground.MapVisualTheme.SnowRuins;
                default: return InfiniteBackground.MapVisualTheme.LavaDungeon;
            }
        }

        private static Vector2 StableUnitOffset(int seed)
        {
            float x = Hash01(0, 0, seed) * 2f - 1f;
            float y = Hash01(1, 1, seed + 1) * 2f - 1f;
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.right;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h ^= x * 374761393;
                h = (h << 13) | (int)((uint)h >> 19);
                h = h * 1274126177;
                h ^= y * 668265263;
                uint u = (uint)h;
                return (u & 0x00FFFFFFu) / 16777215f;
            }
        }

        private void EnsureWallTilemap()
        {
            if (wallTilemap != null)
                return;

            Transform parent = grid.transform;
            Transform existing = parent.Find("RuntimeKingCityWalls");
            GameObject go = existing != null ? existing.gameObject : new GameObject("RuntimeKingCityWalls");
            go.transform.SetParent(parent, false);

            wallTilemap = go.GetComponent<Tilemap>();
            if (wallTilemap == null)
                wallTilemap = go.AddComponent<Tilemap>();

            if (go.GetComponent<TilemapRenderer>() == null)
                go.AddComponent<TilemapRenderer>();

            var renderer = go.GetComponent<TilemapRenderer>();
            TilemapRenderer ground = GameObject.Find("Tilemaps/Ground")?.GetComponent<TilemapRenderer>();
            if (ground != null)
            {
                renderer.sortingLayerID = ground.sortingLayerID;
                renderer.sortingOrder = ground.sortingOrder + 5;
            }

            TilemapCollider2D col = go.GetComponent<TilemapCollider2D>();
            if (col == null)
                col = go.AddComponent<TilemapCollider2D>();
            col.usedByComposite = true;

            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;

            CompositeCollider2D comp = go.GetComponent<CompositeCollider2D>();
            if (comp == null)
                comp = go.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

            go.tag = "Wall";

            wallTile = CreateWallTile();

            var hpHolder = go.GetComponent<KingCityWallRoot>();
            if (hpHolder == null)
                hpHolder = go.AddComponent<KingCityWallRoot>();
            hpHolder.WallHp = config != null ? config.wallDisplayHp : 999999;
        }

        private void BuildKingCityForWorld(MonsterWorldKind world, Vector2 centerWorld, int mazeSeed)
        {
            Vector3Int centerCell = grid.WorldToCell(new Vector3(centerWorld.x, centerWorld.y, 0f));
            int ox = centerCell.x - mazeW / 2;
            int oy = centerCell.y - mazeH / 2;

            int gateHalfWidth = 1; // 3-cell small gate
            int gateCenterX = mazeW / 2;

            for (int y = 0; y < mazeH; y++)
            for (int x = 0; x < mazeW; x++)
            {
                bool isPerimeter = (x == 0) || (x == mazeW - 1) || (y == 0) || (y == mazeH - 1);
                if (!isPerimeter)
                    continue;

                bool isSouthGate = (y == 0) && Mathf.Abs(x - gateCenterX) <= gateHalfWidth;
                if (isSouthGate)
                    continue;

                wallTilemap.SetTile(new Vector3Int(ox + x, oy + y, 0), wallTile);
            }
        }

        private static Tile CreateWallTile()
        {
            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float edge = Mathf.Min(Mathf.Min(px, size - 1 - px), Mathf.Min(py, size - 1 - py));
                Color c = edge <= 1
                    ? new Color(0.18f, 0.16f, 0.14f, 1f)
                    : new Color(0.32f, 0.30f, 0.28f, 1f);
                tex.SetPixel(px, py, c);
            }
            tex.Apply();

            Tile t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            t.colliderType = Tile.ColliderType.Grid;
            return t;
        }
    }
}

