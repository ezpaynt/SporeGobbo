using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampRunPortal : MonoBehaviour, ICampInteractable
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptText;
    public Button goButton;
    public Button cancelButton;
    public string promptMessage = "To the next cave?";
    public string goButtonText = "Go";
    public string cancelButtonText = "Not yet";

    [Header("Save")]
    public bool saveCampBeforeLeaving = true;
    public bool savePlayerBeforeLeaving = true;
    public bool beginRunSnapshotBeforeLeaving = true;

    private GobboController currentPlayer;
    private bool promptOpen;

    void Awake()
    {
        HookButtons();
        HidePrompt();
    }

    void OnEnable()
    {
        HookButtons();
    }

    void Update()
    {
        if (promptOpen && Input.GetKeyDown(KeyCode.Escape))
            HidePrompt();
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public void Interact(GobboController playerController)
    {
        currentPlayer = playerController;
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
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = text;
    }

    void ShowPrompt()
    {
        promptOpen = true;

        if (promptText != null)
            promptText.text = promptMessage;

        if (promptPanel != null)
        {
            CampMenuModal.Open(currentPlayer, this, HidePrompt);
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogWarning("CampRunPortal has no Prompt Panel assigned.");
        }
    }

    public void HidePrompt()
    {
        promptOpen = false;

        if (promptPanel != null)
            promptPanel.SetActive(false);

        CampMenuModal.Close(this);
    }

    public void StartNextRun()
    {
        GameState state = GameState.Instance;
        if (state != null)
        {
            if (savePlayerBeforeLeaving && currentPlayer != null)
                state.SavePlayer(currentPlayer);

            CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
            if (pref != null) pref.ValidateAgainstRoster();

            if (saveCampBeforeLeaving)
                SporeSaveManager.SaveCurrentSlotFromGameState();

            string markedSuccessorId = pref != null ? pref.GetMarkedSuccessorId() : state.markedSuccessorId;
            PlayerDeathRunStore.GetOrCreate().LockSuccessorForRun(markedSuccessorId);

            if (beginRunSnapshotBeforeLeaving)
                state.BeginRunSnapshot();

            Debug.Log("[CampRunPortal] Starting run. Roster: " + CountRoster() +
                      ", active: " + CountActive() +
                      ", marked successor: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        }

        HidePrompt();
        Time.timeScale = 1f;
        PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
        SceneManager.LoadScene(runSceneName);
    }

    int CountRoster() => GameState.Instance != null && GameState.Instance.ownedGobbos != null ? GameState.Instance.ownedGobbos.Count : 0;
    int CountActive() => GameState.Instance != null && GameState.Instance.activeSquadIds != null ? GameState.Instance.activeSquadIds.Count : 0;
}
