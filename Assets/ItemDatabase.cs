using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("Definitions")]
    public List<ItemDefinition> items = new List<ItemDefinition>();

    private readonly Dictionary<string, ItemDefinition> lookup = new Dictionary<string, ItemDefinition>();
    private bool lookupBuilt;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null)
            transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    public static ItemDefinition Get(string itemId)
    {
        ItemDatabase database = Instance;
        if (database == null)
            database = Object.FindAnyObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        return database != null ? database.GetById(itemId) : null;
    }

    public ItemDefinition GetById(string itemId)
    {
        if (!lookupBuilt) RebuildLookup();
        string normalized = ItemIdUtility.Normalize(itemId);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return lookup.TryGetValue(normalized, out ItemDefinition definition) ? definition : null;
    }

    public IReadOnlyList<ItemDefinition> GetDefinitions()
    {
        return items;
    }

    public void RebuildLookup()
    {
        lookup.Clear();
        lookupBuilt = true;

        if (items == null)
        {
            items = new List<ItemDefinition>();
            return;
        }

        foreach (ItemDefinition definition in items)
        {
            if (definition == null) continue;
            string id = definition.NormalizedId;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("ItemDatabase has an ItemDefinition with a blank itemId.", definition);
                continue;
            }

            if (lookup.ContainsKey(id))
            {
                Debug.LogWarning("ItemDatabase duplicate itemId ignored: " + id, definition);
                continue;
            }

            lookup.Add(id, definition);
        }
    }

    void OnValidate()
    {
        lookupBuilt = false;
    }
}
