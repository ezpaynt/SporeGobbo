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

    void Start()
    {
        HookButtons();
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
            quitToMenuButton.onClick.AddListener(QuitToMainMenu);
            SetButtonText(quitToMenuButton, "Quit To Menu");
        }
        if (quitToDesktopButton != null)
        {
            quitToDesktopButton.onClick.RemoveAllListeners();
            quitToDesktopButton.onClick.AddListener(QuitToDesktop);
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
            if (paused) pausePanel.transform.SetAsLastSibling();
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
}
