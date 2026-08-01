using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class JournalEntryDefinition
{
    public string entryId = "";
    public string requiredStoryEventId = "";
    [TextArea(4, 12)] public string bodyText = "";
}

[Serializable]
public sealed class JournalThreadDefinition
{
    public string threadId = "";
    public string displayName = "";
    public int sortOrder = 0;
    public List<JournalEntryDefinition> entries = new List<JournalEntryDefinition>();
}

[CreateAssetMenu(fileName = "Journal Content Library", menuName = "Spore Gobbo/Story/Journal Content Library")]
public sealed class JournalContentLibrary : ScriptableObject
{
    public List<JournalThreadDefinition> threads = new List<JournalThreadDefinition>();
}
