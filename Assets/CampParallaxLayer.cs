using UnityEngine;

public class CampParallaxLayer : MonoBehaviour
{
    [Header("Renderer")]
    public SpriteRenderer spriteRenderer;

    [Header("Parallax")]
    public float parallaxStrengthX;
    public float parallaxStrengthY;
    public Vector2 positionOffset;

    [Header("Rendering")]
    public int sortingOrder;
    public bool applyTintAndAlpha;
    public Color tintColor = Color.white;
    [Range(0f, 1f)] public float alpha = 1f;

    private Vector3 initialLocalPosition;
    private bool initialized;

    void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        Initialize();
        ApplyRendererSettings();
    }

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyRendererSettings();
    }

    public void Initialize()
    {
        if (initialized)
            return;

        initialLocalPosition = transform.localPosition;
        initialized = true;
    }

    public void ApplyParallax(Vector3 cameraDelta)
    {
        Initialize();

        Vector3 parallaxOffset = new Vector3(
            cameraDelta.x * parallaxStrengthX,
            cameraDelta.y * parallaxStrengthY,
            0f);

        transform.localPosition = initialLocalPosition + (Vector3)positionOffset + parallaxOffset;
    }

    public void ApplyRendererSettings()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sortingOrder = sortingOrder;

        if (!applyTintAndAlpha)
            return;

        Color color = tintColor;
        color.a *= Mathf.Clamp01(alpha);
        spriteRenderer.color = color;
    }
}