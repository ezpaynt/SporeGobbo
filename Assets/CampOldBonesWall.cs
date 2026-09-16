using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SporeGobbo.CampLifecycle;

/// <summary>
/// Interactable camp location for reading saved death history.
/// Keep this GameObject active in the scene. This script hides/shows only the art, collider, and optional marker sprite.
/// </summary>
public class CampOldBonesWall : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    [Header("Visibility")]
    public GameObject wallVisualRoot;
    public bool hideUntilFirstDeath = true;
    public bool hideThisMarkerSpriteUntilVisible = true;

    [Header("Camp Interaction")]
    public string interactPrompt = "Read Old Bones";
    [Min(0.1f)] public float interactionRange = 1.4f;
    public int interactionPriority = 5;
    public HandcraftedCampTerrain campTerrain;
    public string terrainFootprintId = CampEarlyMapCatalog.BonesFootprintId;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text deadBuddiesText;
    public TMP_Text deadLeadersText;
    public Button continueButton;
    public string emptyBuddyText = "No fallen buddies are on the wall yet.";
    public string emptyLeaderText = "No fallen leaders yet.";

    public Transform playerOverride;
    bool lastAvailable;

    void Awake()
    {
        HookButtons();
    }

    void Start()
    {
        HookButtons();
        if (panel != null) panel.SetActive(false);
        RefreshVisibility();
    }

    void Update()
    {
        bool available = IsAvailable();
        if (available != lastAvailable) ApplyAvailability(available);
    }

    void OnEnable()
    {
        HookButtons();
        RefreshVisibility();
    }

    public string GetInteractPrompt()
    {
        return IsAvailable() ? interactPrompt : "";
    }

    public bool CanInteract(GobboController player) => player != null && IsAvailable();
    public Vector2 GetInteractionPoint() => transform.position;
    public int InteractionPriority => interactionPriority;
    public float InteractionRange => interactionRange;

    public void Interact(GobboController player)
    {
        if (!CanInteract(player)) return;
        if (player != null) playerOverride = player.transform;
        OpenPanel();
    }

    void HookButtons()
    {
        if (continueButton == null && panel != null)
            continueButton = panel.GetComponentInChildren<Button>(true);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(ClosePanel);
        }
    }

    public void OpenPanel()
    {
        if (panel == null) return;
        HookButtons();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        RefreshPanel();
        CampMenuModal.Open(playerOverride != null ? playerOverride.GetComponent<GobboController>() : null,
            this, ClosePanel, continueButton, panel);
    }

    public void ClosePanel()
    {
        if (panel != null) panel.SetActive(false);
        CampMenuModal.Close(this);
    }

    bool IsDeathPresentationEstablished()
    {
        if (!hideUntilFirstDeath) return true;
        return GameState.Instance != null && GameState.Instance.campTerrainState != null &&
            GameState.Instance.campTerrainState.memorialEstablished;
    }

    public bool IsPhysicallyAccessible()
    {
        if (campTerrain == null) campTerrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>();
        return campTerrain != null && campTerrain.HasOpenAccessToFootprint(terrainFootprintId);
    }

    bool IsAvailable() => IsDeathPresentationEstablished() && IsPhysicallyAccessible();

    public void RefreshVisibility()
    {
        ApplyAvailability(IsAvailable());
    }

    void ApplyAvailability(bool visible)
    {
        lastAvailable = visible;

        if (wallVisualRoot != null)
            wallVisualRoot.SetActive(visible);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = visible;

        if (hideThisMarkerSpriteUntilVisible)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.enabled = visible;
        }
    }

    public void RefreshPanel()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        if (titleText != null) titleText.text = "Old Bones";

        List<DeadBuddyRecord> buddies = new List<DeadBuddyRecord>();
        List<DeadBuddyRecord> leaders = new List<DeadBuddyRecord>();

        if (store != null && store.deadBuddyHistory != null)
        {
            foreach (DeadBuddyRecord record in store.deadBuddyHistory)
            {
                if (record == null) continue;
                if (record.wasLeader) leaders.Add(record);
                else buddies.Add(record);
            }
        }

        if (deadBuddiesText != null) deadBuddiesText.text = BuildList("Fallen Buddies", buddies, emptyBuddyText);
        if (deadLeadersText != null) deadLeadersText.text = BuildList("Fallen Leaders", leaders, emptyLeaderText);
    }

    string BuildList(string header, List<DeadBuddyRecord> records, string empty)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine();

        if (records == null || records.Count == 0)
        {
            sb.AppendLine(empty);
            return sb.ToString();
        }

        foreach (DeadBuddyRecord record in records)
        {
            if (record == null) continue;
            sb.AppendLine("• " + record.GetDisplayLine());
            sb.AppendLine();
        }

        return sb.ToString();
    }

}
