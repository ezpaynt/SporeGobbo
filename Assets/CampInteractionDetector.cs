using System.Collections.Generic;
using SporeGobbo.Input;
using TMPro;
using UnityEngine;

/// <summary>Single authority for world interaction selection, prompts, and execution.</summary>
public class CampInteractionDetector : MonoBehaviour
{
    private const string RuntimePromptResourcePath = "UI/CampInteractionPrompt";

    [Header("Player")]
    public Transform playerTransform;

    [Header("Discovery")]
    public float interactRadius = 1.2f;
    public LayerMask interactableLayers = ~0;
    [Range(0f, 0.5f)] public float facingTieDistance = 0.2f;
    [Range(0f, 0.5f)] public float switchDistanceAdvantage = 0.1f;

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptText;

    [Header("Debug")]
    public bool drawDebugRadius = true;
    public bool logInteractions = true;

    private readonly List<Candidate> candidates = new();
    private readonly List<InteractionCandidateData> rankingData = new();
    private readonly HashSet<int> seenOwners = new();
    private Candidate current;
    private GobboController player;
    private SporeInputReader inputReader;
    private SporeUiCoordinator uiCoordinator;
    private string currentPrompt;
    private bool promptResolutionAttempted;

    private sealed class Candidate
    {
        public MonoBehaviour Owner;
        public ICampInteractable Interactable;
        public IWorldInteractionMetadata Metadata;
        public int Priority;
        public float Distance;
    }

    void Awake()
    {
        inputReader = SporeInputReader.Instance;
        EnsurePromptPresentation();
        HidePrompt();
    }

    void OnEnable()
    {
        if (inputReader == null) inputReader = SporeInputReader.Instance;
        uiCoordinator = SporeUiCoordinator.Instance;
        uiCoordinator.PresentationChanged += HandlePresentationChanged;
    }

    void OnDisable()
    {
        if (uiCoordinator != null)
            uiCoordinator.PresentationChanged -= HandlePresentationChanged;
        uiCoordinator = null;
    }

    void HandlePresentationChanged() => UpdatePrompt();

    void Update()
    {
        if (inputReader == null) inputReader = SporeInputReader.Instance;
        if (playerTransform == null) FindPlayer();
        else if (player == null) player = playerTransform.GetComponent<GobboController>();

        if (CampMenuModal.IsOpen)
        {
            ClearCurrent();
            HidePrompt();
            return;
        }

        if (playerTransform == null || player == null || inputReader == null || Time.timeScale <= 0f ||
            inputReader.Context != SporeInputContext.Gameplay)
        {
            ClearCurrent();
            HidePrompt();
            return;
        }

        SelectCurrent();
        UpdatePrompt();

        if (inputReader.Interact.StartedThisFrame && IsUsable(current))
        {
            Candidate target = current;
            if (logInteractions) Debug.Log("[WorldInteraction] Interact with " + target.Owner.name, target.Owner);
            target.Interactable.Interact(player);
        }
    }

    public void SetPlayer(Transform target)
    {
        EnsurePromptPresentation();
        playerTransform = target;
        player = target != null ? target.GetComponent<GobboController>() : null;
        ClearCurrent();
    }

