using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampSuccessionUI : MonoBehaviour
{
    [Header("Succession Panel")]
    public GameObject successionPanel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button acceptMarkedSuccessorButton;
    public Button letCampChooseButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public Button returnToMainMenuButton;
    public string mainMenuSceneName = "MainMenu";

    [Header("Text")]
    public string title = "The leader is bones now.";
    [TextArea(2, 5)] public string markedSuccessorBody = "{0} was marked to take over.";
    [TextArea(2, 5)] public string noMarkedSuccessorBody = "No gobbo was marked as successor. The camp has to choose.";
    [TextArea(2, 5)] public string gameOverMessage = "GAME OVER\n\nNo gobbos are left to remember the camp.";

    [Header("Debug")]
    public bool logDebugMessages = true;

    private BuddyData markedSuccessor;
    private List<BuddyData> eligibleSuccessors = new List<BuddyData>();

    void Awake()
    {
        HookButtons();
        HideAllPanels();
    }

    void OnEnable()
    {
        HookButtons();
    }

    void Start()
    {
        HookButtons();
    }

    public bool TryOpenDeathFlow()
    {
        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store == null || !store.playerDiedThisRun)
        {
            Log("No pending player death. Succession UI will stay closed.");
            HideAllPanels();
            return false;
        }

        OpenDeathFlow();
        return true;
    }

    public void OpenDeathFlow()
    {
        RefreshCandidates();

        if (eligibleSuccessors.Count <= 0)
        {
            Log("No eligible gobbos found. Opening game over panel.");
            OpenGameOverPanel();
            return;
        }

        OpenSuccessionPanel();
    }

    void HookButtons()
    {
        if (acceptMarkedSuccessorButton != null)
        {
            acceptMarkedSuccessorButton.onClick.RemoveAllListeners();
            acceptMarkedSuccessorButton.onClick.AddListener(AcceptMarkedSuccessor);
        }

        if (letCampChooseButton != null)
        {
            letCampChooseButton.onClick.RemoveAllListeners();
            letCampChooseButton.onClick.AddListener(LetCampChoose);
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveAllListeners();
            returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    void RefreshCandidates()
    {
        eligibleSuccessors.Clear();
        markedSuccessor = null;

        if (GameState.Instance != null && GameState.Instance.ownedBuddies != null)
        {
            foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
            {
                if (buddy == null)
                    continue;

                buddy.EnsureId();
                buddy.EnsureRuntimeDefaults();

                if (buddy.health <= 0)
                    buddy.health = Mathf.Max(1, buddy.maxHealth);

                eligibleSuccessors.Add(buddy);
            }
        }

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
        if (pref != null)
        {
            string markedId = pref.GetMarkedSuccessorId();
            if (!string.IsNullOrWhiteSpace(markedId))
            {
                foreach (BuddyData buddy in eligibleSuccessors)
                {
                    if (buddy != null && buddy.uniqueId == markedId)
                    {
                        markedSuccessor = buddy;
                        break;
                    }
                }
            }
        }

        Log("Opened succession panel. Marked successor: " + (markedSuccessor != null ? markedSuccessor.buddyName : "none") + ", eligible count: " + eligibleSuccessors.Count);
    }

    void OpenSuccessionPanel()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (successionPanel != null)
        {
            successionPanel.SetActive(true);
            successionPanel.transform.SetAsLastSibling();
        }

        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = markedSuccessor != null
                ? string.Format(markedSuccessorBody, markedSuccessor.buddyName)
                : noMarkedSuccessorBody;

        if (acceptMarkedSuccessorButton != null)
            acceptMarkedSuccessorButton.gameObject.SetActive(markedSuccessor != null);

        if (letCampChooseButton != null)
            letCampChooseButton.gameObject.SetActive(true);
    }

    void OpenGameOverPanel()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }

        if (gameOverText != null)
            gameOverText.text = gameOverMessage;
    }

    public void AcceptMarkedSuccessor()
    {
        Log("Accept marked successor clicked.");
        if (markedSuccessor == null)
        {
            LetCampChoose();
            return;
        }

        PromoteSuccessor(markedSuccessor);
    }

    public void LetCampChoose()
    {
        Log("Let Camp Choose clicked.");
        RefreshCandidates();

        BuddyData chosen = PickStrongestSuccessor();
        if (chosen == null)
        {
            OpenGameOverPanel();
            return;
        }

        PromoteSuccessor(chosen);
    }

    BuddyData PickStrongestSuccessor()
    {
        BuddyData best = null;
        foreach (BuddyData buddy in eligibleSuccessors)
        {
            if (buddy == null)
                continue;

            if (best == null || GetSuccessorScore(buddy) > GetSuccessorScore(best))
                best = buddy;
        }
        return best;
    }

    int GetSuccessorScore(BuddyData buddy)
    {
        if (buddy == null)
            return -999999;

        return buddy.level * 10000 + buddy.maxHealth * 100 + buddy.damage * 25 + buddy.loyalty;
    }

    void PromoteSuccessor(BuddyData successor)
    {
        if (successor == null || GameState.Instance == null)
            return;

        successor.EnsureId();
        successor.EnsureRuntimeDefaults();

        GobboSaveData gobbo = GameState.Instance.gobbo;
        if (gobbo == null)
        {
            gobbo = new GobboSaveData();
            GameState.Instance.gobbo = gobbo;
        }

        gobbo.level = Mathf.Max(1, successor.level);
        gobbo.maxHealth = Mathf.Max(1, successor.maxHealth);
        gobbo.health = gobbo.maxHealth;
        gobbo.attack = Mathf.Max(1, successor.damage);
        gobbo.defense = Mathf.Max(0, successor.defense);
        gobbo.moveSpeed = Mathf.Max(0.1f, successor.moveSpeed);

        GameState.Instance.ownedBuddies.RemoveAll(b => b == null || b.uniqueId == successor.uniqueId);
        GameState.Instance.activeSquadIds.RemoveAll(id => id == successor.uniqueId);

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
        if (pref != null && pref.IsMarked(successor.uniqueId))
            pref.ClearSuccessor();

        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store != null)
            store.ClearPendingDeath();

        Log("Promoted successor: " + successor.buddyName);

        HideAllPanels();

        CampSceneController controller = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (controller != null)
            controller.RevealCampVisuals();
    }

    public void ReturnToMainMenu()
    {
        Log("Return to main menu clicked. Scene: " + mainMenuSceneName);
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    void HideAllPanels()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void Log(string message)
    {
        if (logDebugMessages)
            Debug.Log("[CampSuccessionUI] " + message);
    }
}
