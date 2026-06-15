using UnityEngine;

public class BuddyDirectionalSprite : MonoBehaviour
{
    public Sprite front;
    public Sprite frontLeft;
    public Sprite frontRight;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    public SpriteRenderer spriteRenderer;

    private GobboVisualController visualController;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
    }

    public void SetDirection(Vector2 direction)
    {
        if (visualController != null)
            return;

        if (spriteRenderer == null)
            return;

        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.Normalize();

        if (direction.y > 0.35f)
        {
            if (direction.x < -0.35f && backLeft != null)
                spriteRenderer.sprite = backLeft;
            else if (direction.x > 0.35f && backRight != null)
                spriteRenderer.sprite = backRight;
            else if (back != null)
                spriteRenderer.sprite = back;
        }
        else
        {
            if (direction.x < -0.35f && frontLeft != null)
                spriteRenderer.sprite = frontLeft;
            else if (direction.x > 0.35f && frontRight != null)
                spriteRenderer.sprite = frontRight;
            else if (front != null)
                spriteRenderer.sprite = front;
        }
    }
}
