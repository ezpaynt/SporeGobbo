using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RunRetreatPortal : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    [Header("Return")]
    public string campSceneName = "CampScene";
    public bool saveRunBeforeLeaving = true;
    public bool saveSlotAfterRunCommit = true;

    [Header("Interaction")]
    public float promptRange = 1.15f;
    public string interactPrompt = "Retreat to Camp";

    public int InteractionPriority => 0;
    public float InteractionRange => promptRange;
    public bool CanInteract(GobboController playerController) => playerController != null;
    public Vector2 GetInteractionPoint() => transform.position;

    void Awake()
    {
        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController playerController)
    {
        if (playerController == null)
            return;

        RunReturnService.ReturnToCamp(
            campSceneName,
            saveRunBeforeLeaving,
            saveSlotAfterRunCommit,
            RunReturnReason.Retreat,
            "spawn retreat portal");
    }

}
