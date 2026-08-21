using UnityEngine;

/// <summary>
/// Temporary prototype artwork for the local Bullseye health HUD.
/// Final sprites can replace these through Inspector fields.
/// </summary>
public static class BullseyeHealthHudPlaceholders
{
    public const int DefaultSize = 256;
    public const int CrackLayerCount = 7;

    public static Sprite CreateBaseBullseye(int size = DefaultSize)
    {
        Color[] pixels = CreateTransparent(size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;

        Color cream = new Color(0.96f, 0.94f, 0.90f, 1f);
        Color red = new Color(0.78f, 0.12f, 0.16f, 1f);
        Color darkRed = new Color(0.42f, 0.05f, 0.08f, 1f);
        Color ink = new Color(0.08f, 0.07f, 0.07f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float outer = Coverage(dist, radius, 1.4f);
                if (outer <= 0.001f)
                    continue;

                float t = Mathf.Clamp01(dist / Mathf.Max(0.001f, radius));
                float band = t * 5f;
                int ring = Mathf.Clamp(Mathf.FloorToInt(band), 0, 4);
                Color ringColor = ring switch
                {
                    0 => darkRed,
                    1 => cream,
                    2 => red,
                    3 => cream,
                    _ => red
                };

                float boundary = Mathf.Abs(band - Mathf.Round(band));
                if (boundary < 0.045f && ring < 4)
                    ringColor = Color.Lerp(ringColor, ink, 1f - boundary / 0.045f);

                float rim = Coverage(dist, radius - 3.2f, 1.6f);
                ringColor = Color.Lerp(ink, ringColor, rim);

                pixels[y * size + x] = Premultiply(ringColor, outer);
            }
        }

        return ToSprite(pixels, size, "BullseyeHealth_Base");
    }

