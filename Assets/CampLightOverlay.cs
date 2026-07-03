using UnityEngine;

public class CampLightOverlay : MonoBehaviour
{
    [Header("Renderer")]
    public SpriteRenderer spriteRenderer;

    [Header("Darkness")]
    [Range(0f, 1f)] public float baseAlpha = 0.35f;
    [Range(0f, 2f)] public float darknessAmount = 1f;
    public Color tintColor = Color.black;

    [Header("Ambient Breathing")]
    public bool enableBreathing = true;
    public float breathSpeed = 0.08f;
    [Range(0f, 0.1f)] public float breathAmplitude = 0.015f;

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

        float breath = 0f;
        if (enableBreathing && Application.isPlaying)
            breath = Mathf.Sin(Time.time * Mathf.Max(0f, breathSpeed)) * Mathf.Max(0f, breathAmplitude);

        Color color = tintColor;
        color.a = Mathf.Clamp01(baseAlpha * darknessAmount + breath);
        spriteRenderer.color = color;
    }
}
