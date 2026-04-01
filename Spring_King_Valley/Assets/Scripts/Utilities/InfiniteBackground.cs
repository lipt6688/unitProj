using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Vampire
{
    public class InfiniteBackground : MonoBehaviour
    {
        public enum MapVisualTheme
        {
            LavaDungeon = 1,
            GrasslandRuins = 2,
            SnowRuins = 3
        }

        private Transform playerTransform;
        [Header("Tilemap Runtime Layers")]
        [SerializeField] private Grid targetGrid;
        [SerializeField] private Tilemap backgroundTilemap;
        [SerializeField] private Tilemap foregroundTilemap;

        [Header("Tile Sources")]
        [SerializeField] private TileBase[] backgroundTiles;
        [SerializeField] private TileBase[] foregroundTiles;

        [Header("Chunk Settings")]
        [SerializeField, Range(8, 64)] private int chunkSize = 24;
        [SerializeField, Range(1, 8)] private int activeChunkRadius = 3;
        [SerializeField, Range(2, 12)] private int unloadChunkRadius = 5;

        [Header("Variation")]
        [SerializeField, Range(1, 10)] private int patternPatchSize = 4;
        [SerializeField, Range(0f, 0.25f)] private float backgroundTintJitter = 0.08f;
        [SerializeField, Range(0f, 0.2f)] private float foregroundTintJitter = 0.06f;
        [SerializeField, Range(0f, 0.6f)] private float foregroundDensity = 0.12f;
        [SerializeField, Range(0.45f, 1f)] private float tileCellScale = 0.72f;

        [Header("Theme")]
        [SerializeField] private MapVisualTheme mapTheme = MapVisualTheme.LavaDungeon;
        [SerializeField, Range(0.08f, 0.7f)] private float lavaCoverage = 0.28f;
        [SerializeField, Range(0f, 0.2f)] private float lavaEdgeBlend = 0.09f;
        [SerializeField, Range(0f, 0.2f)] private float emberChance = 0.04f;
        [SerializeField, Range(0.08f, 0.75f)] private float grassPathCoverage = 0.26f;
        [SerializeField, Range(0.08f, 0.75f)] private float snowCoverage = 0.35f;
        [SerializeField] private bool useThreeThemeRegions = true;
        [SerializeField] private Vector2 threeRegionCenterCell = Vector2.zero;
        [SerializeField, Range(0f, 360f)] private float threeRegionStartAngle = 0f;

        [Header("Runtime")]
        [SerializeField] private bool clearOnInit = true;
        [SerializeField] private bool disableLegacyMeshRenderer = true;
        [SerializeField] private bool autoCollectTilesFromScene = true;

        private int mapSeed;
        private bool initialized;
        private Vector2Int centerChunk;

        private TileBase[] resolvedBackgroundTiles;
        private TileBase[] resolvedForegroundTiles;
        private TileBase[] lavaBackgroundTiles;
        private TileBase[] lavaForegroundTiles;
        private TileBase[] grassBackgroundTiles;
        private TileBase[] grassForegroundTiles;
        private TileBase[] snowBackgroundTiles;
        private TileBase[] snowForegroundTiles;

        private readonly HashSet<Vector2Int> generatedChunks = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> trimBuffer = new List<Vector2Int>();
        private readonly List<Tile> runtimeTiles = new List<Tile>();
        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        [SerializeField] private WorldMapCoordinator worldCoordinator;

        public Grid WorldGrid => targetGrid;
        public float TileCellScale => tileCellScale;
        public int MapSeed => mapSeed;

        public void SetWorldCoordinator(WorldMapCoordinator coordinator)
        {
            worldCoordinator = coordinator;
        }

        private void Awake()
        {
            EnsureRuntimeTilemaps();

            if (disableLegacyMeshRenderer)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.enabled = false;
            }
        }

        public void SetTheme(MapVisualTheme theme)
        {
            mapTheme = theme;
        }

        public void SetThemeByIndex(int themeIndex)
        {
            switch (themeIndex)
            {
                case 1:
                    mapTheme = MapVisualTheme.LavaDungeon;
                    break;
                case 2:
                    mapTheme = MapVisualTheme.GrasslandRuins;
                    break;
                case 3:
                    mapTheme = MapVisualTheme.SnowRuins;
                    break;
                default:
                    mapTheme = MapVisualTheme.LavaDungeon;
                    break;
            }
        }

        public void Init(Texture2D backgroundTexture, Transform playerTransform)
        {
            this.playerTransform = playerTransform;
            EnsureRuntimeTilemaps();

            mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
            ResolveTileSources(backgroundTexture);

            if (clearOnInit)
            {
                backgroundTilemap.ClearAllTiles();
                foregroundTilemap.ClearAllTiles();
            }

            generatedChunks.Clear();
            centerChunk = WorldToChunk(playerTransform != null ? playerTransform.position : Vector3.zero);
            PopulateAround(centerChunk, true);
            initialized = true;
        }

        public void Init(Texture2D backgroundTexture, Transform playerTransform, int seed, WorldMapCoordinator coordinator)
        {
            this.playerTransform = playerTransform;
            worldCoordinator = coordinator;
            EnsureRuntimeTilemaps();

            mapSeed = seed;
            ResolveTileSources(backgroundTexture);

            if (clearOnInit)
            {
                backgroundTilemap.ClearAllTiles();
                foregroundTilemap.ClearAllTiles();
            }

            generatedChunks.Clear();
            centerChunk = WorldToChunk(playerTransform != null ? playerTransform.position : Vector3.zero);
            PopulateAround(centerChunk, true);
            initialized = true;
        }

        private static Color SampleBaseColor(Texture2D baseTex)
        {
            Color baseColor = new Color(0.3f, 0.4f, 0.2f);
            if (baseTex == null)
                return baseColor;

            try
            {
                RenderTexture tmp = RenderTexture.GetTemporary(16, 16, 0);
                Graphics.Blit(baseTex, tmp);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = tmp;
                Texture2D copy = new Texture2D(16, 16, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(tmp);
                baseColor = copy.GetPixel(8, 8);
                Destroy(copy);
            }
            catch (Exception)
            {
                // Keep fallback color.
            }

            return baseColor;
        }

        private void EnsureRuntimeTilemaps()
        {
            if (targetGrid == null)
                targetGrid = GetComponentInChildren<Grid>();

            if (targetGrid == null)
            {
                GameObject go = new GameObject("RuntimeTilemapGrid");
                go.transform.SetParent(transform, false);
                targetGrid = go.AddComponent<Grid>();
            }

            targetGrid.cellSize = new Vector3(tileCellScale, tileCellScale, 1f);

            if (backgroundTilemap == null)
                backgroundTilemap = EnsureTilemapLayer(targetGrid.transform, "RuntimeBackgroundTilemap", -20);
            if (foregroundTilemap == null)
                foregroundTilemap = EnsureTilemapLayer(targetGrid.transform, "RuntimeForegroundTilemap", -18);

            ApplyVisibleSorting();
        }

        private static Tilemap EnsureTilemapLayer(Transform parent, string layerName, int sortingOrder)
        {
            Transform existing = parent.Find(layerName);
            Tilemap tilemap;

            if (existing == null)
            {
                GameObject go = new GameObject(layerName);
                go.transform.SetParent(parent, false);
                tilemap = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
            }
            else
            {
                tilemap = existing.GetComponent<Tilemap>();
                if (tilemap == null)
                    tilemap = existing.gameObject.AddComponent<Tilemap>();
                if (existing.GetComponent<TilemapRenderer>() == null)
                    existing.gameObject.AddComponent<TilemapRenderer>();
            }

            var renderer = tilemap.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private void ApplyVisibleSorting()
        {
            var bgRenderer = backgroundTilemap != null ? backgroundTilemap.GetComponent<TilemapRenderer>() : null;
            var fgRenderer = foregroundTilemap != null ? foregroundTilemap.GetComponent<TilemapRenderer>() : null;
            if (bgRenderer == null || fgRenderer == null)
                return;

            TilemapRenderer groundRenderer = GameObject.Find("Tilemaps/Ground")?.GetComponent<TilemapRenderer>();
            TilemapRenderer wallRenderer = GameObject.Find("Tilemaps/Wall")?.GetComponent<TilemapRenderer>();

            if (groundRenderer != null)
            {
                bgRenderer.sortingLayerID = groundRenderer.sortingLayerID;
                fgRenderer.sortingLayerID = groundRenderer.sortingLayerID;

                bgRenderer.sortingOrder = groundRenderer.sortingOrder + 2;
                int wallOrder = wallRenderer != null ? wallRenderer.sortingOrder : groundRenderer.sortingOrder + 1;
                fgRenderer.sortingOrder = Mathf.Max(bgRenderer.sortingOrder + 1, wallOrder + 1);
            }
            else
            {
                bgRenderer.sortingOrder = 2;
                fgRenderer.sortingOrder = 3;
            }
        }

        private void ResolveTileSources(Texture2D backgroundTexture)
        {
            if (useThreeThemeRegions)
            {
                BuildLavaThemeTiles(backgroundTexture);
                lavaBackgroundTiles = resolvedBackgroundTiles;
                lavaForegroundTiles = resolvedForegroundTiles;

                BuildGrassRuinsThemeTiles(backgroundTexture);
                grassBackgroundTiles = resolvedBackgroundTiles;
                grassForegroundTiles = resolvedForegroundTiles;

                BuildSnowRuinsThemeTiles(backgroundTexture);
                snowBackgroundTiles = resolvedBackgroundTiles;
                snowForegroundTiles = resolvedForegroundTiles;

                // Keep non-null defaults for fallback utility paths.
                resolvedBackgroundTiles = lavaBackgroundTiles;
                resolvedForegroundTiles = lavaForegroundTiles;

                backgroundTilemap.color = Color.white;
                foregroundTilemap.color = new Color(1f, 1f, 1f, 0.82f);
                return;
            }

            if (mapTheme == MapVisualTheme.LavaDungeon)
            {
                BuildLavaThemeTiles(backgroundTexture);
                backgroundTilemap.color = Color.white;
                foregroundTilemap.color = new Color(1f, 0.90f, 0.82f, 0.82f);
                return;
            }

            if (mapTheme == MapVisualTheme.GrasslandRuins)
            {
                BuildGrassRuinsThemeTiles(backgroundTexture);
                backgroundTilemap.color = Color.white;
                foregroundTilemap.color = new Color(0.92f, 1f, 0.92f, 0.82f);
                return;
            }

            if (mapTheme == MapVisualTheme.SnowRuins)
            {
                BuildSnowRuinsThemeTiles(backgroundTexture);
                backgroundTilemap.color = Color.white;
                foregroundTilemap.color = new Color(0.9f, 0.95f, 1f, 0.78f);
                return;
            }

            resolvedBackgroundTiles = CompactTiles(backgroundTiles);
            resolvedForegroundTiles = CompactTiles(foregroundTiles);

            if (autoCollectTilesFromScene)
                AutoFillFromSceneAndAssets(ref resolvedBackgroundTiles, ref resolvedForegroundTiles);

            if (resolvedBackgroundTiles.Length == 0)
            {
                Color baseColor = SampleBaseColor(backgroundTexture);
                resolvedBackgroundTiles = new TileBase[]
                {
                    CreateRuntimeTile(Color.Lerp(baseColor, Color.black, 0.05f), 0),
                    CreateRuntimeTile(baseColor, 1),
                    CreateRuntimeTile(Color.Lerp(baseColor, Color.white, 0.08f), 2),
                    CreateRuntimeTile(Color.Lerp(baseColor, new Color(0.35f, 0.30f, 0.22f, 1f), 0.22f), 3)
                };
            }

            if (resolvedForegroundTiles.Length == 0)
            {
                Color tint = Color.Lerp(SampleBaseColor(backgroundTexture), Color.white, 0.3f);
                resolvedForegroundTiles = new TileBase[]
                {
                    CreateRuntimeTile(new Color(tint.r, tint.g, tint.b, 0.85f), 4),
                    CreateRuntimeTile(new Color(tint.r * 0.9f, tint.g * 0.95f, tint.b * 0.9f, 0.78f), 5)
                };
            }

            backgroundTilemap.color = Color.white;
            foregroundTilemap.color = new Color(1f, 1f, 1f, 0.65f);
        }

        private void BuildLavaThemeTiles(Texture2D backgroundTexture)
        {
            Color seedColor = SampleBaseColor(backgroundTexture);
            Color basaltDark = Color.Lerp(new Color(0.07f, 0.06f, 0.07f, 1f), seedColor, 0.08f);
            Color basaltMid = Color.Lerp(new Color(0.13f, 0.11f, 0.11f, 1f), seedColor, 0.15f);
            Color scorched = new Color(0.24f, 0.14f, 0.10f, 1f);
            Color lavaFlow = new Color(0.82f, 0.22f, 0.03f, 1f);
            Color lavaCore = new Color(1f, 0.58f, 0.08f, 1f);

            resolvedBackgroundTiles = new TileBase[]
            {
                CreateRuntimeTile(basaltDark, 30),
                CreateRuntimeTile(basaltMid, 31),
                CreateRuntimeTile(scorched, 32),
                CreateRuntimeTile(lavaFlow, 33),
                CreateRuntimeTile(lavaCore, 34)
            };

            resolvedForegroundTiles = new TileBase[]
            {
                CreateRuntimeTile(new Color(0.20f, 0.15f, 0.14f, 0.95f), 40),
                CreateRuntimeTile(new Color(0.29f, 0.20f, 0.16f, 0.92f), 41),
                CreateRuntimeTile(new Color(1f, 0.70f, 0.16f, 0.86f), 42)
            };
        }

        private void BuildGrassRuinsThemeTiles(Texture2D backgroundTexture)
        {
            Color seed = SampleBaseColor(backgroundTexture);
            Color mossDark = Color.Lerp(new Color(0.13f, 0.20f, 0.10f, 1f), seed, 0.18f);
            Color grass = Color.Lerp(new Color(0.20f, 0.35f, 0.16f, 1f), seed, 0.20f);
            Color dirt = new Color(0.34f, 0.27f, 0.18f, 1f);
            Color oldStone = new Color(0.45f, 0.43f, 0.35f, 1f);
            Color wornPath = new Color(0.52f, 0.46f, 0.34f, 1f);

            resolvedBackgroundTiles = new TileBase[]
            {
                CreateRuntimeTile(mossDark, 60),
                CreateRuntimeTile(grass, 61),
                CreateRuntimeTile(dirt, 62),
                CreateRuntimeTile(oldStone, 63),
                CreateRuntimeTile(wornPath, 64)
            };

            resolvedForegroundTiles = new TileBase[]
            {
                CreateRuntimeTile(new Color(0.30f, 0.39f, 0.23f, 0.9f), 70),
                CreateRuntimeTile(new Color(0.45f, 0.44f, 0.37f, 0.9f), 71),
                CreateRuntimeTile(new Color(0.56f, 0.52f, 0.41f, 0.88f), 72)
            };
        }

        private void BuildSnowRuinsThemeTiles(Texture2D backgroundTexture)
        {
            Color seed = SampleBaseColor(backgroundTexture);
            Color frozenStone = Color.Lerp(new Color(0.21f, 0.23f, 0.28f, 1f), seed, 0.10f);
            Color coldRock = new Color(0.30f, 0.34f, 0.39f, 1f);
            Color frost = new Color(0.66f, 0.75f, 0.82f, 1f);
            Color snow = new Color(0.88f, 0.93f, 0.98f, 1f);
            Color ice = new Color(0.74f, 0.86f, 0.95f, 1f);

            resolvedBackgroundTiles = new TileBase[]
            {
                CreateRuntimeTile(frozenStone, 80),
                CreateRuntimeTile(coldRock, 81),
                CreateRuntimeTile(frost, 82),
                CreateRuntimeTile(snow, 83),
                CreateRuntimeTile(ice, 84)
            };

            resolvedForegroundTiles = new TileBase[]
            {
                CreateRuntimeTile(new Color(0.78f, 0.86f, 0.94f, 0.86f), 90),
                CreateRuntimeTile(new Color(0.63f, 0.71f, 0.80f, 0.88f), 91),
                CreateRuntimeTile(new Color(0.92f, 0.97f, 1f, 0.82f), 92)
            };
        }

        private void AutoFillFromSceneAndAssets(ref TileBase[] bg, ref TileBase[] fg)
        {
            List<TileBase> floorCandidates = new List<TileBase>();
            List<TileBase> decorCandidates = new List<TileBase>();

            AddTilesFromTilemap("Tilemaps/Ground", floorCandidates);
            AddTilesFromTilemap("Tilemaps/MazeFloor", floorCandidates);
            AddTilesFromTilemap("Tilemaps/Wall", decorCandidates);
            AddTilesFromTilemap("Tilemaps/MazeWall", decorCandidates);

            if (bg.Length == 0)
                bg = DistinctAndLimit(floorCandidates, 12);
            if (fg.Length == 0)
            {
                if (decorCandidates.Count == 0)
                    decorCandidates.AddRange(floorCandidates);
                fg = DistinctAndLimit(decorCandidates, 8);
            }

#if UNITY_EDITOR
            if (bg.Length < 4 || fg.Length < 2)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TileBase Roguelike", new[] { "Assets/Tiles" });
                List<TileBase> loaded = new List<TileBase>();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    TileBase tile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                        loaded.Add(tile);
                }

                loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                if (bg.Length < 4)
                    bg = MergeAndLimit(bg, loaded, 12);
                if (fg.Length < 2)
                    fg = MergeAndLimit(fg, loaded, 8);
            }
#endif
        }

        private static TileBase[] MergeAndLimit(TileBase[] seed, List<TileBase> extra, int limit)
        {
            List<TileBase> merged = new List<TileBase>();
            if (seed != null)
                merged.AddRange(seed);
            merged.AddRange(extra);
            return DistinctAndLimit(merged, limit);
        }

        private static TileBase[] DistinctAndLimit(List<TileBase> source, int limit)
        {
            HashSet<TileBase> seen = new HashSet<TileBase>();
            List<TileBase> list = new List<TileBase>();
            for (int i = 0; i < source.Count; i++)
            {
                TileBase t = source[i];
                if (t == null || !seen.Add(t))
                    continue;

                list.Add(t);
                if (list.Count >= limit)
                    break;
            }

            return list.ToArray();
        }

        private static void AddTilesFromTilemap(string path, List<TileBase> output)
        {
            GameObject go = GameObject.Find(path);
            if (go == null)
                return;

            Tilemap tm = go.GetComponent<Tilemap>();
            if (tm == null)
                return;

            TileBase[] used = new TileBase[Mathf.Max(1, tm.GetUsedTilesCount())];
            int count = tm.GetUsedTilesNonAlloc(used);
            for (int i = 0; i < count; i++)
            {
                if (used[i] != null)
                    output.Add(used[i]);
            }
        }

        private static TileBase[] CompactTiles(TileBase[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<TileBase>();

            List<TileBase> list = new List<TileBase>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                    list.Add(source[i]);
            }

            return list.ToArray();
        }

        private Tile CreateRuntimeTile(Color baseColor, int style)
        {
            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = (Hash01(x, y, style + 13) - 0.5f) * 0.08f;
                    float wave = Mathf.Sin((x + style * 5) * 0.7f) * Mathf.Cos((y - style * 3) * 0.6f) * 0.03f;
                    float v = Mathf.Clamp01(1f + grain + wave);
                    Color c = new Color(
                        Mathf.Clamp01(baseColor.r * v),
                        Mathf.Clamp01(baseColor.g * v),
                        Mathf.Clamp01(baseColor.b * v),
                        baseColor.a
                    );
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            tile.colliderType = Tile.ColliderType.None;

            runtimeTextures.Add(tex);
            runtimeTiles.Add(tile);
            return tile;
        }

        private void PopulateAround(Vector2Int chunkCenter, bool force)
        {
            int radius = Mathf.Max(1, activeChunkRadius);
            for (int cy = chunkCenter.y - radius; cy <= chunkCenter.y + radius; cy++)
            {
                for (int cx = chunkCenter.x - radius; cx <= chunkCenter.x + radius; cx++)
                {
                    Vector2Int c = new Vector2Int(cx, cy);
                    if (force || !generatedChunks.Contains(c))
                        GenerateChunk(c);
                }
            }
        }

        private void GenerateChunk(Vector2Int chunk)
        {
            int cs = Mathf.Max(1, chunkSize);
            int startX = chunk.x * cs;
            int startY = chunk.y * cs;
            int patch = Mathf.Max(1, patternPatchSize);

            for (int y = 0; y < cs; y++)
            {
                for (int x = 0; x < cs; x++)
                {
                    int wx = startX + x;
                    int wy = startY + y;
                    Vector3Int cell = new Vector3Int(wx, wy, 0);

                    TileBase bg = PickThemedBackgroundTile(wx, wy, patch);
                    backgroundTilemap.SetTile(cell, bg);
                    if (backgroundTintJitter > 0f)
                        ApplyTileTint(backgroundTilemap, cell, wx, wy, patch, backgroundTintJitter, mapSeed + 41);

                    if (ShouldPlaceForeground(wx, wy))
                    {
                        TileBase fg = PickForegroundTile(wx, wy);
                        if (fg != null)
                        {
                            foregroundTilemap.SetTile(cell, fg);
                            if (foregroundTintJitter > 0f)
                                ApplyTileTint(foregroundTilemap, cell, wx, wy, 3, foregroundTintJitter, mapSeed + 101);
                        }
                    }
                    else
                    {
                        foregroundTilemap.SetTile(cell, null);
                    }
                }
            }

            generatedChunks.Add(chunk);
        }

        private TileBase PickBackgroundTile(int x, int y, int patch)
        {
            return PickBackgroundTileFromSet(resolvedBackgroundTiles, x, y, patch, mapSeed + 17);
        }

        private TileBase PickBackgroundTileFromSet(TileBase[] tiles, int x, int y, int patch, int seed)
        {
            if (tiles == null || tiles.Length == 0)
                return null;
            if (tiles.Length == 1)
                return tiles[0];

            int blockX = Mathf.FloorToInt((float)x / patch);
            int blockY = Mathf.FloorToInt((float)y / patch);
            int coarse = StableHash(blockX, blockY, seed);
            int idx = PositiveModulo(coarse, tiles.Length);

            float detail = Hash01(x, y, seed + 36);
            if (detail > 0.78f)
                idx = PositiveModulo(idx + 1 + ((coarse >> 4) & 3), tiles.Length);

            return tiles[idx];
        }

        private MapVisualTheme GetThemeForCell(int x, int y)
        {
            if (worldCoordinator != null)
                return worldCoordinator.GetThemeForCell(x, y);

            if (!useThreeThemeRegions)
                return mapTheme;

            Vector2 toCell = new Vector2(x - threeRegionCenterCell.x, y - threeRegionCenterCell.y);
            float angle = Mathf.Atan2(toCell.y, toCell.x) * Mathf.Rad2Deg;
            float normalized = Mathf.Repeat(angle - threeRegionStartAngle, 360f);
            int index = Mathf.Clamp(Mathf.FloorToInt(normalized / 120f), 0, 2);

            switch (index)
            {
                case 0:
                    return MapVisualTheme.LavaDungeon;
                case 1:
                    return MapVisualTheme.GrasslandRuins;
                default:
                    return MapVisualTheme.SnowRuins;
            }
        }

        private TileBase[] GetBackgroundTilesForTheme(MapVisualTheme theme)
        {
            switch (theme)
            {
                case MapVisualTheme.GrasslandRuins:
                    return grassBackgroundTiles;
                case MapVisualTheme.SnowRuins:
                    return snowBackgroundTiles;
                default:
                    return lavaBackgroundTiles;
            }
        }

        private TileBase[] GetForegroundTilesForTheme(MapVisualTheme theme)
        {
            switch (theme)
            {
                case MapVisualTheme.GrasslandRuins:
                    return grassForegroundTiles;
                case MapVisualTheme.SnowRuins:
                    return snowForegroundTiles;
                default:
                    return lavaForegroundTiles;
            }
        }

        private TileBase PickThemedBackgroundTile(int x, int y, int patch)
        {
            MapVisualTheme theme = GetThemeForCell(x, y);
            TileBase[] tiles = useThreeThemeRegions ? GetBackgroundTilesForTheme(theme) : resolvedBackgroundTiles;

            switch (theme)
            {
                case MapVisualTheme.LavaDungeon:
                    return PickLavaBackgroundTile(x, y, patch, tiles);
                case MapVisualTheme.GrasslandRuins:
                    return PickGrassBackgroundTile(x, y, patch, tiles);
                case MapVisualTheme.SnowRuins:
                    return PickSnowBackgroundTile(x, y, patch, tiles);
                default:
                    return PickBackgroundTile(x, y, patch);
            }
        }

        private TileBase PickLavaBackgroundTile(int x, int y, int patch, TileBase[] tiles)
        {
            if (tiles == null || tiles.Length < 5)
                return PickBackgroundTileFromSet(tiles, x, y, patch, mapSeed + 17);

            float lava = LavaMask(x, y);
            float threshold = 1f - Mathf.Clamp(lavaCoverage, 0.08f, 0.7f);
            float edge = Mathf.Clamp(lavaEdgeBlend, 0f, 0.2f);

            if (lava > threshold + edge)
                return tiles[4];
            if (lava > threshold)
                return tiles[3];
            if (lava > threshold - edge)
                return tiles[2];

            int rockIdx = PositiveModulo(StableHash(x / patch, y / patch, mapSeed + 77), 2);
            return tiles[rockIdx];
        }

        private TileBase PickGrassBackgroundTile(int x, int y, int patch, TileBase[] tiles)
        {
            if (tiles == null || tiles.Length < 5)
                return PickBackgroundTileFromSet(tiles, x, y, patch, mapSeed + 17);

            float mask = GrassMask(x, y);
            float threshold = 1f - Mathf.Clamp(grassPathCoverage, 0.08f, 0.75f);
            float edge = Mathf.Clamp(lavaEdgeBlend * 0.8f, 0.02f, 0.16f);

            if (mask > threshold + edge)
                return tiles[4];
            if (mask > threshold)
                return tiles[3];
            if (mask > threshold - edge)
                return tiles[2];

            int idx = PositiveModulo(StableHash(x / patch, y / patch, mapSeed + 177), 2);
            return tiles[idx];
        }

        private TileBase PickSnowBackgroundTile(int x, int y, int patch, TileBase[] tiles)
        {
            if (tiles == null || tiles.Length < 5)
                return PickBackgroundTileFromSet(tiles, x, y, patch, mapSeed + 17);

            float mask = SnowMask(x, y);
            float threshold = 1f - Mathf.Clamp(snowCoverage, 0.08f, 0.75f);
            float edge = Mathf.Clamp(lavaEdgeBlend * 0.7f, 0.015f, 0.15f);

            if (mask > threshold + edge)
                return tiles[4];
            if (mask > threshold)
                return tiles[3];
            if (mask > threshold - edge)
                return tiles[2];

            int idx = PositiveModulo(StableHash(x / patch, y / patch, mapSeed + 277), 2);
            return tiles[idx];
        }

        private TileBase PickForegroundTile(int x, int y)
        {
            MapVisualTheme theme = GetThemeForCell(x, y);
            TileBase[] tiles = useThreeThemeRegions ? GetForegroundTilesForTheme(theme) : resolvedForegroundTiles;

            if (tiles == null || tiles.Length == 0)
                return null;

            if (theme == MapVisualTheme.LavaDungeon)
            {
                float lava = LavaMask(x, y);
                if (lava > 0.85f)
                    return tiles[Mathf.Min(2, tiles.Length - 1)];
                if (lava > 0.70f)
                    return tiles[Mathf.Min(1, tiles.Length - 1)];
                return tiles[PositiveModulo(StableHash(x, y, mapSeed + 313), Mathf.Min(2, tiles.Length))];
            }

            if (theme == MapVisualTheme.GrasslandRuins)
            {
                float g = GrassMask(x, y);
                if (g > 0.8f)
                    return tiles[Mathf.Min(2, tiles.Length - 1)];
                return tiles[PositiveModulo(StableHash(x, y, mapSeed + 413), Mathf.Min(2, tiles.Length))];
            }

            if (theme == MapVisualTheme.SnowRuins)
            {
                float s = SnowMask(x, y);
                if (s > 0.82f)
                    return tiles[Mathf.Min(2, tiles.Length - 1)];
                return tiles[PositiveModulo(StableHash(x, y, mapSeed + 513), Mathf.Min(2, tiles.Length))];
            }

            int h = StableHash(x, y, mapSeed + 313);
            return tiles[PositiveModulo(h, tiles.Length)];
        }

        private bool ShouldPlaceForeground(int x, int y)
        {
            TileBase[] tiles = useThreeThemeRegions ? GetForegroundTilesForTheme(GetThemeForCell(x, y)) : resolvedForegroundTiles;
            if (tiles == null || tiles.Length == 0)
                return false;

            MapVisualTheme theme = GetThemeForCell(x, y);

            if (theme == MapVisualTheme.LavaDungeon)
            {
                float lava = LavaMask(x, y);
                float lavaNoise = Hash01(x, y, mapSeed + 211);

                if (lava > 0.85f)
                    return lavaNoise < Mathf.Clamp01(emberChance);
                if (lava > 0.72f)
                    return lavaNoise < Mathf.Clamp01(emberChance * 0.5f);

                float rockChance = Mathf.Clamp01(foregroundDensity + 0.08f);
                if (lavaNoise > rockChance)
                    return false;

                int lavaCluster = StableHash(Mathf.FloorToInt((float)x / 3f), Mathf.FloorToInt((float)y / 3f), mapSeed + 811);
                return (lavaCluster & 3) != 0;
            }

            if (theme == MapVisualTheme.GrasslandRuins)
            {
                float g = GrassMask(x, y);
                float n = Hash01(x, y, mapSeed + 611);
                if (g > 0.78f)
                    return n < 0.22f;

                float chance = Mathf.Clamp01(foregroundDensity + 0.10f);
                if (n > chance)
                    return false;

                int c = StableHash(Mathf.FloorToInt((float)x / 3f), Mathf.FloorToInt((float)y / 3f), mapSeed + 817);
                return (c & 3) != 0;
            }

            if (theme == MapVisualTheme.SnowRuins)
            {
                float s = SnowMask(x, y);
                float n = Hash01(x, y, mapSeed + 711);
                if (s > 0.84f)
                    return n < 0.18f;

                float chance = Mathf.Clamp01(foregroundDensity * 0.8f + 0.05f);
                if (n > chance)
                    return false;

                int c = StableHash(Mathf.FloorToInt((float)x / 4f), Mathf.FloorToInt((float)y / 4f), mapSeed + 911);
                return (c & 3) != 0;
            }

            float densityNoise = Hash01(x, y, mapSeed + 211);
            if (densityNoise > Mathf.Clamp01(foregroundDensity))
                return false;

            int clusterHash = StableHash(Mathf.FloorToInt((float)x / 3f), Mathf.FloorToInt((float)y / 3f), mapSeed + 811);
            return (clusterHash & 3) != 0;
        }

        private float LavaMask(int x, int y)
        {
            float f1 = Mathf.Sin((x + mapSeed * 0.0037f) * 0.082f);
            float f2 = Mathf.Cos((y - mapSeed * 0.0041f) * 0.091f);
            float bands = Mathf.Abs(f1 + f2) * 0.5f;

            float noise = Hash01(x, y, mapSeed + 1001);
            float grain = Hash01(x / 2, y / 2, mapSeed + 1031);

            float lava = bands * 0.68f + noise * 0.23f + (1f - Mathf.Abs(grain * 2f - 1f)) * 0.09f;
            return Mathf.Clamp01(lava);
        }

        private float GrassMask(int x, int y)
        {
            float s1 = Mathf.Sin((x + mapSeed * 0.0023f) * 0.061f);
            float s2 = Mathf.Cos((y - mapSeed * 0.0017f) * 0.054f);
            float ridge = Mathf.Abs(s1 * 0.62f + s2 * 0.38f);

            float noise = Hash01(x, y, mapSeed + 1201);
            float macro = Hash01(x / 4, y / 4, mapSeed + 1229);
            float mask = ridge * 0.58f + noise * 0.24f + macro * 0.18f;
            return Mathf.Clamp01(mask);
        }

        private float SnowMask(int x, int y)
        {
            float wind = Mathf.Sin((x + mapSeed * 0.0019f) * 0.045f + y * 0.009f);
            float drift = Mathf.Cos((y - mapSeed * 0.0021f) * 0.051f - x * 0.006f);
            float shape = Mathf.Abs(wind * 0.55f + drift * 0.45f);

            float noise = Hash01(x, y, mapSeed + 1301);
            float broad = Hash01(x / 5, y / 5, mapSeed + 1337);
            float mask = shape * 0.62f + noise * 0.20f + broad * 0.18f;
            return Mathf.Clamp01(mask);
        }

        private static void ApplyTileTint(Tilemap tilemap, Vector3Int cell, int x, int y, int patch, float jitter, int seed)
        {
            int blockX = Mathf.FloorToInt((float)x / patch);
            int blockY = Mathf.FloorToInt((float)y / patch);

            float blockV = Hash01(blockX, blockY, seed) * 2f - 1f;
            float microV = Hash01(x, y, seed + 29) * 2f - 1f;
            float delta = (blockV * 0.65f + microV * 0.35f) * jitter;

            float v = Mathf.Clamp(1f + delta, 0.7f, 1.35f);
            tilemap.RemoveTileFlags(cell, TileFlags.LockColor);
            tilemap.SetColor(cell, new Color(v, v, v, 1f));
        }

        private void TrimChunks(Vector2Int chunkCenter)
        {
            int radius = Mathf.Max(activeChunkRadius + 1, unloadChunkRadius);
            trimBuffer.Clear();

            foreach (Vector2Int c in generatedChunks)
            {
                if (Mathf.Abs(c.x - chunkCenter.x) > radius || Mathf.Abs(c.y - chunkCenter.y) > radius)
                    trimBuffer.Add(c);
            }

            for (int i = 0; i < trimBuffer.Count; i++)
            {
                ClearChunk(trimBuffer[i]);
                generatedChunks.Remove(trimBuffer[i]);
            }
        }

        private void ClearChunk(Vector2Int chunk)
        {
            int cs = Mathf.Max(1, chunkSize);
            int startX = chunk.x * cs;
            int startY = chunk.y * cs;

            for (int y = 0; y < cs; y++)
            {
                for (int x = 0; x < cs; x++)
                {
                    Vector3Int cell = new Vector3Int(startX + x, startY + y, 0);
                    backgroundTilemap.SetTile(cell, null);
                    foregroundTilemap.SetTile(cell, null);
                }
            }
        }

        private Vector2Int WorldToChunk(Vector3 world)
        {
            Vector3Int cell = backgroundTilemap.WorldToCell(world);
            int cs = Mathf.Max(1, chunkSize);
            return new Vector2Int(FloorDiv(cell.x, cs), FloorDiv(cell.y, cs));
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            if (r != 0 && ((r > 0) != (divisor > 0)))
                q--;
            return q;
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

        public IEnumerator Shockwave(float distance)
        {
            if (!initialized)
                yield break;

            float duration = Mathf.Max(0.12f, distance / 16f);
            float t = 0f;
            Color bgStart = backgroundTilemap.color;
            Color fgStart = foregroundTilemap.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                float n = Mathf.Sin((t / duration) * Mathf.PI);
                float bgAmp = 1f + n * 0.08f;
                float fgAmp = 1f + n * 0.16f;

                backgroundTilemap.color = new Color(bgStart.r * bgAmp, bgStart.g * bgAmp, bgStart.b * bgAmp, bgStart.a);
                foregroundTilemap.color = new Color(fgStart.r * fgAmp, fgStart.g * fgAmp, fgStart.b * fgAmp, fgStart.a);
                yield return null;
            }

            backgroundTilemap.color = bgStart;
            foregroundTilemap.color = fgStart;
        }

        private void Update()
        {
            if (!initialized || playerTransform == null)
                return;

            Vector2Int nextCenter = WorldToChunk(playerTransform.position);
            if (nextCenter == centerChunk)
                return;

            centerChunk = nextCenter;
            PopulateAround(centerChunk, false);
            TrimChunks(centerChunk);
        }
        
        private void OnDestroy()
        {
            for (int i = 0; i < runtimeTiles.Count; i++)
            {
                if (runtimeTiles[i] != null)
                    Destroy(runtimeTiles[i]);
            }

            for (int i = 0; i < runtimeTextures.Count; i++)
            {
                if (runtimeTextures[i] != null)
                    Destroy(runtimeTextures[i]);
            }
        }
    }
}
