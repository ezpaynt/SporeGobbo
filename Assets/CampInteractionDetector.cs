using TMPro;
using UnityEngine;

/// <summary>
/// Scene-level camp interaction system.
/// Put this on a scene object like CampInteractionSystem.
/// CampPlayableSpawner should call SetPlayer when it spawns the camp player.
/// </summary>
public class CampInteractionDetector : MonoBehaviour
{
    [Header("Player")]
    public Transform playerTransform;

    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRadius = 1.15f;
    public LayerMask interactableLayers = ~0;

    [Header("Hold Interaction")]
    public bool allowHoldInteractions = true;
    public float holdSeconds = 0.65f;

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptText;
    public string pressPrefix = "E - ";
    public string holdPrefix = "Hold E - ";

    [Header("Input Safety")]
    public float interactionCooldown = 0.15f;

    [Header("Debug")]
    public bool drawDebugRadius = true;
    public bool logInteractions = true;
    public bool logMissingReferences = true;

    private ICampInteractable currentInteractable;
    private ICampHoldInteractable currentHoldInteractable;
    private GameObject currentObject;
    private GobboController player;

    private float holdTimer;
    private bool holdTriggered;
    private float cooldownTimer;

    void Awake()
    {
        if (promptPanel == null && logMissingReferences)
            Debug.LogWarning("CampInteractionDetector missing Prompt Panel reference.");

        if (promptText == null && logMissingReferences)
            Debug.LogWarning("CampInteractionDetector missing Prompt Text reference.");

        HidePrompt();
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (playerTransform != null && player == null)
            player = playerTransform.GetComponent<GobboController>();

        if (CampMenuModal.IsOpen)
        {
            ClearCurrent();
            HidePrompt();

            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Escape))
                CampMenuModal.CloseCurrent();

            return;
        }

        if (playerTransform == null)
        {
            ClearCurrent();
            HidePrompt();
            return;
        }

        FindBestInteractable();
        UpdatePrompt();
        HandleInput();
    }

    public void SetPlayer(Transform player)
    {
        playerTransform = player;
        this.player = player != null ? player.GetComponent<GobboController>() : null;
    }

    void FindBestInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerTransform.position, interactRadius, interactableLayers);

        float bestDistance = float.MaxValue;
        ICampInteractable bestInteractable = null;
        ICampHoldInteractable bestHoldInteractable = null;
        GameObject bestObject = null;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            ICampInteractable interactable = hit.GetComponentInParent<ICampInteractable>();
            ICampHoldInteractable holdInteractable = hit.GetComponentInParent<ICampHoldInteractable>();

            if (interactable == null && holdInteractable == null)
                continue;

            string prompt = interactable != null ? interactable.GetInteractPrompt() : "";
            string holdPrompt = holdInteractable != null ? holdInteractable.GetHoldPrompt() : "";

            if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(holdPrompt))
                continue;

            float distance = Vector2.Distance(playerTransform.position, hit.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestInteractable = interactable;
                bestHoldInteractable = holdInteractable;
                bestObject = hit.gameObject;
            }
        }

        currentInteractable = bestInteractable;
        currentHoldInteractable = bestHoldInteractable;
        currentObject = bestObject;
    }

    void UpdatePrompt()
    {
        string prompt = currentInteractable != null ? currentInteractable.GetInteractPrompt() : "";
        string holdPrompt = currentHoldInteractable != null ? currentHoldInteractable.GetHoldPrompt() : "";

        if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(holdPrompt))
        {
            HidePrompt();
            return;
        }

        if (promptText != null)
        {
            if (!string.IsNullOrWhiteSpace(prompt) && !string.IsNullOrWhiteSpace(holdPrompt))
                promptText.text = pressPrefix + prompt + "\n" + holdPrefix + holdPrompt;
            else if (!string.IsNullOrWhiteSpace(prompt))
                promptText.text = pressPrefix + prompt;
            else
                promptText.text = holdPrefix + holdPrompt;
        }

        if (promptPanel != null)
            promptPanel.SetActive(true);
    }

    void HandleInput()
    {
        if (cooldownTimer > 0f)
            return;

        if (currentInteractable != null && Input.GetKeyDown(interactKey))
        {
            LogInteraction("Pressed", currentObject);
            currentInteractable.Interact(player);
            cooldownTimer = interactionCooldown;
            holdTimer = 0f;
            holdTriggered = false;
            return;
        }

        if (!allowHoldInteractions || currentHoldInteractable == null)
        {
            holdTimer = 0f;
            holdTriggered = false;
            return;
        }

        if (Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            if (!holdTriggered && holdTimer >= holdSeconds)
            {
                LogInteraction("Held", currentObject);
                currentHoldInteractable.HoldInteract(player);
                holdTriggered = true;
                cooldownTimer = interactionCooldown;
            }
        }
        else
        {
            holdTimer = 0f;
            holdTriggered = false;
        }
    }

    void ClearCurrent()
    {
        currentInteractable = null;
        currentHoldInteractable = null;
        currentObject = null;
        holdTimer = 0f;
        holdTriggered = false;
    }

    void HidePrompt()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    void LogInteraction(string action, GameObject target)
    {
        if (!logInteractions) return;
        Debug.Log("[CampInteractionDetector] " + action + " interaction with " + (target != null ? target.name : "unknown"));
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebugRadius || playerTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerTransform.position, interactRadius);
    }
}
