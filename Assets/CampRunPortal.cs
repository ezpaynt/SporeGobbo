using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampRunPortal : MonoBehaviour, ICampInteractable, IWorldInteractionMetadata
{
    [Header("Run Scene")]
    public string runSceneName = "SampleScene";

    [Header("Development Only")]
    [Tooltip("Optional. Assign 'Persistent Test Cave' to enter that profile from Camp. Null preserves the normal production run profile.")]
    public RunProfile developmentRunProfileOverride;

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptText;
    public Button goButton;
    public Button cancelButton;
    public string promptMessage = "To the next cave?";
    public string goButtonText = "Go";
    public string cancelButtonText = "Not yet";
    [Min(0.1f)] public float interactionRange = 1.2f;
    public int interactionPriority = 10;

    [Header("Save")]
    public bool saveCampBeforeLeaving = true;
    public bool savePlayerBeforeLeaving = true;
    public bool beginRunSnapshotBeforeLeaving = true;

    private GobboController currentPlayer;
    private bool promptOpen;
    private bool departureRequested;
    public string LastDepartureFailure { get; private set; }

    void Awake()
    {
        HookButtons();
        HidePrompt();
    }

    void OnDestroy() { SporePauseService.Release(gameObject); }

    void OnEnable()
    {
        HookButtons();
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public bool CanInteract(GobboController player) => player != null && !promptOpen && !departureRequested;
    public int InteractionPriority => interactionPriority;
    public float InteractionRange => interactionRange;
    public Vector2 GetInteractionPoint()
    {
        Collider2D col = GetComponent<Collider2D>();
        return col != null ? new Vector2(col.bounds.center.x, col.bounds.min.y) : transform.position;
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
            promptPanel.SetActive(true);
            promptPanel.transform.SetAsLastSibling();
            CampMenuModal.Open(currentPlayer, this, HidePrompt, cancelButton, promptPanel);
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
        if (departureRequested) return;
        GameState state = GameState.Instance;
        var routines = Object.FindAnyObjectByType<CampStartRoutineManager>();
        var terrain = CampSpatialTerrain.Active as ICampDepartureState;
        bool snapshotStarted = false;
        try
        {
            if (state == null || state.HasActiveRunSnapshot)
                throw new System.InvalidOperationException("Departure requires a ready Camp campaign without an active run.");
            if (!Application.CanStreamedLevelBeLoaded(GameSceneCatalog.Run))
                throw new System.InvalidOperationException("Run destination is unavailable.");
            RunReturnService.RequireSelectedCampaign();
            departureRequested = true;
            LastDepartureFailure = null;
            // Scene unload is deferred. Quiesce coroutines and terrain before capturing either snapshot.
            terrain?.BeginDeparture();
            routines?.SuspendForDeparture();
            SporePauseService.Acquire(gameObject);
            if (savePlayerBeforeLeaving && currentPlayer != null) state.SavePlayer(currentPlayer);
            CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
            if (pref != null) pref.ValidateAgainstRoster();
            if (saveCampBeforeLeaving) SporeSaveManager.SaveCurrentSlotFromGameState();
            string successor = pref != null ? pref.GetMarkedSuccessorId() : state.markedSuccessorId;
            PlayerDeathRunStore.GetOrCreate().LockSuccessorForRun(successor);
            if (beginRunSnapshotBeforeLeaving)
            {
                snapshotStarted = state.TryBeginRunSnapshot();
                if (!snapshotStarted) throw new System.InvalidOperationException("Run snapshot already exists.");
            }
            Debug.Log("[CampRunPortal] Starting run. Roster: " + CountRoster() + ", active: " + CountActive());
            HidePrompt();
            PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
            SampleSceneModeContext.SetPending(SampleSceneMode.NormalRun, developmentRunProfileOverride);
            // Keep the departure pause until the existing single-scene readiness reset.
            SceneManager.LoadScene(GameSceneCatalog.Run);
        }
        catch (System.Exception exception)
        {
            if (snapshotStarted) state.EndRunSnapshot();
            if (departureRequested) PlayerDeathRunStore.GetOrCreate().ClearPendingDeath();
            PlayerDeathWatcher.ClearSceneChangeSuppression();
            terrain?.CancelDeparture();
            SporePauseService.Release(gameObject);
            departureRequested = false;
            routines?.ResumeAfterFailedDeparture();
            LastDepartureFailure = exception.Message;
            ShowPrompt();
            if (promptText != null) promptText.text = "Could not depart: " + exception.Message + "\nYour Camp remains available. Retry or cancel.";
            Debug.LogWarning("[CampRunPortal] Departure held: " + exception.Message);
        }
    }

    int CountRoster() => GameState.Instance != null && GameState.Instance.ownedGobbos != null ? GameState.Instance.ownedGobbos.Count : 0;
    int CountActive() => GameState.Instance != null && GameState.Instance.activeSquadIds != null ? GameState.Instance.activeSquadIds.Count : 0;
}
