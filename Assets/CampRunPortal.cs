using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampRunPortal : MonoBehaviour
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Interaction")]
    public float interactRange = 1.35f;
    public KeyCode interactKey = KeyCode.E;
    public bool requireKeyPress = true;

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

    private Transform player;
    private bool promptOpen;

    void Start()
    {
        HidePrompt();
        HookButtons();
    }

    void Update()
    {
        FindPlayerIfMissing();

        if (player == null)
            return;

        bool closeEnough = Vector2.Distance(transform.position, player.position) <= interactRange;

        if (!closeEnough)
        {
            if (promptOpen)
                HidePrompt();
            return;
        }

        if (!requireKeyPress || Input.GetKeyDown(interactKey))
            ShowPrompt();
    }

    void HookButtons()
    {
        if (goButton != null)
        {
            goButton.onClick.RemoveAllListeners();
            goButton.onClick.AddListener(StartNextRun);
            SetButtonText(goButton, goButtonText);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HidePrompt);
            SetButtonText(cancelButton, cancelButtonText);
        }
    }

    void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
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
            Debug.Log("Camp portal ready. Assign Prompt Panel for confirmation UI.");
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
            }

            // GameState owns the roster. Do NOT save a scene BuddyRoster here.
            // The squad menu edits GameState directly, and scene rosters can be stale/empty.
            GameState.Instance.RepairRosterState();

            if (beginRunSnapshotBeforeLeaving)
                GameState.Instance.BeginRunSnapshot();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(runSceneName);
    }

    void FindPlayerIfMissing()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
