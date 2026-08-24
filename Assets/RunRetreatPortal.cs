using TMPro;
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

    [Header("Legacy Prompt Visual (disabled; shared authority owns prompts)")]
    public GameObject promptObject;
    public TMP_Text promptText;

    public int InteractionPriority => 0;
    public float InteractionRange => promptRange;
    public bool CanInteract(GobboController playerController) => playerController != null;
    public Vector2 GetInteractionPoint() => transform.position;

    void Awake()
    {
        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

        if (promptObject == gameObject)
        {
            Debug.LogWarning("RunRetreatPortal Prompt Object must be a separate child, not the portal root. Ignoring the invalid assignment.", this);
            promptObject = null;
        }

        HidePrompt();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController playerController)
    {
        if (playerController == null)
            return;

        float distance = Vector2.Distance(transform.position, playerController.transform.position);
        if (distance > Mathf.Max(promptRange, playerController.interactRange))
            return;

        HidePrompt();
        RunReturnService.ReturnToCamp(
            campSceneName,
            saveRunBeforeLeaving,
            saveSlotAfterRunCommit,
            RunReturnReason.Retreat,
            "spawn retreat portal");
    }

    void HidePrompt()
    {
        if (promptObject != null)
            promptObject.SetActive(false);
        else if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        HidePrompt();
    }
}
