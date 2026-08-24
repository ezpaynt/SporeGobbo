using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour, ISporePauseScreen
{
    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";
    public string campSceneName = "CampScene";

    [Header("UI")]
    public GameObject pausePanel;
    public TMP_Text titleText;
    public Button resumeButton;
    public Button quitToMenuButton;
    public Button quitToDesktopButton;

    [Header("Story")]
    public JournalContentLibrary journalContentLibrary;

    private bool paused;
    private bool isRunMenu;
    private bool isIntroMenu;
    private PauseMenuRunView runView;

    void Start()
    {
        isIntroMenu = SampleSceneModeController.IsIntroMode;
        isRunMenu = SceneManager.GetActiveScene().name != campSceneName;
        HookButtons();
        BuildRunMenuIfNeeded();
        paused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void OnEnable() => HookButtons();

    public bool IsPauseOpen => paused;
    public bool HasPauseSubpage => runView != null && runView.IsSubPageOpen;
    public Selectable PauseDefaultSelectable => resumeButton;
    public void OpenPause() => SetPaused(true);
    public void ClosePause() => SetPaused(false);
    public void BackPauseSubpage() { if (runView != null) runView.ShowMainPage(); }

    void HookButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(Resume);
            SetButtonText(resumeButton, "Resume");
        }
        if (quitToMenuButton != null)
        {
            quitToMenuButton.onClick.RemoveAllListeners();
            quitToMenuButton.onClick.AddListener(RequestQuitToMainMenu);
            SetButtonText(quitToMenuButton, "Quit To Menu");
        }
        if (quitToDesktopButton != null)
        {
            quitToDesktopButton.onClick.RemoveAllListeners();
            quitToDesktopButton.onClick.AddListener(RequestQuitToDesktop);
            SetButtonText(quitToDesktopButton, "Quit To Desktop");
        }
        if (titleText != null) titleText.text = "Paused";
    }

    void SetButtonText(Button button, string text)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = text;
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void SetPaused(bool value)
    {
        if (value == paused) return;
        if (value) SporePauseService.Acquire(this);
        else SporePauseService.Release(this);
        paused = value;
        if (pausePanel != null)
        {
            pausePanel.SetActive(paused);
            if (paused)
            {
                pausePanel.transform.SetAsLastSibling();
                if (runView != null)
                {
                    runView.ShowMainPage();
                    runView.Refresh(
                        PauseMenuStatusSnapshotBuilder.Build(),
                        JournalSnapshotBuilder.Build(journalContentLibrary, GameState.Instance));
                }
            }
        }
    }

    public void QuitToMainMenu()
    {
        SaveIfInCamp("quit to menu");
        SporePauseService.ResetAll();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitToDesktop()
    {
        SaveIfInCamp("quit to desktop");
        SporePauseService.ResetAll();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SaveIfInCamp(string reason)
    {
        if (SceneManager.GetActiveScene().name != campSceneName)
        {
            Debug.Log("[PauseMenuController] Not saving " + reason + " because active scene is a run. Mid-run progress rolls back.");
            return;
        }
        if (GameState.Instance == null)
        {
            Debug.Log("[PauseMenuController] No GameState. Nothing to save for " + reason + ".");
            return;
        }
        SporeSaveManager.SaveCurrentSlotFromGameState();
        Debug.Log("[PauseMenuController] Saved camp before " + reason + ".");
    }
    void BuildRunMenuIfNeeded()
    {
        if (pausePanel == null) return;
        runView = pausePanel.GetComponent<PauseMenuRunView>();
        if (runView == null) runView = pausePanel.AddComponent<PauseMenuRunView>();
        runView.Build(pausePanel, titleText, resumeButton, quitToMenuButton, quitToDesktopButton,
            Resume, OpenOptions, RequestQuitToMainMenu, RequestQuitToDesktop, isIntroMenu || !isRunMenu);
    }

    public void OpenOptions()
    {
        SporeControlsScreen.Open(() =>
        {
            if (runView != null) runView.ReturnFromControls();
        });
    }

    public void RequestQuitToMainMenu()
    {
        if (runView != null)
            runView.ShowConfirmation("Exit this run and return to the main menu? Mid-run progress will be lost.", "Exit To Menu", QuitToMainMenu);
        else
            QuitToMainMenu();
    }

    public void RequestQuitToDesktop()
    {
        if (runView != null)
            runView.ShowConfirmation("Exit the game? Mid-run progress will be lost.", "Exit To Desktop", QuitToDesktop);
        else
            QuitToDesktop();
    }
}
