using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// DFS backtracker maze on an odd grid.
    /// solid[x,y] == true means place a blocking wall tile.
    /// </summary>
    public static class KingCityMazeGenerator
    {
        public static bool[,] Build(int width, int height, int seed)
        {
            int w = Mathf.Max(11, width | 1);
            int h = Mathf.Max(11, height | 1);

            bool[,] solid = new bool[w, h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                solid[x, y] = true;

            bool[,] visited = new bool[w, h];
            Stack<Vector2Int> stack = new Stack<Vector2Int>();
            Vector2Int start = new Vector2Int(1, 1);
            solid[start.x, start.y] = false;
            visited[start.x, start.y] = true;
            stack.Push(start);

            Vector2Int[] dirs =
            {
                new Vector2Int(2, 0),
                new Vector2Int(-2, 0),
                new Vector2Int(0, 2),
                new Vector2Int(0, -2)
            };

            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                List<int> choices = new List<int>(4);
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector2Int n = cur + dirs[i];
                    if (n.x < 1 || n.x >= w - 1 || n.y < 1 || n.y >= h - 1)
                        continue;
                    if (!visited[n.x, n.y])
                        choices.Add(i);
                }

                if (choices.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                int pickIndex = PositiveMod(StableHash(cur.x, cur.y, seed + stack.Count * 739), choices.Count);
                int pick = choices[pickIndex];
                Vector2Int step = dirs[pick];
                Vector2Int next = cur + step;
                Vector2Int mid = cur + new Vector2Int(step.x / 2, step.y / 2);

                solid[mid.x, mid.y] = false;
                solid[next.x, next.y] = false;
                visited[next.x, next.y] = true;
                stack.Push(next);
            }

            // Reserve a small boss arena area in the center (boss not implemented yet).
            int cx = w / 2;
            int cy = h / 2;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                solid[cx + dx, cy + dy] = false;

            return solid;
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

        private static int PositiveMod(int value, int mod)
        {
            int r = value % mod;
            return r < 0 ? r + mod : r;
        }
    }
}
