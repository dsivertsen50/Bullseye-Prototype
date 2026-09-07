using UnityEngine;

/// <summary>
/// Runtime / editor placeholder optic art. Assigned ScopeDefinition sprites
/// replace these without code changes.
/// </summary>
public static class ScopePlaceholderSprites
{
    private const int DefaultSize = 512;

    private static Sprite hole;
    private static Sprite housing;
    private static Sprite vignette;
    private static Sprite reticle;
    private static Sprite dot;

    public static Sprite Hole => hole != null ? hole : hole = CreateSprite(CreateHoleTexture(DefaultSize), "ScopeHole");
    public static Sprite Housing => housing != null ? housing : housing = CreateSprite(CreateHousingTexture(DefaultSize), "ScopeHousing");
    public static Sprite Vignette => vignette != null ? vignette : vignette = CreateSprite(CreateVignetteTexture(DefaultSize), "ScopeVignette");
    public static Sprite Reticle => reticle != null ? reticle : reticle = CreateSprite(CreateReticleTexture(DefaultSize), "ScopeReticle");
    public static Sprite Dot => dot != null ? dot : dot = CreateSprite(CreateDotTexture(64), "ScopeDot");

    public static Texture2D CreateHoleTexture(int size)
    {
        var texture = CreateTexture(size);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Radius(x, y, half);
                float t = Mathf.InverseLerp(0.985f, 1f, r);
                float alpha = t * t * (3f - 2f * t);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateHousingTexture(int size)
    {
        var texture = CreateTexture(size);
        float half = size * 0.5f;
        const float inner = 0.88f;
        const float lip = 0.93f;
        const float outerFade = 1.02f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Radius(x, y, half);
                float ring = Mathf.Clamp01(SmoothEdge(r, inner, lip) - SmoothEdge(r, 0.995f, outerFade));
                float innerLip = Mathf.Clamp01(1f - Mathf.Abs(r - inner) / 0.018f);
                innerLip *= 1f - SmoothEdge(r, inner, inner + 0.01f);
                float alpha = Mathf.Max(ring, innerLip * 0.85f);
                float rgb = Mathf.Clamp01(0.08f + innerLip * 0.55f);
                texture.SetPixel(x, y, new Color(rgb, rgb, rgb, alpha));
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateVignetteTexture(int size)
    {
        var texture = CreateTexture(size);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Radius(x, y, half);
                float inside = 1f - SmoothEdge(r, 0.98f, 1.0f);
                float radial = Mathf.Clamp01((r - 0.22f) / 0.76f);
                radial *= radial;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, radial * inside));
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateReticleTexture(int size)
    {
        var texture = CreateTexture(size);
        Clear(texture, Color.clear);

        int center = size / 2;
        int outer = Mathf.RoundToInt(size * 0.10f);
        int gap = Mathf.Max(2, Mathf.RoundToInt(size * 0.028f));
        DrawOutlinedCross(texture, center, outer, size - outer, gap);
        DrawOutlinedDot(texture, center, Mathf.Max(2, size / 128));

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateSniperHousingTexture(int size)
    {
        var texture = CreateTexture(size);
        float half = size * 0.5f;
        const float inner = 0.72f;
        const float lip = 0.82f;
        const float metal = 0.94f;
        const float outerFade = 1.02f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Radius(x, y, half);
                float ring = Mathf.Clamp01(SmoothEdge(r, inner, lip) - SmoothEdge(r, metal, outerFade));
                float bevel = Mathf.Clamp01(1f - Mathf.Abs(r - inner) / 0.02f);
                bevel *= 1f - SmoothEdge(r, inner, inner + 0.012f);
                float outerBevel = Mathf.Clamp01(1f - Mathf.Abs(r - metal) / 0.018f);
                outerBevel *= SmoothEdge(r, metal - 0.03f, metal);
                float alpha = Mathf.Max(ring, Mathf.Max(bevel * 0.95f, outerBevel * 0.65f));
                float rgb = Mathf.Clamp01(0.03f + bevel * 0.42f + outerBevel * 0.18f);
                texture.SetPixel(x, y, new Color(rgb, rgb, rgb, alpha));
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateSniperVignetteTexture(int size)
    {
        var texture = CreateTexture(size);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Radius(x, y, half);
                float inside = 1f - SmoothEdge(r, 0.97f, 1.0f);
                float radial = Mathf.Clamp01((r - 0.08f) / 0.9f);
                radial *= radial;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, radial * inside * 0.92f));
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateSniperReticleTexture(int size)
    {
        var texture = CreateTexture(size);
        Clear(texture, Color.clear);

        int center = size / 2;
        int outer = Mathf.RoundToInt(size * 0.08f);
        int outerEnd = size - outer;
        int gap = Mathf.Max(3, Mathf.RoundToInt(size * 0.018f));
        DrawOutlinedCross(texture, center, outer, outerEnd, gap);
        DrawMilMarks(texture, center, gap, outer, outerEnd);
        DrawOutlinedDot(texture, center, Mathf.Max(1, size / 220));

        texture.Apply(false, false);
        return texture;
    }

    public static Texture2D CreateDotTexture(int size)
    {
        var texture = CreateTexture(size);
        Clear(texture, Color.clear);
        int center = size / 2;
        int radius = Mathf.Max(2, size / 2 - 2);
        FillCircle(texture, center, center, radius + 1, new Color(0f, 0f, 0f, 0.7f));
        FillCircle(texture, center, center, radius, Color.white);
        texture.Apply(false, false);
        return texture;
    }

    private static void DrawOutlinedCross(Texture2D texture, int center, int outer, int outerEnd, int gap)
    {
        DrawArm(texture, center, outer, outerEnd, gap, 3, new Color(0f, 0f, 0f, 0.7f));
        DrawArm(texture, center, outer, outerEnd, gap, 1, Color.white);
    }

    private static void DrawArm(Texture2D texture, int center, int outer, int outerEnd, int gap, int thickness, Color color)
    {
        int half = thickness / 2;
        int leftEnd = center - gap;
        int rightStart = center + gap;
        for (int i = -half; i <= half; i++)
        {
            int y = center + i;
            for (int x = outer; x < leftEnd; x++)
                texture.SetPixel(x, y, color);
            for (int x = rightStart; x <= outerEnd; x++)
                texture.SetPixel(x, y, color);

            int xArm = center + i;
            for (int yArm = outer; yArm < leftEnd; yArm++)
                texture.SetPixel(xArm, yArm, color);
            for (int yArm = rightStart; yArm <= outerEnd; yArm++)
                texture.SetPixel(xArm, yArm, color);
        }
    }

    private static void DrawOutlinedDot(Texture2D texture, int center, int radius)
    {
        FillCircle(texture, center, center, radius + 1, new Color(0f, 0f, 0f, 0.75f));
        FillCircle(texture, center, center, radius, Color.white);
    }

    private static void DrawMilMarks(Texture2D texture, int center, int gap, int outer, int outerEnd)
    {
        int armSpan = Mathf.Max(1, (center - gap) - outer);
        int step = Mathf.Max(8, armSpan / 8);
        int shortHash = Mathf.Max(4, texture.width / 64);
        int longHash = Mathf.Max(7, texture.width / 40);
        Color outline = new Color(0f, 0f, 0f, 0.7f);

        for (int i = 1; i <= 8; i++)
        {
            int dist = gap + step * i;
            if (center - dist < outer || center + dist > outerEnd)
                break;

            int hash = (i % 2 == 0) ? longHash : shortHash;
            PlotHash(texture, center, center - dist, hash, true, outline);
            PlotHash(texture, center, center - dist, hash - 2, true, Color.white);
            PlotHash(texture, center, center + dist, hash, true, outline);
            PlotHash(texture, center, center + dist, hash - 2, true, Color.white);
            PlotHash(texture, center - dist, center, hash, false, outline);
            PlotHash(texture, center - dist, center, hash - 2, false, Color.white);
            PlotHash(texture, center + dist, center, hash, false, outline);
            PlotHash(texture, center + dist, center, hash - 2, false, Color.white);
        }
    }

    private static void PlotHash(Texture2D texture, int x, int y, int length, bool horizontal, Color color)
    {
        int half = Mathf.Max(1, length / 2);
        if (horizontal)
        {
            for (int i = -half; i <= half; i++)
            {
                int px = x + i;
                if (px >= 0 && px < texture.width && y >= 0 && y < texture.height)
                    texture.SetPixel(px, y, color);
            }
        }
        else
        {
            for (int i = -half; i <= half; i++)
            {
                int py = y + i;
                if (x >= 0 && x < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(x, py, color);
            }
        }
    }

    private static void FillCircle(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > r2)
                    continue;
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(px, py, color);
            }
        }
    }

    private static Texture2D CreateTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite CreateSprite(Texture2D texture, string name)
    {
        texture.name = name;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width,
            0,
            SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static void Clear(Texture2D texture, Color color)
    {
        Color[] pixels = new Color[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        texture.SetPixels(pixels);
    }

    private static float Radius(int x, int y, float half)
    {
        float dx = (x + 0.5f - half) / half;
        float dy = (y + 0.5f - half) / half;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static float SmoothEdge(float value, float from, float to)
    {
        float t = Mathf.InverseLerp(from, to, value);
        return t * t * (3f - 2f * t);
    }
}
