using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CampSuccessionUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Transform successorListParent;
    public Button successorButtonPrefab;
    public Button letThemChooseButton;
    public Button gameOverButton;

    [Header("Text")]
    public string title = "The leader is bones now.";
    [TextArea(2, 6)] public string body = "The camp scratches a name into the old bones. Pick who gets shoved into being leader next.";
    public string letThemChooseText = "Let Them Choose";
    public string gameOverText = "No Gobbos Left";

    [Header("Behavior")]
    public bool openAutomaticallyOnCampLoad = true;
    public bool pauseWhileOpen = true;
    public bool includeReserveIfNoRunSurvivors = true;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    void Start()
    {
        HookButtons();
        ClosePanel(false);

        if (openAutomaticallyOnCampLoad)
            TryOpenForPendingDeath();
    }

    void HookButtons()
    {
        if (letThemChooseButton != null)
        {
            letThemChooseButton.onClick.RemoveAllListeners();
            letThemChooseButton.onClick.AddListener(LetThemChoose);
            SetButtonText(letThemChooseButton, letThemChooseText);
        }

        if (gameOverButton != null)
        {
            gameOverButton.onClick.RemoveAllListeners();
            gameOverButton.onClick.AddListener(() => CampMessageUI.Show("No gobbos left. Later this can open a real Game Over screen."));
            SetButtonText(gameOverButton, gameOverText);
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

    public void TryOpenForPendingDeath()
    {
        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        if (pending == null || !pending.playerDiedThisRun)
            return;

        CampDeathHistoryStore.GetOrCreate().AddDeadLeaderFromPendingStore(pending);
        OpenPanel();
    }

    void OpenPanel()
    {
        if (panel == null)
        {
            Debug.LogWarning("CampSuccessionUI needs a Panel assigned.", this);
            return;
        }

        if (pauseWhileOpen)
            Time.timeScale = 0f;

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;

        RefreshSuccessorButtons();
    }

    void ClosePanel(bool restoreTime)
    {
        if (restoreTime)
            Time.timeScale = 1f;

        if (panel != null)
            panel.SetActive(false);
    }

    void RefreshSuccessorButtons()
    {
        ClearRows();

        List<BuddyData> candidates = GetCandidates();
        bool hasCandidates = candidates.Count > 0;

        if (letThemChooseButton != null)
            letThemChooseButton.gameObject.SetActive(hasCandidates);

        if (gameOverButton != null)
            gameOverButton.gameObject.SetActive(!hasCandidates);

        if (!hasCandidates)
        {
            if (bodyText != null)
                bodyText.text = "No one is left to take the torch. The little camp goes quiet.";
            return;
        }

        foreach (BuddyData buddy in candidates)
            AddSuccessorButton(buddy);
    }

    List<BuddyData> GetCandidates()
    {
        List<BuddyData> result = new List<BuddyData>();

        if (GameState.Instance == null || GameState.Instance.ownedBuddies == null)
            return result;

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        List<string> allowedIds = pending != null ? pending.eligibleSuccessorIds : null;

        foreach (BuddyData buddy in GameState.Instance.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            bool allowedByRun = allowedIds == null || allowedIds.Count == 0 || allowedIds.Contains(buddy.uniqueId);
            if (!allowedByRun && !includeReserveIfNoRunSurvivors)
                continue;

            if (buddy.health > 0)
                result.Add(buddy);
        }

        return result;
    }

    void AddSuccessorButton(BuddyData buddy)
    {
        if (successorListParent == null || successorButtonPrefab == null || buddy == null)
            return;

        Button button = Instantiate(successorButtonPrefab, successorListParent);
        spawnedRows.Add(button.gameObject);

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = buddy.buddyName + "  " + buddy.buddyType + " / " + buddy.ageStage +
                        "  Lv " + buddy.level + "  HP " + buddy.health + "/" + buddy.maxHealth;
        }

        string id = buddy.uniqueId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ChooseSuccessor(id));
    }

    void LetThemChoose()
    {
        List<BuddyData> candidates = GetCandidates();
        if (candidates.Count == 0)
            return;

        BuddyData best = candidates[0];
        int bestScore = GetAutoChoiceScore(best);

        for (int i = 1; i < candidates.Count; i++)
        {
            int score = GetAutoChoiceScore(candidates[i]);
            if (score > bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
        }

        ChooseSuccessor(best.uniqueId);
    }

    int GetAutoChoiceScore(BuddyData buddy)
    {
        if (buddy == null)
            return int.MinValue;

        return buddy.level * 10 + buddy.loyalty + buddy.happiness + Random.Range(0, 12);
    }

    void ChooseSuccessor(string buddyId)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(buddyId))
            return;

        BuddyData successor = GameState.Instance.FindBuddy(buddyId);
        if (successor == null)
        {
            Debug.LogWarning("Could not find successor buddy: " + buddyId);
            RefreshSuccessorButtons();
            return;
        }

        ConvertBuddyToPlayer(successor);
        RemoveSuccessorFromRoster(successor.uniqueId);

        PlayerDeathRunStore pending = PlayerDeathRunStore.Instance;
        if (pending != null)
            pending.ClearPendingDeath();

        CampMessageUI.Show(successor.buddyName + " gets shoved forward. New leader.");
        ClosePanel(true);
    }

    void ConvertBuddyToPlayer(BuddyData buddy)
    {
        if (GameState.Instance.gobbo == null)
            GameState.Instance.gobbo = new GobboSaveData();

        object gobbo = GameState.Instance.gobbo;

        SetField(gobbo, "level", Mathf.Max(1, buddy.level));
        SetField(gobbo, "maxHealth", Mathf.Max(1, buddy.maxHealth));
        SetField(gobbo, "health", Mathf.Max(1, buddy.maxHealth));
        SetField(gobbo, "attack", Mathf.Max(1, buddy.damage));
        SetField(gobbo, "defense", Mathf.Max(0, buddy.defense));

        // Player-only defaults. Reflection keeps this safe if your save class changes names later.
        SetFieldIfExists(gobbo, "digPower", 1);
        SetFieldIfExists(gobbo, "digRadius", 1f);
        SetFieldIfExists(gobbo, "moveSpeed", Mathf.Max(3f, buddy.moveSpeed));

        Debug.Log("Successor chosen: " + buddy.buddyName + " became player gobbo.");
    }

    void RemoveSuccessorFromRoster(string buddyId)
    {
        if (string.IsNullOrWhiteSpace(buddyId) || GameState.Instance == null)
            return;

        if (GameState.Instance.ownedBuddies != null)
            GameState.Instance.ownedBuddies.RemoveAll(b => b == null || b.uniqueId == buddyId);

        if (GameState.Instance.activeSquadIds != null)
            GameState.Instance.activeSquadIds.RemoveAll(id => id == buddyId);
    }

    void SetField(object target, string fieldName, object value)
    {
        SetFieldIfExists(target, fieldName, value);
    }

    void SetFieldIfExists(object target, string fieldName, object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(fieldName))
            return;

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
            return;

        if (field.FieldType == typeof(int))
            field.SetValue(target, Mathf.RoundToInt(System.Convert.ToSingle(value)));
        else if (field.FieldType == typeof(float))
            field.SetValue(target, System.Convert.ToSingle(value));
        else if (field.FieldType == typeof(string))
            field.SetValue(target, value != null ? value.ToString() : "");
        else
            field.SetValue(target, value);
    }

    void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i]);
        }

        spawnedRows.Clear();
    }
}
