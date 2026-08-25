using UnityEngine;

/// <summary>
/// Temporary FRONT / BACK body diagrams and bullseye marker art.
/// Final sprites replace these through Inspector fields on BullseyeBodyHud.
/// </summary>
public static class BullseyeBodyHudPlaceholders
{
    public const int BodyWidth = 256;
    public const int BodyHeight = 384;
    public const int MarkerSize = 64;

    /// <summary>
    /// Marker placement region inside the generated silhouette, in normalized
    /// sprite coordinates with origin at the bottom-left.
    /// </summary>
    public static readonly Rect BodyMapNormalized = new Rect(0.24f, 0.07f, 0.52f, 0.86f);

    public static Sprite CreateFrontBody()
    {
        return ToSprite(DrawBody(front: true), BodyWidth, BodyHeight, "BullseyeBody_Front");
    }

    public static Sprite CreateBackBody()
    {
        return ToSprite(DrawBody(front: false), BodyWidth, BodyHeight, "BullseyeBody_Back");
    }

    public static Sprite CreateMarker(int size = MarkerSize)
    {
        size = Mathf.Max(16, size);
        Color[] pixels = CreateTransparent(size, size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;

        Color cream = new Color(0.96f, 0.94f, 0.90f, 1f);
        Color red = new Color(0.82f, 0.12f, 0.16f, 1f);
        Color darkRed = new Color(0.42f, 0.05f, 0.08f, 1f);
        Color ink = new Color(0.08f, 0.07f, 0.07f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float outer = Coverage(dist, radius, 1.3f);
                if (outer <= 0.001f)
                    continue;

                float t = Mathf.Clamp01(dist / Mathf.Max(0.001f, radius));
                float band = t * 4f;
                int ring = Mathf.Clamp(Mathf.FloorToInt(band), 0, 3);
                Color ringColor = ring switch
                {
                    0 => darkRed,
                    1 => cream,
                    2 => red,
                    _ => cream
                };

                float boundary = Mathf.Abs(band - Mathf.Round(band));
                if (boundary < 0.06f && ring < 3)
                    ringColor = Color.Lerp(ringColor, ink, 1f - boundary / 0.06f);

                float rim = Coverage(dist, radius - 2.4f, 1.4f);
                ringColor = Color.Lerp(ink, ringColor, rim);
                pixels[y * size + x] = Premultiply(ringColor, outer);
            }
        }

        return ToSprite(pixels, size, size, "BullseyeBody_Marker");
    }

    private static Color[] DrawBody(bool front)
    {
        Color[] pixels = CreateTransparent(BodyWidth, BodyHeight);
        float cx = (BodyWidth - 1) * 0.5f;

        Color fill = front
            ? new Color(0.90f, 0.91f, 0.94f, 1f)
            : new Color(0.62f, 0.64f, 0.70f, 1f);
        Color outline = new Color(0.10f, 0.11f, 0.14f, 1f);
        Color accent = front
            ? new Color(0.18f, 0.20f, 0.24f, 1f)
            : new Color(0.22f, 0.18f, 0.16f, 1f);

        Vector2 head = new Vector2(cx, 328f);
        float headRadius = 30f;
        Vector2 neckTop = new Vector2(cx, 300f);
        Vector2 neckBottom = new Vector2(cx, 286f);
        Vector2 torsoTop = new Vector2(cx, 284f);
        Vector2 torsoBottom = new Vector2(cx, 176f);
        Vector2 leftShoulder = new Vector2(cx - 38f, 272f);
        Vector2 rightShoulder = new Vector2(cx + 38f, 272f);
        Vector2 leftHand = new Vector2(cx - 78f, 188f);
        Vector2 rightHand = new Vector2(cx + 78f, 188f);
        Vector2 leftHip = new Vector2(cx - 18f, 180f);
        Vector2 rightHip = new Vector2(cx + 18f, 180f);
        Vector2 leftFoot = new Vector2(cx - 34f, 28f);
        Vector2 rightFoot = new Vector2(cx + 34f, 28f);

        DrawCapsule(pixels, leftShoulder, leftHand, 11f, fill, outline);
        DrawCapsule(pixels, rightShoulder, rightHand, 11f, fill, outline);
        DrawCapsule(pixels, leftHip, leftFoot, 13f, fill, outline);
        DrawCapsule(pixels, rightHip, rightFoot, 13f, fill, outline);
        DrawCapsule(pixels, neckTop, neckBottom, 8f, fill, outline);
        DrawRoundedRect(pixels, torsoTop, torsoBottom, 34f, 8f, fill, outline);
        DrawCircle(pixels, head, headRadius, fill, outline);

        if (front)
        {
            DrawCircle(pixels, head + new Vector2(-9f, 4f), 4.2f, accent, accent);
            DrawCircle(pixels, head + new Vector2(9f, 4f), 4.2f, accent, accent);
            DrawCapsule(pixels, head + new Vector2(-7f, -10f), head + new Vector2(7f, -10f), 1.6f, accent, accent);
        }
        else
        {
            DrawCapsule(pixels, head + new Vector2(0f, 10f), new Vector2(cx, 188f), 2.4f, accent, accent);
        }

        return pixels;
    }

