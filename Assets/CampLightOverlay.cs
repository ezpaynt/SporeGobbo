using UnityEngine;

public class CampLightOverlay : MonoBehaviour
{
    [Header("Renderer")]
    public SpriteRenderer spriteRenderer;

    [Header("Overlay Alpha")]
    [Range(0f, 1f)] public float baseAlpha = 0.35f;
    [Range(0f, 0.1f)] public float flickerAmount = 0.015f;
    public float flickerSpeed = 0.08f;

    void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        ApplyOverlay();
    }

    void Update()
    {
        ApplyOverlay();
    }

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyOverlay();
    }

    void ApplyOverlay()
    {
        if (spriteRenderer == null)
            return;

        float flicker = 0f;
        if (Application.isPlaying && flickerAmount > 0f && flickerSpeed > 0f)
            flicker = Mathf.Sin(Time.time * flickerSpeed) * flickerAmount;

        Color color = spriteRenderer.color;
        color.a = Mathf.Clamp01(baseAlpha + flicker);
        spriteRenderer.color = color;
    }
}
