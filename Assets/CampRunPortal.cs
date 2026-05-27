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
    public bool saveCampBeforeLeaving = true;
    public bool beginRunSnapshotBeforeLeaving = true;

    private Transform player;
    private bool promptOpen = false;

    void Start()
    {
        HidePrompt();
        HookButtons();
    }

    void OnEnable() => HookButtons();

    void Update()
    {
        FindPlayerIfMissing();
        if (player == null) return;

        bool closeEnough = Vector2.Distance(transform.position, player.position) <= interactRange;
        if (!closeEnough)
        {
            if (promptOpen) HidePrompt();
            return;
        }

        if (!requireKeyPress || Input.GetKeyDown(interactKey)) ShowPrompt();
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
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = text;
    }

    void ShowPrompt()
    {
        promptOpen = true;
        if (promptText != null) promptText.text = promptMessage;
        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
        }
    }

    void HidePrompt()
    {
        promptOpen = false;
        if (promptPanel != null) promptPanel.SetActive(false);
    }

    public void StartNextRun()
    {
        string markedSuccessorId = "";

        if (GameState.Instance != null)
        {
            if (savePlayerBeforeLeaving)
            {
                GobboController playerController = FindAnyObjectByType<GobboController>();
                if (playerController != null) GameState.Instance.SavePlayer(playerController);
            }

            CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.GetOrCreate();
            if (pref != null)
            {
                pref.ValidateAgainstRoster();
                markedSuccessorId = pref.GetMarkedSuccessorId();
            }

            GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
            bridge.SetMarkedSuccessor(markedSuccessorId, false);

            // This is the safe camp state. If the player quits during the run,
            // load/continue returns to this camp save.
            if (saveCampBeforeLeaving) SporeSaveManager.SaveCurrentGameToCurrentSlot();

            PlayerDeathRunStore.GetOrCreate().LockSuccessorForRun(markedSuccessorId);
            if (beginRunSnapshotBeforeLeaving) GameState.Instance.BeginRunSnapshot();

            Debug.Log("[CampRunPortal] Saved camp and starting run. Roster: " + CountRoster() +
                      ", active: " + CountActive() +
                      ", marked successor: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        }

        HidePrompt();
        Time.timeScale = 1f;
        PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
        SceneManager.LoadScene(runSceneName);
    }

    int CountRoster() => GameState.Instance != null && GameState.Instance.ownedBuddies != null ? GameState.Instance.ownedBuddies.Count : 0;
    int CountActive() => GameState.Instance != null && GameState.Instance.activeSquadIds != null ? GameState.Instance.activeSquadIds.Count : 0;

    void FindPlayerIfMissing()
    {
        if (player != null) return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
