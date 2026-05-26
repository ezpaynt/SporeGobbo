using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampOldBonesWall : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Read old bones";

    [Header("UI")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button closeButton;

    [Header("Text")]
    public string title = "Old Bones Wall";
    public string emptyMessage = "No little bones remembered yet.";
    public string unknownCauseText = "Cause unknown";

    private bool isOpen = false;

    void Awake()
    {
        HookButtons();
        CloseMenu();
    }

    void Start()
    {
        HookButtons();
        CloseMenu();
    }

    void Update()
    {
        if (!isOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public string GetInteractPrompt()
    {
        return isOpen ? "Close old bones" : interactPrompt;
    }

    public void Interact(GobboController player)
    {
        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (panel == null)
        {
            Debug.LogWarning("CampOldBonesWall has no Panel assigned.", this);
            CampMessageUI.Show("Old Bones wall menu is not wired yet.");
            return;
        }

        isOpen = true;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = title;

        RefreshText();
    }

    public void CloseMenu()
    {
        isOpen = false;

        if (panel != null)
            panel.SetActive(false);
    }

    void HookButtons()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(CloseMenu);
    }

    void RefreshText()
    {
        if (bodyText == null)
        {
            Debug.LogWarning("CampOldBonesWall has no Body Text assigned.", this);
            return;
        }

        if (GameState.Instance == null)
        {
            bodyText.text = "No GameState found.";
            return;
        }

        string text = BuildBonesText();

        if (string.IsNullOrWhiteSpace(text))
            text = emptyMessage;

        bodyText.text = text;
    }

    string BuildBonesText()
    {
        object state = GameState.Instance;
        System.Type type = state.GetType();

        string[] possibleFieldNames =
        {
            "deadBuddies",
            "fallenBuddies",
            "deadBuddyHistory",
            "fallenBuddyHistory",
            "oldBones",
            "oldBonesRecords"
        };

        foreach (string fieldName in possibleFieldNames)
        {
            System.Reflection.FieldInfo field = type.GetField(fieldName);
            if (field == null)
                continue;

            object value = field.GetValue(state);
            string built = BuildTextFromListObject(value);
            if (!string.IsNullOrWhiteSpace(built))
                return built;
        }

        RunSummaryData lastRun = GameState.Instance.lastRun;
        if (lastRun != null && lastRun.deadBuddyNames != null && lastRun.deadBuddyNames.Count > 0)
        {
            string text = "";
            foreach (string name in lastRun.deadBuddyNames)
                text += "☠ " + name + "\n";

            return text.TrimEnd();
        }

        return "";
    }

    string BuildTextFromListObject(object value)
    {
        if (value == null)
            return "";

        System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
        if (enumerable == null)
            return "";

        string text = "";

        foreach (object item in enumerable)
        {
            if (item == null)
                continue;

            text += BuildLineFromRecord(item) + "\n";
        }

        return text.TrimEnd();
    }

    string BuildLineFromRecord(object record)
    {
        if (record == null)
            return "";

        System.Type type = record.GetType();

        string name = GetStringFieldOrProperty(type, record, "buddyName");
        if (string.IsNullOrWhiteSpace(name))
            name = GetStringFieldOrProperty(type, record, "name");
        if (string.IsNullOrWhiteSpace(name))
            name = GetStringFieldOrProperty(type, record, "deadBuddyName");
        if (string.IsNullOrWhiteSpace(name))
            name = "Unknown Gobbo";

        string kind = GetStringFieldOrProperty(type, record, "type");
        if (string.IsNullOrWhiteSpace(kind))
            kind = GetStringFieldOrProperty(type, record, "buddyType");

        string cause = GetStringFieldOrProperty(type, record, "causeOfDeath");
        if (string.IsNullOrWhiteSpace(cause))
            cause = GetStringFieldOrProperty(type, record, "deathCause");
        if (string.IsNullOrWhiteSpace(cause))
            cause = unknownCauseText;

        int runs = GetIntFieldOrProperty(type, record, "runsSurvived", -1);

        string line = "☠ " + name;

        if (!string.IsNullOrWhiteSpace(kind))
            line += " the " + kind;

        if (runs >= 0)
            line += " — " + runs + " runs";

        line += "\n   " + cause;

        return line;
    }

    string GetStringFieldOrProperty(System.Type type, object obj, string name)
    {
        System.Reflection.FieldInfo field = type.GetField(name);
        if (field != null)
        {
            object value = field.GetValue(obj);
            return value != null ? value.ToString() : "";
        }

        System.Reflection.PropertyInfo prop = type.GetProperty(name);
        if (prop != null)
        {
            object value = prop.GetValue(obj, null);
            return value != null ? value.ToString() : "";
        }

        return "";
    }

    int GetIntFieldOrProperty(System.Type type, object obj, string name, int fallback)
    {
        System.Reflection.FieldInfo field = type.GetField(name);
        if (field != null)
        {
            object value = field.GetValue(obj);
            if (value is int intValue)
                return intValue;
        }

        System.Reflection.PropertyInfo prop = type.GetProperty(name);
        if (prop != null)
        {
            object value = prop.GetValue(obj, null);
            if (value is int intValue)
                return intValue;
        }

        return fallback;
    }
}