    void FindPlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) SetPlayer(found.transform);
    }

    void SelectCurrent()
    {
        int currentId = IsUsable(current) ? current.Owner.GetInstanceID() : int.MinValue;
        candidates.Clear();
        rankingData.Clear();
        seenOwners.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(playerTransform.position, interactRadius, interactableLayers);
        foreach (Collider2D hit in hits) AddCandidate(hit);

        int selectedIndex = InteractionSelectionMath.Select(
            rankingData, currentId, facingTieDistance, switchDistanceAdvantage);
        current = selectedIndex >= 0 ? candidates[selectedIndex] : null;
    }

    void AddCandidate(Collider2D hit)
    {
        if (hit == null) return;

        MonoBehaviour owner = null;
        ICampInteractable interactable = null;
        foreach (MonoBehaviour behaviour in hit.GetComponentsInParent<MonoBehaviour>(true))
        {
            if (behaviour is ICampInteractable found)
            {
                owner = behaviour;
                interactable = found;
                break;
            }
        }

        if (owner == null || !owner.isActiveAndEnabled || !seenOwners.Add(owner.GetInstanceID())) return;

        IWorldInteractionMetadata metadata = owner as IWorldInteractionMetadata;
        if (metadata != null && !metadata.CanInteract(player)) return;
        if (string.IsNullOrWhiteSpace(interactable.GetInteractPrompt())) return;

        Vector2 point = metadata != null ? metadata.GetInteractionPoint() : owner.transform.position;
        float distance = Vector2.Distance(playerTransform.position, point);
        float allowedRange = metadata != null ? metadata.InteractionRange : interactRadius;
        if (distance > Mathf.Min(interactRadius, Mathf.Max(0.1f, allowedRange))) return;

        Vector2 direction = point - (Vector2)playerTransform.position;
        float alignment = direction.sqrMagnitude > 0.0001f
            ? Vector2.Dot(player.CurrentAimDirection, direction.normalized)
            : 1f;
        var candidate = new Candidate
        {
            Owner = owner,
            Interactable = interactable,
            Metadata = metadata,
            Priority = metadata?.InteractionPriority ?? 0,
            Distance = distance
        };
        candidates.Add(candidate);
        rankingData.Add(new InteractionCandidateData(owner.GetInstanceID(), candidate.Priority, distance, alignment));
    }

    bool IsUsable(Candidate candidate)
    {
        return candidate != null && candidate.Owner != null && candidate.Owner.isActiveAndEnabled &&
               candidate.Interactable != null &&
               (candidate.Metadata == null || candidate.Metadata.CanInteract(player)) &&
               !string.IsNullOrWhiteSpace(candidate.Interactable.GetInteractPrompt());
    }

    void UpdatePrompt()
    {
        if (!IsUsable(current))
        {
            HidePrompt();
            return;
        }

        currentPrompt = inputReader.GetInteractBindingDisplay() + " - " + current.Interactable.GetInteractPrompt();
        if (promptText != null) promptText.text = currentPrompt;
        if (promptPanel != null) promptPanel.SetActive(true);
    }

    void EnsurePromptPresentation()
    {
        if (promptPanel != null && promptText != null) return;

        if (promptPanel != null && promptText == null)
            promptText = promptPanel.GetComponentInChildren<TMP_Text>(true);
        if (promptPanel != null && promptText != null) return;
        if (promptResolutionAttempted) return;

        promptResolutionAttempted = true;
        Canvas canvas = FindPromptCanvas();
        GameObject prefab = Resources.Load<GameObject>(RuntimePromptResourcePath);
        if (canvas == null || prefab == null)
        {
            Debug.LogError("CampInteractionDetector could not resolve the shared interaction prompt presentation.", this);
            return;
        }

        promptPanel = Object.Instantiate(prefab, canvas.transform, false);
        promptPanel.name = "WorldInteractionPrompt";
        promptText = promptPanel.GetComponentInChildren<TMP_Text>(true);
        if (promptText == null)
        {
            Debug.LogError("Shared interaction prompt prefab has no TMP_Text child.", promptPanel);
            Object.Destroy(promptPanel);
            promptPanel = null;
        }
    }

    static Canvas FindPromptCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        foreach (Canvas candidate in canvases)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.renderMode == RenderMode.WorldSpace)
                continue;
            if (best == null || candidate.sortingOrder > best.sortingOrder)
                best = candidate;
        }
        return best;
    }

    void ClearCurrent()
    {
        current = null;
        currentPrompt = null;
    }

    void HidePrompt()
    {
        currentPrompt = null;
        if (promptPanel != null) promptPanel.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebugRadius || playerTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerTransform.position, interactRadius);
    }
}