    public static Sprite CreateCrackLayer(int layerIndex, int size = DefaultSize)
    {
        Color[] pixels = CreateTransparent(size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2 impact = center + new Vector2(size * 0.07f, -size * 0.05f);
        var rng = new System.Random(1409 + layerIndex * 73);

        int rays = 3 + layerIndex;
        float thickness = 2.6f + layerIndex * 0.28f;
        Color edge = new Color(0.04f, 0.03f, 0.03f, 0.92f);
        Color highlight = new Color(1f, 1f, 0.98f, 0.78f);

        for (int i = 0; i < rays; i++)
        {
            float angle = (layerIndex * 0.41f + i * (Mathf.PI * 2f / rays) + (float)rng.NextDouble() * 0.35f);
            float length = size * (0.28f + 0.08f * layerIndex + (float)rng.NextDouble() * 0.08f);
            DrawJaggedCrack(pixels, size, impact, angle, length, thickness, edge, highlight, rng);

            if (layerIndex >= 2 && rng.NextDouble() > 0.35d)
            {
                float branchAngle = angle + ((rng.NextDouble() > 0.5d) ? 0.55f : -0.55f);
                Vector2 branchStart = impact + AngleDirection(angle) * (length * 0.45f);
                DrawJaggedCrack(
                    pixels,
                    size,
                    branchStart,
                    branchAngle,
                    length * 0.42f,
                    thickness * 0.75f,
                    edge,
                    highlight,
                    rng);
            }
        }

        if (layerIndex >= 3)
        {
            float arcRadius = size * (0.16f + layerIndex * 0.03f);
            int segments = 10 + layerIndex * 2;
            float start = (float)rng.NextDouble() * Mathf.PI * 2f;
            float sweep = 0.8f + layerIndex * 0.18f;
            Vector2 previous = impact + AngleDirection(start) * arcRadius;
            for (int i = 1; i <= segments; i++)
            {
                float a = start + sweep * (i / (float)segments);
                Vector2 next = impact + AngleDirection(a) * (arcRadius + ((float)rng.NextDouble() - 0.5f) * 4f);
                DrawStroke(pixels, size, previous, next, thickness * 0.7f, edge);
                DrawStroke(pixels, size, previous + Vector2.one, next + Vector2.one, thickness * 0.35f, highlight);
                previous = next;
            }
        }

        return ToSprite(pixels, size, $"BullseyeHealth_Crack_{layerIndex + 1:00}");
    }

    public static Sprite CreateShatteredOverlay(int size = DefaultSize)
    {
        Color[] pixels = CreateTransparent(size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var rng = new System.Random(77);

        Color veil = new Color(0.05f, 0.04f, 0.04f, 0.28f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float outer = Coverage(dist, radius, 1.4f);
                if (outer <= 0.001f)
                    continue;

                pixels[y * size + x] = Premultiply(veil, outer);
            }
        }

        Color edge = new Color(0.02f, 0.02f, 0.02f, 0.96f);
        Color highlight = new Color(1f, 0.98f, 0.94f, 0.9f);
        for (int i = 0; i < 14; i++)
        {
            float angle = i * (Mathf.PI * 2f / 14f) + (float)rng.NextDouble() * 0.2f;
            DrawJaggedCrack(
                pixels,
                size,
                center,
                angle,
                size * (0.38f + (float)rng.NextDouble() * 0.08f),
                1.8f,
                edge,
                highlight,
                rng);
        }

        PunchMissingShards(pixels, size, center, radius * 0.22f, rng);
        return ToSprite(pixels, size, "BullseyeHealth_Shattered");
    }

    public static Texture2D CreateTexture(Sprite sprite)
    {
        return sprite != null ? sprite.texture : null;
    }

    private static void DrawJaggedCrack(
        Color[] pixels,
        int size,
        Vector2 start,
        float angle,
        float length,
        float thickness,
        Color edge,
        Color highlight,
        System.Random rng)
    {
        int steps = Mathf.Max(10, Mathf.RoundToInt(length / 4f));
        Vector2 current = start;
        float currentAngle = angle;
        for (int i = 0; i < steps; i++)
        {
            currentAngle += ((float)rng.NextDouble() - 0.5f) * 0.55f;
            Vector2 next = current + AngleDirection(currentAngle) * (length / steps);
            DrawStroke(pixels, size, current, next, thickness, edge);
            DrawStroke(pixels, size, current + new Vector2(0.8f, 0.8f), next + new Vector2(0.8f, 0.8f), thickness * 0.4f, highlight);
            current = next;
        }
    }

    private static void DrawStroke(Color[] pixels, int size, Vector2 from, Vector2 to, float thickness, Color color)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance * 2f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            Stamp(pixels, size, point, thickness, color);
        }
    }

    private static void Stamp(Color[] pixels, int size, Vector2 point, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(point.x - radius - 1f));
        int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(point.x + radius + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(point.y - radius - 1f));
        int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(point.y + radius + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float coverage = Coverage(Vector2.Distance(new Vector2(x, y), point), radius, 0.85f);
                if (coverage <= 0.001f)
                    continue;

                int index = y * size + x;
                pixels[index] = Blend(pixels[index], Premultiply(color, coverage));
            }
        }
    }

    private static void PunchMissingShards(Color[] pixels, int size, Vector2 center, float holeRadius, System.Random rng)
    {
        int shardCount = 5;
        for (int s = 0; s < shardCount; s++)
        {
            float start = (float)rng.NextDouble() * Mathf.PI * 2f;
            float sweep = 0.35f + (float)rng.NextDouble() * 0.4f;
            float inner = holeRadius * (0.15f + (float)rng.NextDouble() * 0.35f);
            float outer = holeRadius * (1.1f + (float)rng.NextDouble() * 0.7f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float dist = delta.magnitude;
                    if (dist < inner || dist > outer)
                        continue;

                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, start * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (deltaAngle > sweep * 0.5f)
                        continue;

                    int index = y * size + x;
                    Color pixel = pixels[index];
                    pixel.a *= 0.08f;
                    pixels[index] = pixel;
                }
            }
        }
    }

    private static Color[] CreateTransparent(int size)
    {
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        return pixels;
    }

    private static Sprite ToSprite(Color[] pixels, int size, string name)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Vector2 AngleDirection(float angle)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float Coverage(float distance, float radius, float aa)
    {
        if (aa <= 0.0001f)
            return distance <= radius ? 1f : 0f;

        return Mathf.Clamp01((radius - distance) / aa + 1f);
    }

    private static Color Premultiply(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    private static Color Blend(Color under, Color over)
    {
        float outAlpha = over.a + under.a * (1f - over.a);
        if (outAlpha <= 0.0001f)
            return Color.clear;

        Color result = (over * over.a + under * under.a * (1f - over.a)) / outAlpha;
        result.a = outAlpha;
        return result;
    }
}
