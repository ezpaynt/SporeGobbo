using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Interactable camp location for reading saved death history.
/// This should be disabled/hidden until CampDeathHistoryStore has at least one record.
/// </summary>
public class CampOldBonesWall : MonoBehaviour
{
    [Header("Visibility")]
    public GameObject wallVisualRoot;
    public bool hideUntilFirstDeath = true;

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text deadBuddiesText;
    public TMP_Text deadLeadersText;
    public string emptyBuddyText = "No buddies from this run are on the wall.";
    public string emptyLeaderText = "No fallen leaders yet.";

    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 1.75f;
    public Transform playerOverride;

    bool playerNearby;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        RefreshVisibility();
    }

    void Update()
    {
        RefreshVisibility();
        Transform player = playerOverride != null ? playerOverride : FindPlayer();
        playerNearby = player != null && Vector2.Distance(transform.position, player.position) <= interactRange;
        if (playerNearby && Input.GetKeyDown(interactKey)) TogglePanel();
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) panel.SetActive(false);
    }

    public void TogglePanel()
    {
        if (panel == null) return;
        bool open = !panel.activeSelf;
        panel.SetActive(open);
        if (open) RefreshPanel();
    }

    public void OpenPanel()
    {
        if (panel == null) return;
        panel.SetActive(true);
        RefreshPanel();
    }

    public void ClosePanel()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void RefreshVisibility()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.Instance;
        bool visible = !hideUntilFirstDeath || (store != null && store.HasAnyDeaths());

        if (wallVisualRoot != null)
            wallVisualRoot.SetActive(visible);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = visible;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = visible;
    }

    public void RefreshPanel()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        if (titleText != null) titleText.text = "Old Bones Wall";

        List<DeadBuddyRecord> buddiesThisRun = new List<DeadBuddyRecord>();
        List<DeadBuddyRecord> leaders = new List<DeadBuddyRecord>();
        if (store != null && store.deadBuddyHistory != null)
        {
            int currentRun = GameState.Instance != null ? GameState.Instance.currentRunNumber : 1;
            foreach (DeadBuddyRecord record in store.deadBuddyHistory)
            {
                if (record == null) continue;
                if (record.wasLeader) leaders.Add(record);
                else if (record.runNumber == currentRun) buddiesThisRun.Add(record);
            }
        }

        if (deadBuddiesText != null) deadBuddiesText.text = BuildList("Dead Buddies This Run", buddiesThisRun, emptyBuddyText);
        if (deadLeadersText != null) deadLeadersText.text = BuildList("Fallen Player Gobbos", leaders, emptyLeaderText);
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
        }
        return sb.ToString();
    }

    Transform FindPlayer()
    {
        GobboController player = FindAnyObjectByType<GobboController>();
        return player != null ? player.transform : null;
    }
}
