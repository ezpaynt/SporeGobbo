using UnityEngine;

public class DirectionalSpriteController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite front;
    public Sprite frontLeft;
    public Sprite frontRight;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    [Header("Renderer")]
    public SpriteRenderer spriteRenderer;

    public Vector2 LastDirection { get; private set; } = Vector2.down;

    private Sprite fallbackSprite;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            fallbackSprite = spriteRenderer.sprite;
    }

    public void SetDirection(Vector2 direction)
    {
        if (spriteRenderer == null)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();
        LastDirection = direction;

        Sprite next = GetSpriteForDirection(direction);
        if (next != null)
            spriteRenderer.sprite = next;
        else if (fallbackSprite != null)
            spriteRenderer.sprite = fallbackSprite;
    }

    private Sprite GetSpriteForDirection(Vector2 direction)
    {
        if (direction.y > 0.35f)
        {
            if (direction.x < -0.35f && backLeft != null)
                return backLeft;
            if (direction.x > 0.35f && backRight != null)
                return backRight;
            return back;
        }

        if (direction.x < -0.35f && frontLeft != null)
            return frontLeft;

        if (direction.x > 0.35f && frontRight != null)
            return frontRight;

        return front;
    }
}
