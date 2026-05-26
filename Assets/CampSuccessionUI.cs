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
    public string title = "The leader is dead";
    [TextArea(2, 5)] public string markedSuccessorBody = "{0} was marked to take over.";
    [TextArea(2, 5)] public string noMarkedSuccessorBody = "No gobbo was marked as successor. The camp has to choose.";
    [TextArea(2, 5)] public string gameOverMessage = "GAME OVER\n\nNo gobbos are left to remember the camp.";

    private BuddyData markedSuccessor;

    void Start()
    {
        HookButtons();
        HideAll();
        TryOpenDeathFlow();
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

    void HideAll()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void TryOpenDeathFlow()
    {
        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        if (pending == null || !pending.playerDiedThisRun)
            return;

        CampDeathHistoryStore.GetOrCreate().AddDeadLeaderFromPendingStore(pending);

        if (!HasAnyEligibleGobbo())
        {
            OpenGameOver();
            return;
        }

        OpenSuccessionPanel();
    }

    void OpenSuccessionPanel()
    {
        markedSuccessor = CampSuccessorPreferenceStore.GetOrCreate().GetMarkedSuccessor();

        if (successionPanel != null)
            successionPanel.SetActive(true);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (titleText != null)
            titleText.text = title;

        bool hasMarked = markedSuccessor != null;

        if (bodyText != null)
        {
            bodyText.text = hasMarked
                ? string.Format(markedSuccessorBody, markedSuccessor.buddyName)
                : noMarkedSuccessorBody;
        }

        if (acceptMarkedSuccessorButton != null)
        {
            acceptMarkedSuccessorButton.gameObject.SetActive(hasMarked);
            acceptMarkedSuccessorButton.interactable = hasMarked;

            TMP_Text label = acceptMarkedSuccessorButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && hasMarked)
                label.text = "Accept " + markedSuccessor.buddyName;
        }

        if (letCampChooseButton != null)
        {
            letCampChooseButton.gameObject.SetActive(true);
            letCampChooseButton.interactable = true;
        }
    }

    void OpenGameOver()
    {
        if (successionPanel != null)
            successionPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = gameOverMessage;
    }

    void AcceptMarkedSuccessor()
    {
        if (markedSuccessor == null)
        {
            OpenSuccessionPanel();
            return;
        }

        PromoteSuccessor(markedSuccessor);
    }

    void LetCampChoose()
    {
        BuddyData chosen = ChooseStrongestGobbo();
        if (chosen == null)
        {
            OpenGameOver();
            return;
        }

        PromoteSuccessor(chosen);
    }

    BuddyData ChooseStrongestGobbo()
    {
        if (GameState.Instance == null || GameState.Instance.ownedBuddies == null)
            return null;

        BuddyData best = null;
        int bestScore = int.MinValue;

        foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureRuntimeDefaults();

            int score = buddy.level * 10000 + buddy.maxHealth * 100 + buddy.damage * 50 + buddy.defense * 25 + buddy.loyalty;
            if (best == null || score > bestScore)
            {
                best = buddy;
                bestScore = score;
            }
        }

        return best;
    }

    bool HasAnyEligibleGobbo()
    {
        return GameState.Instance != null && GameState.Instance.ownedBuddies != null && GameState.Instance.ownedBuddies.Count > 0;
    }

    void PromoteSuccessor(BuddyData successor)
    {
        if (successor == null || GameState.Instance == null || GameState.Instance.gobbo == null)
            return;

        successor.EnsureId();
        successor.EnsureRuntimeDefaults();

        GobboSaveData player = GameState.Instance.gobbo;

        player.level = Mathf.Max(1, successor.level);
        player.xp = Mathf.Max(0, successor.xp);
        player.xpToNextLevel = Mathf.Max(1, successor.xpToNextLevel);
        player.gobboType = successor.buddyType;
        player.ageStage = successor.ageStage;
        player.visualSetId = string.IsNullOrWhiteSpace(successor.visualSetId) ? successor.buddyType.ToString().ToLowerInvariant() : successor.visualSetId;
        player.pendingEvolution = successor.pendingEvolution;
        player.evolutionLevelWaiting = successor.evolutionLevelWaiting;

        player.maxHealth = Mathf.Max(1, successor.maxHealth);
        player.health = player.maxHealth;
        player.attack = Mathf.Max(1, successor.damage);
        player.defense = Mathf.Max(0, successor.defense);
        player.moveSpeed = Mathf.Max(1f, successor.moveSpeed);
        player.attackCooldown = Mathf.Max(0.1f, successor.attackCooldown);

        if (successor.chosenCardIds != null)
            player.chosenCardIds = new List<string>(successor.chosenCardIds);

        string id = successor.uniqueId;
        GameState.Instance.ownedBuddies.RemoveAll(b => b == null || b.uniqueId == id);
        GameState.Instance.activeSquadIds.RemoveAll(activeId => activeId == id);

        CampSuccessorPreferenceStore.GetOrCreate().ClearSuccessor();

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        if (pending != null)
            pending.ClearPendingDeath();

        HideAll();

        CampSceneController camp = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        if (camp != null)
            camp.RevealCampVisuals();
    }

    void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
