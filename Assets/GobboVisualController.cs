using UnityEngine;

public class GobboVisualController : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public GobboAnimationState currentState = GobboAnimationState.Idle;

    [Header("Runtime Visual Identity")]
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public string visualSetId = "";

    [Header("Diagnostics")]
    public bool warnWhenVisualMissing = true;
    public bool warnWhenSpriteOverridden = false;

    private GobboVisualSet currentSet;
    private Vector2 lastDirection = Vector2.down;
    private Sprite lastAssignedSprite;
    private GobboVisualDirectionSlot lastDirectionSlot = GobboVisualDirectionSlot.Front;
    private int currentFrameIndex;
    private int currentFrameCount = 1;
    private float frameTimer;
    private bool warnedMissingRenderer;
    private bool warnedMissingSet;
    private bool warnedMissingSprite;

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

    void Update()
    {
        AdvanceFrameTimer(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (currentSet == null && GobboVisualDatabase.Instance != null)
            RefreshVisual();

        if (warnWhenSpriteOverridden && spriteRenderer != null && lastAssignedSprite != null && spriteRenderer.sprite != lastAssignedSprite)
        {
            Debug.LogWarning(
                "[GobboVisualController] Sprite changed after assignment on " + name +
                " | expected=" + SpriteName(lastAssignedSprite) +
                " | actual=" + SpriteName(spriteRenderer.sprite),
                this);
            lastAssignedSprite = spriteRenderer.sprite;
        }
    }

    public void ApplyIdentity(BuddyType type, GobboAgeStage stage, string setId)
    {
        bool identityChanged = gobboType != type || ageStage != stage || visualSetId != setId;

        gobboType = type;
        ageStage = stage;
        visualSetId = setId;

        if (identityChanged)
        {
            currentSet = null;
            warnedMissingSet = false;
            warnedMissingSprite = false;
            ResetFrameLoop();
        }

        if (identityChanged || currentSet == null)
            RefreshVisual();
    }

    public void SetAnimationState(GobboAnimationState state)
    {
        if (currentState == state)
            return;

        currentState = state;
        warnedMissingSprite = false;
        ResetFrameLoop();
        SetDirection(lastDirection);
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            lastDirection = direction.normalized;

        if (spriteRenderer == null)
        {
            WarnMissingRendererOnce();
            return;
        }

        if (currentSet == null)
            RefreshSet();

        if (currentSet == null)
        {
            WarnMissingSetOnce();
            return;
        }

        GobboVisualPickResult pick = currentSet.PickSpriteDetailed(currentState, lastDirection, currentFrameIndex);

        if (pick != null && pick.requestedDirectionSlot != lastDirectionSlot)
        {
            lastDirectionSlot = pick.requestedDirectionSlot;
            ResetFrameLoop();
            pick = currentSet.PickSpriteDetailed(currentState, lastDirection, currentFrameIndex);
        }

        Sprite chosen = pick != null ? pick.selectedSprite : null;

        if (chosen == null)
        {
            GobboAnimationState fallbackState;
            GobboVisualDirectionSlot fallbackDirection;
            chosen = currentSet.PickFirstAvailableSprite(out fallbackState, out fallbackDirection);
            currentFrameCount = 1;
        }
        else
        {
            currentFrameCount = Mathf.Max(1, pick.selectedFrameCount);
        }

        if (chosen == null)
        {
            WarnMissingSpriteOnce(pick);
            return;
        }

        spriteRenderer.sprite = chosen;
        lastAssignedSprite = chosen;
    }

    public void RefreshVisual()
    {
        RefreshSet();
        SetDirection(lastDirection);
    }

    [ContextMenu("Force Refresh Visual")]
    public void ForceRefreshVisual()
    {
        Debug.Log("[GobboVisualController] ForceRefreshVisual called on " + name, this);
        RefreshVisual();
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

    void AdvanceFrameTimer(float deltaTime)
    {
        if (currentSet == null || currentFrameCount <= 1)
            return;

        frameTimer += deltaTime;
        float frameDuration = currentSet.GetFrameDuration(currentState);

        if (frameTimer < frameDuration)
            return;

        frameTimer %= frameDuration;
        currentFrameIndex = (currentFrameIndex + 1) % currentFrameCount;
        SetDirection(lastDirection);
    }

    void ResetFrameLoop()
    {
        currentFrameIndex = 0;
        currentFrameCount = 1;
        frameTimer = 0f;
    }

    void WarnMissingRendererOnce()
    {
        if (!warnWhenVisualMissing || warnedMissingRenderer)
            return;

        warnedMissingRenderer = true;
        Debug.LogWarning("[GobboVisualController] No SpriteRenderer assigned/found on " + name + ".", this);
    }

    void WarnMissingSetOnce()
    {
        if (!warnWhenVisualMissing || warnedMissingSet)
            return;

        warnedMissingSet = true;
        Debug.LogWarning(
            "[GobboVisualController] No GobboVisualSet found" +
            " | object=" + name +
            " | visualSetId=" + visualSetId +
            " | type=" + gobboType +
            " | stage=" + ageStage +
            " | databaseFound=" + (GobboVisualDatabase.Instance != null),
            this);
    }

    void WarnMissingSpriteOnce(GobboVisualPickResult pick)
    {
        if (!warnWhenVisualMissing || warnedMissingSprite)
            return;

        warnedMissingSprite = true;
        string availableSprites = currentSet != null ? currentSet.GetAvailableSpriteSummary() : "no visual set";
        string pickDetails = pick == null
            ? "pick=NULL"
            : "requestedAction=" + pick.requestedState +
              " | resolvedAction=" + pick.resolvedState +
              " | requestedDirection=" + pick.requestedDirectionSlot +
              " | selectedDirection=" + pick.selectedDirectionSlot +
              " | frame=" + pick.selectedFrameIndex + "/" + pick.selectedFrameCount;

        Debug.LogWarning(
            "[GobboVisualController] No sprite available for visual request" +
            " | object=" + name +
            " | renderer=" + (spriteRenderer != null ? spriteRenderer.name : "NULL") +
            " | visualSetId=" + visualSetId +
            " | type=" + gobboType +
            " | stage=" + ageStage +
            " | currentState=" + currentState +
            " | facing=" + lastDirection +
            " | resolvedSet=" + (currentSet != null ? currentSet.visualSetId : "NULL") +
            " | " + pickDetails +
            " | availableSprites=" + availableSprites,
            this);
    }

    string SpriteName(Sprite sprite)
    {
        return sprite != null ? sprite.name : "NULL";
    }
}
