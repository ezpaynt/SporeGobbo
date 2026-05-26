using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampRunPortal : MonoBehaviour, ICampInteractable
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Interaction")]
    public string interactPrompt = "Enter tunnel";

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptText;
    public Button goButton;
    public Button cancelButton;
    public string promptMessage = "To the next cave?";
    public string goButtonText = "Go";
    public string cancelButtonText = "Not yet";

    [Header("Save")]
    public bool savePlayerBeforeLeaving = true;
    public bool beginRunSnapshotBeforeLeaving = true;

    private bool promptOpen = false;

    void Start()
    {
        HidePrompt();
        HookButtons();
    }

    public string GetInteractPrompt()
    {
        return promptOpen ? "Close tunnel prompt" : interactPrompt;
    }

    public void Interact(GobboController player)
    {
        if (promptOpen)
            HidePrompt();
        else
            ShowPrompt();
    }

    void HookButtons()
    {
        if (goButton != null)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(StartNextRun);

            TMP_Text text = goButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = goButtonText;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HidePrompt);

            TMP_Text text = cancelButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = cancelButtonText;
        }
    }

    void ShowPrompt()
    {
        promptOpen = true;

        if (promptText != null)
            promptText.text = promptMessage;

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
        }
        else
        {
            Debug.Log("Camp portal ready. Assign Prompt Panel for confirmation UI, or wire StartNextRun directly to a button.");
        }
    }

    void HidePrompt()
    {
        promptOpen = false;

        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    public void StartNextRun()
    {
        if (GameState.Instance != null)
        {
            if (savePlayerBeforeLeaving)
            {
                GobboController playerController = Object.FindAnyObjectByType<GobboController>();
                if (playerController != null)
                    GameState.Instance.SavePlayer(playerController);

                BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>(FindObjectsInactive.Include);
                if (roster != null)
                    GameState.Instance.SaveRoster(roster);
            }

            if (beginRunSnapshotBeforeLeaving)
                GameState.Instance.BeginRunSnapshot();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
