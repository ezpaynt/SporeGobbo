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
    public bool logVisualDiagnostics = true;
    public bool logEverySpriteSelection = false;

    private GobboVisualSet currentSet;
    private Vector2 lastDirection = Vector2.down;
    private Sprite lastAssignedSprite;
    private bool hasLoggedInitialDiagnostic;

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

        if (logVisualDiagnostics && spriteRenderer != null && lastAssignedSprite != null && spriteRenderer.sprite != lastAssignedSprite)
        {
            Debug.LogWarning(
                "[GobboVisualController] Sprite changed after assignment on " + name +
                " | expected=" + SpriteName(lastAssignedSprite) +
                " | actual=" + SpriteName(spriteRenderer.sprite) +
                " | possible override by another script or inspector state",
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

        if (identityChanged || currentSet == null)
            RefreshVisual();
    }

    public void SetAnimationState(GobboAnimationState state)
    {
        if (currentState == state)
            return;

        currentState = state;
        SetDirection(lastDirection);
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            lastDirection = direction.normalized;

        Sprite before = spriteRenderer != null ? spriteRenderer.sprite : null;
        bool databaseFound = GobboVisualDatabase.Instance != null;

        if (spriteRenderer == null)
        {
            LogSelection(null, databaseFound, false, before, null, "No SpriteRenderer assigned/found.");
            return;
        }

        if (currentSet == null)
            RefreshSet();

        bool setFound = currentSet != null;

        if (currentSet == null)
        {
            LogSelection(null, databaseFound, false, before, spriteRenderer.sprite, "No matching GobboVisualSet found.");
            return;
        }

        GobboVisualPickResult pick = currentSet.PickSpriteDetailed(currentState, lastDirection);
        Sprite chosen = pick != null ? pick.selectedSprite : null;
        string note = "Assigned selected sprite.";

        if (chosen == null)
        {
            GobboAnimationState fallbackState;
            GobboVisualDirectionSlot fallbackDirection;
            chosen = currentSet.PickFirstAvailableSprite(out fallbackState, out fallbackDirection);

            if (chosen != null)
            {
                note = "Selected sprite was NULL; assigned first available fallback sprite instead: action=" + fallbackState + " direction=" + fallbackDirection + ".";
            }
            else
            {
                note = "Selected sprite was NULL and no fallback sprites exist in this visual set; renderer was not changed.";
            }
        }

        if (chosen != null)
        {
            spriteRenderer.sprite = chosen;
            lastAssignedSprite = chosen;
        }

        Sprite after = spriteRenderer.sprite;
        LogSelection(pick, databaseFound, setFound, before, after, note);
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

    void LogSelection(GobboVisualPickResult pick, bool databaseFound, bool setFound, Sprite before, Sprite after, string note)
    {
        if (!logVisualDiagnostics)
            return;

        bool selectedSpriteWasNull = pick == null || pick.selectedSprite == null;
        if (!logEverySpriteSelection && hasLoggedInitialDiagnostic && !selectedSpriteWasNull && pick.selectedSprite == lastAssignedSprite)
            return;

        hasLoggedInitialDiagnostic = true;

        string setId = currentSet != null ? currentSet.visualSetId : "NULL";
        string resolvedType = currentSet != null ? currentSet.gobboType.ToString() : "NULL";
        string resolvedStage = currentSet != null ? currentSet.ageStage.ToString() : "NULL";
        string availableSprites = currentSet != null ? currentSet.GetAvailableSpriteSummary() : "no visual set";

        string pickDetails = pick == null
            ? "pick=NULL"
            : "requestedAction=" + pick.requestedState +
              " | resolvedAction=" + pick.resolvedState +
              " | requestedDirectionSlot=" + pick.requestedDirectionSlot +
              " | selectedDirectionSlot=" + pick.selectedDirectionSlot +
              " | selectedSprite=" + SpriteName(pick.selectedSprite) +
              " | actionFallback=" + pick.usedActionFallback +
              " | directionFallback=" + pick.usedDirectionFallback +
              " | actionSetNull=" + pick.actionSetWasNull +
              " | actionSetEmpty=" + pick.actionSetWasEmpty;

        string message =
            "[GobboVisualController] " + note + "\n" +
            "object=" + name +
            " | spriteRenderer=" + (spriteRenderer != null ? spriteRenderer.name : "NULL") +
            " | requestedVisualSetId=" + visualSetId +
            " | requestedType=" + gobboType +
            " | requestedStage=" + ageStage +
            " | currentState=" + currentState +
            " | facing=" + lastDirection +
            " | databaseFound=" + databaseFound +
            " | setFound=" + setFound +
            " | resolvedSetId=" + setId +
            " | resolvedType=" + resolvedType +
            " | resolvedStage=" + resolvedStage +
            " | spriteBefore=" + SpriteName(before) +
            " | spriteAfter=" + SpriteName(after) +
            "\n" + pickDetails +
            "\navailableSprites=" + availableSprites;

        if (selectedSpriteWasNull)
            Debug.LogWarning(message, this);
        else
            Debug.Log(message, this);
    }

    string SpriteName(Sprite sprite)
    {
        return sprite != null ? sprite.name : "NULL";
    }
}
