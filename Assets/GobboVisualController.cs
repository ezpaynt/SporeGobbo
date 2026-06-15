using UnityEngine;

public class GobboVisualController : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public GobboAnimationState currentState = GobboAnimationState.Idle;

    [Header("Runtime Visual Identity")]
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public string visualSetId = "";

    private GobboVisualSet currentSet;
    private Vector2 lastDirection = Vector2.down;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable()
    {
        RefreshVisual();
    }

    void Start()
    {
        RefreshVisual();
    }

    void LateUpdate()
    {
        if (currentSet == null && GobboVisualDatabase.Instance != null)
            RefreshVisual();
    }

    public void ApplyIdentity(BuddyType type, GobboAgeStage stage, string setId)
    {
        gobboType = type;
        ageStage = stage;
        visualSetId = setId;
        RefreshVisual();
    }

    public void SetAnimationState(GobboAnimationState state)
    {
        currentState = state;
        SetDirection(lastDirection);
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            lastDirection = direction.normalized;

        if (spriteRenderer == null)
            return;

        if (currentSet == null)
            RefreshSet();

        if (currentSet == null)
            return;

        DirectionalSpriteSet sprites = currentSet.GetSprites(currentState);
        Sprite chosen = sprites != null ? sprites.PickForDirection(lastDirection) : null;

        if (chosen != null)
            spriteRenderer.sprite = chosen;
    }

    public void RefreshVisual()
    {
        RefreshSet();
        SetDirection(lastDirection);
    }

    void RefreshSet()
    {
        if (GobboVisualDatabase.Instance == null)
        {
            currentSet = null;
            return;
        }

        currentSet = GobboVisualDatabase.Instance.GetVisualSet(visualSetId, gobboType, ageStage);
    }
}
