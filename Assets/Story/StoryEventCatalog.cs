using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StoryEventDefinition
{
    public string eventId = "";
    public string developerLabel = "";
    [TextArea(2, 4)] public string developerDescription = "";
}

[CreateAssetMenu(fileName = "Story Event Catalog", menuName = "Spore Gobbo/Story/Story Event Catalog")]
public sealed class StoryEventCatalog : ScriptableObject
{
    public List<StoryEventDefinition> events = new List<StoryEventDefinition>();
}
