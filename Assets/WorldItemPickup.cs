using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    [Header("Item")]
    public ItemDefinition itemDefinition;
    public int quantity = 1;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public bool useItemIconAsSprite = true;

    [Header("Interaction")]
    public string prompt = "Pick up snack";
    public float pickupRange = 1.1f;

    private bool collected;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null) trigger.isTrigger = true;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ApplySprite();
    }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ApplySprite();
    }

    public int InteractionPriority => 0;
    public float InteractionRange => pickupRange;
    public bool CanInteract(GobboController player) => !collected && player != null;
    public Vector2 GetInteractionPoint() => transform.position;

    public string GetInteractPrompt()
    {
        if (collected) return "";
        ItemDefinition item = itemDefinition;
        if (item == null) return prompt;
        return string.IsNullOrWhiteSpace(prompt) ? "Pick up " + item.GetDisplayName() : prompt;
    }

    public void Interact(GobboController player)
    {
        TryCollect();
    }

    public bool TryCollect()
    {
        if (collected) return false;
        if (itemDefinition == null)
        {
            CampMessageUI.Show("Missing snack definition.");
            Debug.LogWarning("WorldItemPickup has no ItemDefinition assigned.", this);
            return false;
        }

        GameState state = GameState.Instance;
        if (state == null)
        {
            CampMessageUI.Show("No game state found.");
            Debug.LogWarning("WorldItemPickup could not find GameState.", this);
            return false;
        }

        int amount = Mathf.Max(1, quantity);
        if (!RunSnackLootService.AddRunSnack(state, itemDefinition, amount))
        {
            CampMessageUI.Show("Could not collect " + itemDefinition.GetDisplayName() + ".");
            return false;
        }

        collected = true;
        CampMessageUI.Show("Found " + itemDefinition.GetDisplayName() + " x" + amount + ".");
        Destroy(gameObject);
        return true;
    }

    public void RefreshVisual()
    {
        ApplySprite();
    }

    void ApplySprite()
    {
        if (!useItemIconAsSprite || spriteRenderer == null || itemDefinition == null || itemDefinition.icon == null)
            return;

        spriteRenderer.sprite = itemDefinition.icon;
    }

    void OnValidate()
    {
        if (quantity < 1) quantity = 1;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ApplySprite();
    }
}
