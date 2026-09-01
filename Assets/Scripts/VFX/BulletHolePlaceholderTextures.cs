using UnityEngine;

/// <summary>
/// Temporary bullet-hole textures. Assigned materials can be replaced in the
/// inspector without changing code.
/// </summary>
public static class BulletHolePlaceholderTextures
{
    public const int DefaultSize = 256;

    public static Texture2D Create(int variantIndex, int size = DefaultSize)
    {
        int seed = 17 + variantIndex * 97;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"BulletHole_0{variantIndex + 1}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            alphaIsTransparency = true
        };

        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        float holeRadius = 0.22f + Hash(seed, 1) * 0.06f;
        float ovalX = 0.85f + Hash(seed, 2) * 0.3f;
        float ovalY = 0.85f + Hash(seed, 3) * 0.3f;
        float offsetX = (Hash(seed, 4) - 0.5f) * 0.08f;
        float offsetY = (Hash(seed, 5) - 0.5f) * 0.08f;
        int crackCount = 3 + (seed % 3);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half;
                float v = (y + 0.5f - half) / half;
                float nx = (u - offsetX) * ovalX;
                float ny = (v - offsetY) * ovalY;
                float radial = Mathf.Sqrt(nx * nx + ny * ny);
                float noise = ValueNoise(u * 7.3f + seed, v * 7.3f - seed) * 0.045f;
                float warped = radial + noise;

                float hole = 1f - SmoothStep(holeRadius - 0.03f, holeRadius + 0.02f, warped);
                float rim = 1f - SmoothStep(holeRadius, holeRadius + 0.12f, warped);
                rim = Mathf.Max(0f, rim - hole);

                float cracks = 0f;
                for (int i = 0; i < crackCount; i++)
                {
                    float angle = (Hash(seed, 10 + i) * 360f + i * 47f) * Mathf.Deg2Rad;
                    float ca = Mathf.Cos(angle);
                    float sa = Mathf.Sin(angle);
                    float along = u * ca + v * sa;
                    float across = -u * sa + v * ca;
                    if (along < holeRadius * 0.35f)
                        continue;

                    float length = 0.34f + Hash(seed, 20 + i) * 0.28f;
                    float width = 0.016f + Hash(seed, 30 + i) * 0.012f;
                    float t = Mathf.Clamp01(along / length);
                    float w = width * (1f - t * 0.65f);
                    float line = 1f - Mathf.Clamp01(Mathf.Abs(across) / w);
                    line *= 1f - SmoothStep(length * 0.82f, length, along);
                    cracks = Mathf.Max(cracks, line);
                }

                float alpha = Mathf.Clamp01(Mathf.Max(hole, Mathf.Max(rim * 0.7f, cracks * 0.9f)));
                if (alpha <= 0.02f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float soot = 0.015f + ValueNoise(u * 11f, v * 11f + seed) * 0.03f;
                float gray = Mathf.Lerp(soot, 0.12f, rim * 0.35f);
                gray = Mathf.Lerp(gray, 0.05f, cracks);
                pixels[y * size + x] = new Color(gray, gray * 0.96f, gray * 0.9f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static float Hash(int seed, int salt)
    {
        int h = seed * 374761393 + salt * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return (h & 0x7fffffff) / (float)int.MaxValue;
    }

    private static float ValueNoise(float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = x - x0;
        float ty = y - y0;
        float v00 = Hash(x0, y0);
        float v10 = Hash(x0 + 1, y0);
        float v01 = Hash(x0, y0 + 1);
        float v11 = Hash(x0 + 1, y0 + 1);
        float a = Mathf.Lerp(v00, v10, tx);
        float b = Mathf.Lerp(v01, v11, tx);
        return Mathf.Lerp(a, b, ty);
    }

    private static float SmoothStep(float from, float to, float value)
    {
        float t = Mathf.Clamp01((value - from) / Mathf.Max(0.0001f, to - from));
        return t * t * (3f - 2f * t);
    }
}
