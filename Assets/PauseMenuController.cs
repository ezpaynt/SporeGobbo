using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
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

    [Header("Input")]
    public KeyCode pauseKey = KeyCode.Escape;

    private bool paused;
    private bool isRunMenu;
    private PauseMenuRunView runView;

    void Start()
    {
        isRunMenu = SceneManager.GetActiveScene().name != campSceneName;
        HookButtons();
        BuildRunMenuIfNeeded();
        SetPaused(false);
    }

    void OnEnable() => HookButtons();

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            // If a UI button stayed selected after clicking Resume, do not let that block future pause input.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (paused && runView != null && runView.IsSubPageOpen)
                runView.ShowMainPage();
            else
                SetPaused(!paused);
        }
    }

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
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetPaused(bool value)
    {
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
                    runView.Refresh(PauseMenuStatusSnapshotBuilder.Build());
                }
            }
        }
        Time.timeScale = paused ? 0f : 1f;

        if (!paused && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void QuitToMainMenu()
    {
        SaveIfInCamp("quit to menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitToDesktop()
    {
        SaveIfInCamp("quit to desktop");
        Time.timeScale = 1f;
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
        if (!isRunMenu || pausePanel == null) return;
        runView = pausePanel.GetComponent<PauseMenuRunView>();
        if (runView == null) runView = pausePanel.AddComponent<PauseMenuRunView>();
        runView.Build(pausePanel, titleText, resumeButton, quitToMenuButton, quitToDesktopButton,
            Resume, OpenOptions, RequestQuitToMainMenu, RequestQuitToDesktop);
    }

    public void OpenOptions()
    {
        if (runView != null) runView.ShowOptions();
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
