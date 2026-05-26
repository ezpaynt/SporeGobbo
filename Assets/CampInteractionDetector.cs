using TMPro;
using UnityEngine;

/// <summary>
/// Put this on a scene object like CampInteractionSystem, NOT on the player prefab.
/// It finds the spawned camp player by tag, checks nearby camp objects,
/// shows the prompt, and sends E interactions to ICampInteractable objects.
///
/// Long-term goal: every camp object only implements ICampInteractable / ICampHoldInteractable.
/// Objects should not run their own distance checks or key checks.
/// </summary>
public class CampInteractionDetector : MonoBehaviour
{
    [Header("Player")]
    public Transform playerTransform;
    public bool findPlayerByTag = true;
    public string playerTag = "Player";

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

    [Header("Menu Safety")]
    public bool useKeyDownForNormalInteract = true;

    [Header("Debug")]
    public bool drawDebugRadius = true;

    private ICampInteractable currentInteractable;
    private ICampHoldInteractable currentHoldInteractable;
    private GameObject currentObject;
    private GobboController player;
    private float holdTimer;
    private bool holdTriggered;

    void Awake()
    {
        HidePrompt();
    }

    void Update()
    {
        RefreshPlayerReference();

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

    void RefreshPlayerReference()
    {
        if (playerTransform != null)
        {
            if (player == null)
                player = playerTransform.GetComponent<GobboController>();

            return;
        }

        if (!findPlayerByTag)
            return;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found == null)
            return;

        playerTransform = found.transform;
        player = found.GetComponent<GobboController>();
    }

    void FindBestInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerTransform.position, interactRadius, interactableLayers);

        float closestDistance = Mathf.Infinity;
        ICampInteractable bestInteractable = null;
        ICampHoldInteractable bestHoldInteractable = null;
        GameObject bestObject = null;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.gameObject.activeInHierarchy)
                continue;

            ICampInteractable interactable = FindInterface<ICampInteractable>(hit.gameObject);
            if (interactable == null)
                continue;

            float distance = Vector2.Distance(playerTransform.position, hit.ClosestPoint(playerTransform.position));
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            bestInteractable = interactable;
            bestHoldInteractable = FindInterface<ICampHoldInteractable>(hit.gameObject);
            bestObject = hit.gameObject;
        }

        if (bestObject != currentObject)
        {
            holdTimer = 0f;
            holdTriggered = false;
        }

        currentInteractable = bestInteractable;
        currentHoldInteractable = bestHoldInteractable;
        currentObject = bestObject;
    }

    void UpdatePrompt()
    {
        if (currentInteractable == null)
        {
            HidePrompt();
            return;
        }

        string prompt = currentInteractable.GetInteractPrompt();
        string holdPrompt = "";

        if (allowHoldInteractions && currentHoldInteractable != null)
            holdPrompt = currentHoldInteractable.GetHoldPrompt();

        string finalText = "";

        if (!string.IsNullOrWhiteSpace(prompt))
            finalText = pressPrefix + prompt;

        if (!string.IsNullOrWhiteSpace(holdPrompt))
        {
            if (!string.IsNullOrWhiteSpace(finalText))
                finalText += "\n";

            finalText += holdPrefix + holdPrompt;
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            HidePrompt();
            return;
        }

        if (promptText != null)
            promptText.text = finalText;

        if (promptPanel != null)
            promptPanel.SetActive(true);
    }

    void HandleInput()
    {
        if (currentInteractable == null)
        {
            holdTimer = 0f;
            holdTriggered = false;
            return;
        }

        bool keyDown = Input.GetKeyDown(interactKey);
        bool keyHeld = Input.GetKey(interactKey);
        bool keyUp = Input.GetKeyUp(interactKey);

        if (keyDown)
        {
            holdTimer = 0f;
            holdTriggered = false;

            if (useKeyDownForNormalInteract)
            {
                currentInteractable.Interact(player);
                return;
            }
        }

        if (keyHeld)
        {
            holdTimer += Time.deltaTime;

            if (allowHoldInteractions && !holdTriggered && currentHoldInteractable != null && holdTimer >= holdSeconds)
            {
                string holdPrompt = currentHoldInteractable.GetHoldPrompt();
                if (!string.IsNullOrWhiteSpace(holdPrompt))
                {
                    holdTriggered = true;
                    currentHoldInteractable.HoldInteract(player);
                }
            }
        }

        if (!useKeyDownForNormalInteract && keyUp)
        {
            bool wasHold = holdTriggered;
            holdTimer = 0f;
            holdTriggered = false;

            if (!wasHold && currentInteractable != null)
                currentInteractable.Interact(player);
        }

        if (keyUp)
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

    T FindInterface<T>(GameObject source) where T : class
    {
        if (source == null)
            return null;

        MonoBehaviour[] behaviours = source.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is T match)
                return match;
        }

        behaviours = source.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is T match)
                return match;
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebugRadius)
            return;

        Vector3 center = playerTransform != null ? playerTransform.position : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, interactRadius);
    }
}
