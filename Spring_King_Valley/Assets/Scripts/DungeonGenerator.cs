using System.Collections.Generic;
using UnityEngine;

/// <summary>墓室：多个大矩形房间 + L 形走廊连通，唯一入口房间与最远出口房间。</summary>
public static class DungeonGenerator
{
    public struct Result
    {
        public int Width, Height;
        public bool[] Floor;
        public Vector2Int SpawnCell;
        public Vector2Int ExitCell;
        public List<Vector2Int> NestCells;
    }

    public static Result Generate(int width, int height, int roomAttempts, int minRw, int maxRw, int minRh, int maxRh, int nestCount, int rngSeed, int retry = 0)
    {
        if (retry > 12)
        {
            Debug.LogError("DungeonGenerator: 多次重试仍无法生成足够房间。");
            return default;
        }

        Random.InitState(rngSeed);

        bool[] floor = new bool[width * height];
        List<RectInt> rooms = new List<RectInt>();

        for (int t = 0; t < roomAttempts * 8 && rooms.Count < roomAttempts; t++)
        {
            int rw = Random.Range(minRw, maxRw + 1);
            int rh = Random.Range(minRh, maxRh + 1);
            int rx = Random.Range(2, width - rw - 2);
            int ry = Random.Range(2, height - rh - 2);
            RectInt cand = new RectInt(rx, ry, rw, rh);
            if (Overlaps(cand, rooms, 2))
                continue;
            rooms.Add(cand);
            CarveRoom(floor, width, cand);
        }

        if (rooms.Count < 2)
            return Generate(width, height, roomAttempts + 2, minRw, maxRw, minRh, maxRh, nestCount, rngSeed + 1337, retry + 1);

        int entrance = 0;
        int exitRoom = FindFarthestRoom(rooms, entrance);
        List<(int, int)> edges = BuildMST(rooms);

        foreach (var e in edges)
            CarveCorridor(floor, width, CellFromRectCenter(rooms[e.Item1]), CellFromRectCenter(rooms[e.Item2]));

        Vector2Int spawn = CellFromRectCenter(rooms[entrance]);
        Vector2Int exit = CellFromRectCenter(rooms[exitRoom]);

        List<Vector2Int> nests = new List<Vector2Int>();
        List<int> nestCandidates = new List<int>();
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == entrance || i == exitRoom)
                continue;
            nestCandidates.Add(i);
        }
        for (int i = 0; i < nestCandidates.Count; i++)
        {
            int j = Random.Range(i, nestCandidates.Count);
            (nestCandidates[i], nestCandidates[j]) = (nestCandidates[j], nestCandidates[i]);
        }
        for (int i = 0; i < Mathf.Min(nestCount, nestCandidates.Count); i++)
            nests.Add(CellFromRectCenter(rooms[nestCandidates[i]]));

        return new Result
        {
            Width = width,
            Height = height,
            Floor = floor,
            SpawnCell = spawn,
            ExitCell = exit,
            NestCells = nests
        };
    }

    private static Vector2Int CellFromRectCenter(RectInt r)
    {
        int cx = r.x + r.width / 2;
        int cy = r.y + r.height / 2;
        return new Vector2Int(cx, cy);
    }

    private static bool Overlaps(RectInt a, List<RectInt> rooms, int pad)
    {
        RectInt ap = new RectInt(a.x - pad, a.y - pad, a.width + pad * 2, a.height + pad * 2);
        foreach (var b in rooms)
        {
            if (ap.Overlaps(b))
                return true;
        }
        return false;
    }

    private static void CarveRoom(bool[] g, int w, RectInt r)
    {
        for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
                g[x + y * w] = true;
    }

    private static void CarveCorridor(bool[] g, int w, Vector2Int a, Vector2Int b)
    {
        Vector2Int c = a;
        while (c.x != b.x)
        {
            CarveDisk(g, w, c.x, c.y, 1);
            c.x += (int)Mathf.Sign(b.x - c.x);
        }
        while (c.y != b.y)
        {
            CarveDisk(g, w, c.x, c.y, 1);
            c.y += (int)Mathf.Sign(b.y - c.y);
        }
        CarveDisk(g, w, c.x, c.y, 1);
    }

    private static void CarveDisk(bool[] g, int w, int cx, int cy, int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= w || y >= g.Length / w)
                    continue;
                g[x + y * w] = true;
            }
    }

    private static int FindFarthestRoom(List<RectInt> rooms, int start)
    {
        int n = rooms.Count;
        List<Vector2Int> centers = new List<Vector2Int>(n);
        foreach (var r in rooms)
            centers.Add(CellFromRectCenter(r));

        int best = start;
        int bestD = -1;
        for (int i = 0; i < n; i++)
        {
            int d = Mathf.Abs(centers[i].x - centers[start].x) + Mathf.Abs(centers[i].y - centers[start].y);
            if (d > bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    private static List<(int, int)> BuildMST(List<RectInt> rooms)
    {
        int n = rooms.Count;
        List<Vector2Int> c = new List<Vector2Int>();
        foreach (var r in rooms)
            c.Add(CellFromRectCenter(r));

        bool[] used = new bool[n];
        List<(int, int)> mst = new List<(int, int)>();
        used[0] = true;
        for (int k = 0; k < n - 1; k++)
        {
            int bestU = -1, bestV = -1, bestW = int.MaxValue;
            for (int u = 0; u < n; u++)
            {
                if (!used[u]) continue;
                for (int v = 0; v < n; v++)
                {
                    if (used[v]) continue;
                    int d = Mathf.Abs(c[u].x - c[v].x) + Mathf.Abs(c[u].y - c[v].y);
                    if (d < bestW)
                    {
                        bestW = d;
                        bestU = u;
                        bestV = v;
                    }
                }
            }
            if (bestV >= 0)
            {
                used[bestV] = true;
                mst.Add((bestU, bestV));
            }
        }
        return mst;
    }
}
