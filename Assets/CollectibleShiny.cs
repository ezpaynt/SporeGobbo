using UnityEngine;

public class CollectibleShiny : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    public int amount = 1;
    public float pickupRange = 1.1f;
    public string prompt = "Pick Up Shiny";

    public int InteractionPriority => 0;
    public float InteractionRange => pickupRange;
    public bool CanInteract(GobboController player) => player != null;
    public Vector2 GetInteractionPoint() => transform.position;
    public string GetInteractPrompt() => prompt;

    public void Interact(GobboController player)
    {
        if (player == null) return;

        if (GameState.Instance != null)
            CampResourceService.Add(GameState.Instance, CampResourceType.Shinies, amount, false);

        Debug.Log("Picked up shiny!");
        Destroy(gameObject);
    }
}