    private static void DrawRoundedRect(
        Color[] pixels,
        Vector2 top,
        Vector2 bottom,
        float halfWidth,
        float corner,
        Color fill,
        Color outline)
    {
        float minY = Mathf.Min(top.y, bottom.y);
        float maxY = Mathf.Max(top.y, bottom.y);
        float cx = (top.x + bottom.x) * 0.5f;
        float minX = cx - halfWidth;
        float maxX = cx + halfWidth;
        corner = Mathf.Min(corner, halfWidth, (maxY - minY) * 0.5f);

        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX - 3f));
        int x1 = Mathf.Min(BodyWidth - 1, Mathf.CeilToInt(maxX + 3f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(minY - 3f));
        int y1 = Mathf.Min(BodyHeight - 1, Mathf.CeilToInt(maxY + 3f));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float sdf = RoundedRectSdf(new Vector2(x, y), minX, minY, maxX, maxY, corner);
                Stamp(pixels, x, y, sdf, fill, outline);
            }
        }
    }

    private static void DrawCapsule(Color[] pixels, Vector2 a, Vector2 b, float radius, Color fill, Color outline)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 3f));
        int x1 = Mathf.Min(BodyWidth - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 3f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 3f));
        int y1 = Mathf.Min(BodyHeight - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 3f));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float sdf = CapsuleSdf(new Vector2(x, y), a, b, radius);
                Stamp(pixels, x, y, sdf, fill, outline);
            }
        }
    }

    private static void DrawCircle(Color[] pixels, Vector2 center, float radius, Color fill, Color outline)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 3f));
        int x1 = Mathf.Min(BodyWidth - 1, Mathf.CeilToInt(center.x + radius + 3f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 3f));
        int y1 = Mathf.Min(BodyHeight - 1, Mathf.CeilToInt(center.y + radius + 3f));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float sdf = Vector2.Distance(new Vector2(x, y), center) - radius;
                Stamp(pixels, x, y, sdf, fill, outline);
            }
        }
    }

    private static void Stamp(Color[] pixels, int x, int y, float sdf, Color fill, Color outline)
    {
        float fillAlpha = Coverage(sdf + 2.2f, 0f, 1.2f);
        float edge = Coverage(Mathf.Abs(sdf + 0.4f), 0f, 1.6f);
        if (fillAlpha <= 0.001f && edge <= 0.001f)
            return;

        Color color = Color.Lerp(fill, outline, edge * 0.85f);
        float alpha = Mathf.Max(fillAlpha, edge);
        int index = y * BodyWidth + x;
        pixels[index] = Blend(pixels[index], Premultiply(color, alpha));
    }

    private static float CapsuleSdf(Vector2 point, Vector2 a, Vector2 b, float radius)
    {
        Vector2 ab = b - a;
        float lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        return Vector2.Distance(point, a + ab * t) - radius;
    }

    private static float RoundedRectSdf(Vector2 point, float minX, float minY, float maxX, float maxY, float corner)
    {
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 half = new Vector2((maxX - minX) * 0.5f, (maxY - minY) * 0.5f);
        Vector2 q = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - (half - Vector2.one * corner);
        Vector2 maxQ = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return maxQ.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
    }

    private static Color[] CreateTransparent(int width, int height)
    {
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        return pixels;
    }

    private static Sprite ToSprite(Color[] pixels, int width, int height, string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
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
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
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
