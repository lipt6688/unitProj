using UnityEngine;

/// <summary>程序化巢穴标记（卵 + 裂隙 + 暗红核心），无需外置贴图文件。</summary>
public static class NestMarkerSprite
{
    private static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached != null)
            return _cached;

        const int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, clear);

        void Pixel(int x, int y, Color c)
        {
            if (x >= 0 && x < s && y >= 0 && y < s)
                tex.SetPixel(x, y, c);
        }

        void Disc(int cx, int cy, int r, Color c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r * r + r / 2)
                        Pixel(cx + x, cy + y, c);
                }
        }

        Color mud = new Color(0.32f, 0.22f, 0.14f, 1f);
        Color mudDark = new Color(0.18f, 0.12f, 0.08f, 1f);
        Color egg = new Color(0.92f, 0.88f, 0.78f, 1f);
        Color core = new Color(0.55f, 0.08f, 0.1f, 1f);
        Color glow = new Color(1f, 0.45f, 0.12f, 0.35f);

        Disc(32, 30, 22, mudDark);
        Disc(32, 32, 20, mud);
        Disc(26, 34, 5, egg);
        Disc(36, 36, 5, egg);
        Disc(32, 28, 4, egg);
        Disc(32, 32, 6, core);
        Disc(32, 32, 14, glow);

        for (int i = 0; i < 8; i++)
        {
            int vx = 18 + (i * 7) % 28;
            int vy = 20 + (i * 5) % 20;
            Pixel(vx, vy, mudDark);
            Pixel(vx + 1, vy, mudDark);
        }

        tex.Apply();
        _cached = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.32f), 48f);
        return _cached;
    }
}
