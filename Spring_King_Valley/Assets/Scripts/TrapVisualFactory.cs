using UnityEngine;

public static class TrapVisualFactory
{
    private static Sprite _spikeSprite;

    public static Sprite GetSpikeSprite()
    {
        if (_spikeSprite != null)
            return _spikeSprite;

        const int s = 32;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;

        // Draw a simple floor plate + 3 spikes.
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Color c = new Color(0, 0, 0, 0);

            // base plate
            if (y < 10)
            {
                float v = 0.18f + (x % 3) * 0.02f;
                c = new Color(v, v, v, 1f);
            }

            // spikes (triangles)
            int spikeRegionY = y - 10;
            if (spikeRegionY >= 0)
            {
                bool spike = false;
                spike |= InTriangle(x, spikeRegionY, 7, 0, 6, 18);
                spike |= InTriangle(x, spikeRegionY, 16, 0, 6, 20);
                spike |= InTriangle(x, spikeRegionY, 25, 0, 6, 18);

                if (spike)
                {
                    float shade = 0.85f - Mathf.Abs(x - 16) * 0.008f;
                    c = new Color(shade, shade, shade, 1f);
                }
            }

            t.SetPixel(x, y, c);
        }

        t.Apply();
        _spikeSprite = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.25f), s);
        return _spikeSprite;
    }

    private static bool InTriangle(int x, int y, int centerX, int baseY, int halfWidth, int height)
    {
        if (y < baseY || y > baseY + height)
            return false;
        float t = (y - baseY) / (float)height;
        float w = Mathf.Lerp(halfWidth, 0f, t);
        return x >= centerX - w && x <= centerX + w;
    }
}

