using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactable camp location for reading saved death history.
/// Keep this GameObject active in the scene. This script hides/shows only the art, collider, and optional marker sprite.
/// </summary>
public class CampOldBonesWall : MonoBehaviour, ICampInteractable
{
    [Header("Visibility")]
    public GameObject wallVisualRoot;
    public bool hideUntilFirstDeath = true;
    public bool hideThisMarkerSpriteUntilVisible = true;

    [Header("Camp Interaction")]
    public string interactPrompt = "Read Old Bones";
    [Tooltip("ON = CampInteractionDetector owns the E prompt. OFF = this script uses its old distance/E check.")]
    public bool useSharedCampInteraction = true;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text deadBuddiesText;
    public TMP_Text deadLeadersText;
    public Button continueButton;
    public string emptyBuddyText = "No fallen buddies are on the wall yet.";
    public string emptyLeaderText = "No fallen leaders yet.";

    [Header("Legacy Input Fallback")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 1.75f;
    public Transform playerOverride;

    bool playerNearby;

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

    void OnEnable()
    {
        HookButtons();
        RefreshVisibility();
    }

    void Update()
    {
        RefreshVisibility();

        if (!useSharedCampInteraction)
        {
            Transform player = playerOverride != null ? playerOverride : FindPlayer();
            playerNearby = player != null && Vector2.Distance(transform.position, player.position) <= interactRange;
            if (playerNearby && Input.GetKeyDown(interactKey)) TogglePanel();
        }

        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            ClosePanel();
    }

    public string GetInteractPrompt()
    {
        return IsVisible() ? interactPrompt : "";
    }

    public void Interact(GobboController player)
    {
        if (!IsVisible()) return;
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

    public void TogglePanel()
    {
        if (panel == null) return;
        if (panel.activeSelf) ClosePanel();
        else OpenPanel();
    }

    public void OpenPanel()
    {
        if (panel == null) return;
        HookButtons();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        RefreshPanel();
    }

    public void ClosePanel()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }

    bool IsVisible()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.Instance;
        return !hideUntilFirstDeath || (store != null && store.HasAnyDeaths());
    }

    public void RefreshVisibility()
    {
        bool visible = IsVisible();

        if (wallVisualRoot != null)
            wallVisualRoot.SetActive(visible);

        // Keep the interaction collider alive even while the wall is hidden.
        // CampInteractionDetector needs this spot to stay detectable; GetInteractPrompt()
        // and Interact() already prevent use before the wall is unlocked.

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

    Transform FindPlayer()
    {
        GobboController player = FindAnyObjectByType<GobboController>();
        return player != null ? player.transform : null;
    }
}
