using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class CampOldBonesWall : MonoBehaviour, ICampInteractable
{
    [Header("Interaction")]
    public string interactPrompt = "Read old bones";

    [Header("Assigned UI")]
    [Tooltip("Optional. If assigned, the wall opens this panel. If missing, it falls back to CampMessageUI.")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button closeButton;

    [Header("Text")]
    public string title = "Old Bones Wall";
    [TextArea(2, 5)] public string emptyMessage = "No little bones yet. Somehow.";
    public int maxEntriesShownInMessageFallback = 6;

    private bool isOpen;

    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        Close();
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public string GetInteractPrompt()
    {
        return isOpen ? "Close old bones" : interactPrompt;
    }

    public void Interact(GobboController player)
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        List<string> entries = BuildDeathEntries();
        string body = BuildBody(entries);

        if (panel != null && bodyText != null)
        {
            isOpen = true;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();

            if (titleText != null)
                titleText.text = title;

            bodyText.text = body;
        }
        else
        {
            isOpen = false;
            CampMessageUI.Show(BuildFallbackMessage(entries));
        }
    }

    public void Close()
    {
        isOpen = false;

        if (panel != null)
            panel.SetActive(false);
    }

    string BuildBody(List<string> entries)
    {
        if (entries == null || entries.Count == 0)
            return emptyMessage;

        string text = "The camp remembers:";
        for (int i = 0; i < entries.Count; i++)
            text += "\n• " + entries[i];

        return text;
    }

    string BuildFallbackMessage(List<string> entries)
    {
        if (entries == null || entries.Count == 0)
            return emptyMessage;

        string text = title + ":";
        int count = Mathf.Min(entries.Count, maxEntriesShownInMessageFallback);

        for (int i = 0; i < count; i++)
            text += "\n• " + entries[i];

        if (entries.Count > count)
            text += "\n...and more.";

        return text;
    }

    List<string> BuildDeathEntries()
    {
        List<string> entries = new List<string>();

        if (GameState.Instance == null)
            return entries;

        // Known current run summary path. This is definitely present in your camp report flow.
        AddRunSummaryDeadNames(entries);

        // Future-proof path: if GameState later exposes fallen/dead/bones lists, this wall will find many of them
        // without needing a rewrite. Duplicate text is filtered.
        AddReflectedDeathLikeEntries(GameState.Instance, entries, 0);

        return Deduplicate(entries);
    }

    void AddRunSummaryDeadNames(List<string> entries)
    {
        object state = GameState.Instance;
        object lastRun = GetMemberValue(state, "lastRun");

        if (lastRun == null)
            return;

        object deadBuddyNames = GetMemberValue(lastRun, "deadBuddyNames");
        AddEnumerableEntries(deadBuddyNames, entries, "buddy");
    }

    void AddReflectedDeathLikeEntries(object source, List<string> entries, int depth)
    {
        if (source == null || depth > 1)
            return;

        System.Type type = source.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (!LooksDeathRelated(field.Name))
                continue;

            object value = field.GetValue(source);
            AddObjectOrEnumerable(value, entries, depth + 1);
        }

        foreach (PropertyInfo prop in type.GetProperties(flags))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0 || !LooksDeathRelated(prop.Name))
                continue;

            object value = null;
            try { value = prop.GetValue(source, null); }
            catch { continue; }

            AddObjectOrEnumerable(value, entries, depth + 1);
        }
    }

    bool LooksDeathRelated(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string lower = name.ToLowerInvariant();
        return lower.Contains("dead") ||
               lower.Contains("death") ||
               lower.Contains("fallen") ||
               lower.Contains("lost") ||
               lower.Contains("bone") ||
               lower.Contains("grave");
    }

    void AddObjectOrEnumerable(object value, List<string> entries, int depth)
    {
        if (value == null)
            return;

        if (value is string singleString)
        {
            AddEntry(entries, singleString);
            return;
        }

        if (value is BuddyData buddy)
        {
            AddBuddyEntry(entries, buddy);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            AddEnumerableEntries(enumerable, entries, "buddy");
            return;
        }

        AddReflectedDeathLikeEntries(value, entries, depth);
    }

    void AddEnumerableEntries(object enumerableObject, List<string> entries, string fallbackType)
    {
        if (enumerableObject == null)
            return;

        if (!(enumerableObject is IEnumerable enumerable) || enumerableObject is string)
            return;

        foreach (object item in enumerable)
        {
            if (item == null)
                continue;

            if (item is string name)
            {
                AddEntry(entries, name);
            }
            else if (item is BuddyData buddy)
            {
                AddBuddyEntry(entries, buddy);
            }
            else
            {
                string reflected = BuildReflectedEntry(item, fallbackType);
                AddEntry(entries, reflected);
            }
        }
    }

    void AddBuddyEntry(List<string> entries, BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureRuntimeDefaults();
        AddEntry(entries, buddy.buddyName + " the " + buddy.buddyType + " — Lv " + buddy.level);
    }

    string BuildReflectedEntry(object item, string fallbackType)
    {
        string name = GetStringMember(item, "buddyName");
        if (string.IsNullOrWhiteSpace(name)) name = GetStringMember(item, "name");
        if (string.IsNullOrWhiteSpace(name)) name = GetStringMember(item, "leaderName");
        if (string.IsNullOrWhiteSpace(name)) name = "Unknown " + fallbackType;

        string type = GetStringMember(item, "buddyType");
        if (string.IsNullOrWhiteSpace(type)) type = GetStringMember(item, "type");

        string cause = GetStringMember(item, "causeOfDeath");
        if (string.IsNullOrWhiteSpace(cause)) cause = GetStringMember(item, "deathCause");

        string runs = GetStringMember(item, "runsSurvived");

        string text = name;
        if (!string.IsNullOrWhiteSpace(type)) text += " the " + type;
        if (!string.IsNullOrWhiteSpace(runs)) text += " — runs survived: " + runs;
        if (!string.IsNullOrWhiteSpace(cause)) text += " — " + cause;
        return text;
    }

    object GetMemberValue(object source, string memberName)
    {
        if (source == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        System.Type type = source.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(source);

        PropertyInfo prop = type.GetProperty(memberName, flags);
        if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
        {
            try { return prop.GetValue(source, null); }
            catch { return null; }
        }

        return null;
    }

    string GetStringMember(object source, string memberName)
    {
        object value = GetMemberValue(source, memberName);
        return value != null ? value.ToString() : "";
    }

    void AddEntry(List<string> entries, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return;

        entries.Add(entry.Trim());
    }

    List<string> Deduplicate(List<string> raw)
    {
        List<string> clean = new List<string>();
        HashSet<string> seen = new HashSet<string>();

        foreach (string entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            if (seen.Add(entry))
                clean.Add(entry);
        }

        return clean;
    }
}
