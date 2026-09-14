using System.Collections.Generic;
using UnityEngine;

public static class R01VisualFactory
{
    private static readonly Dictionary<string, Sprite> CircleSprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> RingSprites = new Dictionary<string, Sprite>();
    private static Sprite rectangleSprite;

    public static GameObject CreateCircle(string name, Transform parent, float radius, Color color, int sortingOrder)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = GetCircleSprite(color);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        result.transform.localScale = Vector3.one * (radius * 2f);
        return result;
    }

    public static GameObject CreateRing(string name, Transform parent, float radius, Color color, int sortingOrder)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRingSprite(color);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        result.transform.localScale = Vector3.one * (radius * 2f);
        return result;
    }

    public static GameObject CreateRect(string name, Transform parent, float width, float height, Color color, int sortingOrder)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRectangleSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        result.transform.localScale = new Vector3(width, height, 1f);
        return result;
    }

    public static SpriteRenderer GetRenderer(GameObject gameObject)
    {
        return gameObject == null ? null : gameObject.GetComponent<SpriteRenderer>();
    }

    private static Sprite GetCircleSprite(Color color)
    {
        string key = "circle_" + ColorUtility.ToHtmlStringRGBA(color);
        if (CircleSprites.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 1f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = key;
        CircleSprites.Add(key, sprite);
        return sprite;
    }

    private static Sprite GetRingSprite(Color color)
    {
        string key = "ring_" + ColorUtility.ToHtmlStringRGBA(color);
        if (RingSprites.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.5f - 2f;
        float innerRadius = outerRadius - 5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= outerRadius && distance >= innerRadius ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = key;
        RingSprites.Add(key, sprite);
        return sprite;
    }

    private static Sprite GetRectangleSprite()
    {
        if (rectangleSprite != null)
        {
            return rectangleSprite;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "R01_Rectangle";
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply(false, true);
        rectangleSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        return rectangleSprite;
    }
}

public sealed class R01PulseView : MonoBehaviour
{
    private SpriteRenderer pulseRenderer;
    private Color baseColor;
    private float elapsed;
    private float duration;
    private float startScale;
    private float targetScale;

    public void Initialize(SpriteRenderer targetRenderer, Color color, float pulseDuration, float initialScale, float finalScale)
    {
        pulseRenderer = targetRenderer;
        baseColor = color;
        duration = Mathf.Max(0.05f, pulseDuration);
        startScale = initialScale;
        targetScale = finalScale;
        transform.localScale = Vector3.one * startScale;
        pulseRenderer.color = baseColor;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);
        Color color = baseColor;
        color.a = Mathf.Lerp(baseColor.a, 0f, t);
        if (pulseRenderer != null)
        {
            pulseRenderer.color = color;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
