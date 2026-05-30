using TMPro;
using UnityEngine;

/// <summary>
/// Handles first reveal / new memorial notification when camp loads.
/// The wall UI itself lives on CampOldBonesWall.
/// </summary>
public class CampBonesMemorialManager : MonoBehaviour
{
    [Header("Wall")]
    public CampOldBonesWall bonesWall;
    public GameObject wallRoot;

    [Header("Popup")]
    public GameObject popupPanel;
    public TMP_Text popupText;
    [TextArea(2, 4)] public string firstRevealMessage = "The gobbos dragged old bones into camp.\nThe Old Bones Wall can now be read.";
    [TextArea(2, 4)] public string newMemorialMessage = "New names have been scratched onto the Old Bones Wall.";
    public float autoHideAfterSeconds = 4f;

    [Header("Debug")]
    public bool logDebugMessages = true;

    float hideAt = -1f;

    void Start()
    {
        if (bonesWall == null) bonesWall = FindAnyObjectByType<CampOldBonesWall>();
        if (popupPanel != null) popupPanel.SetActive(false);
        RefreshWallVisibility();
        TryShowMemorialPopup();
    }

    void Update()
    {
        if (hideAt > 0f && Time.unscaledTime >= hideAt)
        {
            HidePopup();
        }
    }

    public void TryShowMemorialPopup()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        if (store == null || !store.HasAnyDeaths())
        {
            RefreshWallVisibility();
            return;
        }

        RefreshWallVisibility();
        if (!store.HasUnseenMemorials()) return;

        bool firstReveal = store.deadBuddyHistory != null && store.deadBuddyHistory.Count == 1;
        ShowPopup(firstReveal ? firstRevealMessage : newMemorialMessage);
        store.MarkAllSeen();
        if (bonesWall != null) bonesWall.RefreshVisibility();
    }

    public void RefreshWallVisibility()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.Instance;
        bool visible = store != null && store.HasAnyDeaths();
        if (wallRoot != null) wallRoot.SetActive(visible);
        if (bonesWall != null) bonesWall.RefreshVisibility();
    }

    public void ShowPopup(string message)
    {
        if (popupPanel == null) return;
        popupPanel.SetActive(true);
        if (popupText != null) popupText.text = message;
        hideAt = autoHideAfterSeconds > 0f ? Time.unscaledTime + autoHideAfterSeconds : -1f;
        Log(message);
    }

    public void HidePopup()
    {
        hideAt = -1f;
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampBonesMemorialManager] " + message);
    }
}
