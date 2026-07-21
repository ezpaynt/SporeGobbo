using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup : MonoBehaviour, ICampInteractable
{
    [Header("Item")]
    public ItemDefinition itemDefinition;
    public int quantity = 1;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public bool useItemIconAsSprite = true;

    [Header("Interaction")]
    public string prompt = "Pick up snack";
    public bool allowDirectEInteractionFallback = true;
    public float pickupRange = 1.1f;
    public KeyCode pickupKey = KeyCode.E;
    public string playerTag = "Player";

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

    void Update()
    {
        if (!allowDirectEInteractionFallback || collected || !Input.GetKeyDown(pickupKey))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;
        if (Vector2.Distance(transform.position, player.transform.position) > pickupRange) return;

        Interact(player.GetComponent<GobboController>());
    }

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
