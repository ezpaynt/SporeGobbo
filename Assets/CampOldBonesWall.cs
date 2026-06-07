using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactable camp location for reading saved death history.
/// Visibility is controlled by CampDeathHistoryStore data.
/// </summary>
public class CampOldBonesWall : MonoBehaviour, ICampInteractable
{
    [Header("Visibility")]
    public GameObject wallVisualRoot;
    public bool hideUntilFirstDeath = true;
    public bool hideThisMarkerSpriteUntilVisible = true;

    [Header("Camp Interaction")]
    public string interactPrompt = "Read Old Bones";

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text deadBuddiesText;
    public TMP_Text deadLeadersText;
    public Button continueButton;
    public string emptyBuddyText = "No fallen buddies are on the wall yet.";
    public string emptyLeaderText = "No fallen leaders yet.";

    void Awake()
    {
        HookButtons();
        if (panel != null) panel.SetActive(false);
    }

    void Start()
    {
        RefreshVisibility();
    }

    void OnEnable()
    {
        RefreshVisibility();
    }

    void Update()
    {
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
        OpenPanel();
    }

    void HookButtons()
    {
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
        if (panel == null)
        {
            Debug.LogWarning("CampOldBonesWall missing Panel reference.");
            return;
        }

        HookButtons();
        CampMenuModal.Open(null, this, ClosePanel);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        RefreshPanel();
    }

    public void ClosePanel()
    {
        if (panel != null) panel.SetActive(false);
        CampMenuModal.Close(this);
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

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = visible;

        if (hideThisMarkerSpriteUntilVisible)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.enabled = visible;
        }
    }

    public void RefreshPanel()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.Instance;

        if (titleText != null)
            titleText.text = "Old Bones Wall";

        if (store == null)
        {
            if (deadBuddiesText != null) deadBuddiesText.text = "No CampDeathHistoryStore exists in this scene.";
            if (deadLeadersText != null) deadLeadersText.text = emptyLeaderText;
            return;
        }

        if (deadBuddiesText != null)
        {
            System.Text.StringBuilder buddyBuilder = new System.Text.StringBuilder();
            if (store.deadBuddyHistory != null)
            {
                foreach (DeadBuddyRecord record in store.deadBuddyHistory)
                {
                    if (record != null && !record.wasLeader)
                        buddyBuilder.AppendLine(record.GetDisplayLine());
                }
            }
            deadBuddiesText.text = buddyBuilder.Length > 0 ? buddyBuilder.ToString() : emptyBuddyText;
        }

        if (deadLeadersText != null)
        {
            System.Text.StringBuilder leaderBuilder = new System.Text.StringBuilder();
            if (store.deadBuddyHistory != null)
            {
                foreach (DeadBuddyRecord record in store.deadBuddyHistory)
                {
                    if (record != null && record.wasLeader)
                        leaderBuilder.AppendLine(record.GetDisplayLine());
                }
            }
            deadLeadersText.text = leaderBuilder.Length > 0 ? leaderBuilder.ToString() : emptyLeaderText;
        }
    }
}
