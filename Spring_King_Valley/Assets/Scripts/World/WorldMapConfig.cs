using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "WorldMap", menuName = "Vampire/World Map Config", order = 2)]
    public class WorldMapConfig : ScriptableObject
    {
        [Header("Determinism")]
        public int worldSeed = 424242;

        [Header("Islands (world space)")]
        public Vector2 fireIslandCenter = new Vector2(0f, 0f);
        public Vector2 grassIslandCenter = new Vector2(200f, 0f);
        public Vector2 iceIslandCenter = new Vector2(100f, 173f);

        [Tooltip("Distance scaling for island ownership comparisons (world units).")]
        public float islandInfluenceScale = 140f;

        [Tooltip("Edge jitter magnitude in world units (stable hash).")]
        public float islandEdgeNoise = 14f;

        [Header("King city maze (odd sizes, >= 11)")]
        public int mazeWidthCells = 31;
        public int mazeHeightCells = 31;

        [Tooltip("Offset king city from island center (world units).")]
        public float kingCityOffsetFromIsland = 18f;

        [Header("Monster spawn ring (around maze outer wall)")]
        public float spawnRingInnerCellOffset = 3f;
        public float spawnRingThicknessCells = 12f;

        [Header("Wall (gameplay)")]
        public int wallDisplayHp = 999999;
    }
}
