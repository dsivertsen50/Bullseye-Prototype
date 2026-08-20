using UnityEngine;

internal static class UiWhiteSprite
{
    private static Sprite sprite;

    public static Sprite Get()
    {
        if (sprite != null)
            return sprite;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();

        sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
