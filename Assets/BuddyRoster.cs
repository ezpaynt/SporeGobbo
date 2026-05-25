using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuddyTypeSetup
{
    public BuddyType buddyType;
    public string displayName;

    [Header("Stats")]
    public int maxHealth = 5;
    public int damage = 1;
    public int defense = 0;
    public float moveSpeed = 3.5f;
    public float attackCooldown = 0.8f;

    [Header("Combat Behavior")]
    public bool onlyFightsAfterHit = false;
    public bool collectsFood = false;

    [Header("Look")]
    public Color bodyColor = Color.green;
    public Vector3 prefabScale = Vector3.one;
    public string defaultVisualSetId = "";
}

public class BuddyRoster : MonoBehaviour
{
    [Header("Squad")]
    public int maxActiveSquad = 5;

    [Header("Buddy Type Setups")]
    public List<BuddyTypeSetup> buddyTypes = new List<BuddyTypeSetup>();

    [Header("Runtime Buddy Lists")]
    public List<BuddyData> ownedBuddies = new List<BuddyData>();
    public List<BuddyData> activeSquad = new List<BuddyData>();

    void Awake()
    {
        CreateDefaultBuddyTypesIfEmpty();
        RepairRosterState();
    }

    public bool CanAddToSquad()
    {
        RepairRosterState();
        return activeSquad.Count < maxActiveSquad;
    }

    public BuddyData CreateNewBuddy()
    {
        return CreateNewBuddy(BuddyType.Baby, "");
    }

    public BuddyData CreateNewBuddy(BuddyType type, string chosenName = "")
    {
        BuddyTypeSetup setup = GetSetup(type);

        BuddyData buddy = new BuddyData();
        buddy.EnsureId();

        buddy.buddyType = type;
        buddy.ageStage = type == BuddyType.Baby ? GobboAgeStage.Baby : GobboAgeStage.Young;
        buddy.buddyName = string.IsNullOrWhiteSpace(chosenName)
            ? GetRandomBuddyName()
            : chosenName;

        if (type == BuddyType.Baby)
            BuddyProgression.PrepareNewBaby(buddy);

        ApplySetupToBuddy(buddy, setup);

        ownedBuddies.Add(buddy);

        if (activeSquad.Count < maxActiveSquad)
        {
            buddy.isInActiveSquad = true;
            activeSquad.Add(buddy);
        }
        else
        {
            buddy.isInActiveSquad = false;
        }

        return buddy;
    }

    public void ApplySetupToBuddy(BuddyData buddy, BuddyTypeSetup setup)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        if (setup == null)
            setup = GetSetup(buddy.buddyType);

        if (setup == null)
            return;

        buddy.maxHealth = setup.maxHealth;
        buddy.health = setup.maxHealth;
        buddy.damage = setup.damage;
        buddy.defense = setup.defense;
        buddy.moveSpeed = setup.moveSpeed;
        buddy.attackCooldown = setup.attackCooldown;
        buddy.bodyColor = setup.bodyColor;
        buddy.onlyFightsAfterHit = setup.onlyFightsAfterHit;
        buddy.collectsFood = setup.collectsFood;
        buddy.hasBeenHit = false;

        if (!string.IsNullOrWhiteSpace(setup.defaultVisualSetId))
            buddy.visualSetId = setup.defaultVisualSetId;
        else
            buddy.visualSetId = buddy.buddyType.ToString().ToLowerInvariant() + "_" + buddy.ageStage.ToString().ToLowerInvariant();
    }

    public void RemoveBuddy(string buddyId)
    {
        if (string.IsNullOrWhiteSpace(buddyId))
            return;

        ownedBuddies.RemoveAll(b => b == null || b.uniqueId == buddyId);
        activeSquad.RemoveAll(b => b == null || b.uniqueId == buddyId);

        RepairRosterState();
    }

    public void RemoveBuddy(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        RemoveBuddy(buddy.uniqueId);
    }

    public List<BuddyTypeSetup> GetRandomBuddyChoices(int amount)
    {
        List<BuddyTypeSetup> pool = new List<BuddyTypeSetup>();

        foreach (BuddyTypeSetup setup in buddyTypes)
        {
            if (setup != null && setup.buddyType != BuddyType.Baby)
                pool.Add(setup);
        }

        List<BuddyTypeSetup> choices = new List<BuddyTypeSetup>();

        while (choices.Count < amount && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            choices.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return choices;
    }

    public BuddyTypeSetup GetSetup(BuddyType type)
    {
        foreach (BuddyTypeSetup setup in buddyTypes)
        {
            if (setup != null && setup.buddyType == type)
                return setup;
        }

        return null;
    }

    public BuddyData FindBuddyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            if (buddy.uniqueId == id)
                return buddy;
        }

        return null;
    }

    public List<BuddyData> GetReserveBuddies()
    {
        RepairRosterState();

        List<BuddyData> reserve = new List<BuddyData>();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy != null && !buddy.isInActiveSquad)
                reserve.Add(buddy);
        }

        return reserve;
    }

    public void RenameBuddy(string id, string newName)
    {
        BuddyData buddy = FindBuddyById(id);

        if (buddy == null || string.IsNullOrWhiteSpace(newName))
            return;

        buddy.buddyName = newName.Trim();
    }

    public bool MoveBuddyToActiveSquad(string id)
    {
        RepairRosterState();

        BuddyData buddy = FindBuddyById(id);

        if (buddy == null)
            return false;

        if (buddy.isInActiveSquad)
            return true;

        if (activeSquad.Count >= maxActiveSquad)
            return false;

        buddy.isInActiveSquad = true;
        activeSquad.Add(buddy);
        return true;
    }

    public bool MoveBuddyToReserve(string id)
    {
        RepairRosterState();

        BuddyData buddy = FindBuddyById(id);

        if (buddy == null)
            return false;

        buddy.isInActiveSquad = false;
        activeSquad.RemoveAll(b => b == null || b.uniqueId == id);
        return true;
    }

    public bool SwapBuddies(string activeBuddyId, string reserveBuddyId)
    {
        RepairRosterState();

        BuddyData activeBuddy = FindBuddyById(activeBuddyId);
        BuddyData reserveBuddy = FindBuddyById(reserveBuddyId);

        if (activeBuddy == null || reserveBuddy == null)
            return false;

        activeBuddy.isInActiveSquad = false;
        reserveBuddy.isInActiveSquad = true;

        RebuildActiveSquadFromOwned();
        return true;
    }

    public void LoadRoster(List<BuddyData> owned, List<string> activeIds)
    {
        ownedBuddies = new List<BuddyData>();
        activeSquad = new List<BuddyData>();

        if (owned != null)
        {
            foreach (BuddyData buddy in owned)
            {
                if (buddy == null)
                    continue;

                BuddyData copy = buddy.Clone();
                copy.EnsureId();
                copy.isInActiveSquad = false;
                ownedBuddies.Add(copy);
            }
        }

        if (activeIds != null)
        {
            foreach (string id in activeIds)
            {
                BuddyData buddy = FindBuddyById(id);

                if (buddy == null || activeSquad.Count >= maxActiveSquad)
                    continue;

                buddy.isInActiveSquad = true;
                activeSquad.Add(buddy);
            }
        }

        RepairRosterState();
    }

    public void RepairRosterState()
    {
        ownedBuddies.RemoveAll(b => b == null);

        foreach (BuddyData buddy in ownedBuddies)
            buddy.EnsureId();

        activeSquad.RemoveAll(b => b == null || !ownedBuddies.Contains(b));

        foreach (BuddyData buddy in ownedBuddies)
            buddy.isInActiveSquad = activeSquad.Contains(buddy);

        while (activeSquad.Count > maxActiveSquad)
        {
            BuddyData removed = activeSquad[activeSquad.Count - 1];
            activeSquad.RemoveAt(activeSquad.Count - 1);

            if (removed != null)
                removed.isInActiveSquad = false;
        }
    }

    void RebuildActiveSquadFromOwned()
    {
        activeSquad.Clear();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null || !buddy.isInActiveSquad)
                continue;

            if (activeSquad.Count >= maxActiveSquad)
            {
                buddy.isInActiveSquad = false;
                continue;
            }

            activeSquad.Add(buddy);
        }
    }

    string GetRandomBuddyName()
    {
        string[] names =
        {
            "Grub", "Pip", "Mug", "Bunk", "Snorp", "Wim",
            "Grot", "Bibble", "Nub", "Boil", "Lump", "Pickle"
        };

        return names[Random.Range(0, names.Length)];
    }

    void CreateDefaultBuddyTypesIfEmpty()
    {
        if (buddyTypes != null && buddyTypes.Count > 0)
            return;

        buddyTypes = new List<BuddyTypeSetup>();

        AddType(BuddyType.Baby, "Baby Gobbo", 4, 1, 0, 3.5f, 0.9f, false, false, new Color(0.55f, 0.95f, 0.55f), Vector3.one * 0.75f);
        AddType(BuddyType.Fast, "Fast Gobbo", 5, 1, 0, 5.2f, 0.65f, false, false, new Color(0.35f, 0.9f, 0.9f), Vector3.one * 0.9f);
        AddType(BuddyType.Fat, "Fat Gobbo", 10, 1, 1, 2.8f, 0.9f, false, false, new Color(0.45f, 0.9f, 0.35f), Vector3.one * 1.25f);
        AddType(BuddyType.Scavenger, "Scavenger Gobbo", 6, 1, 0, 3.8f, 0.8f, true, true, new Color(0.9f, 0.75f, 0.25f), Vector3.one);
        AddType(BuddyType.Tank, "Tank Gobbo", 12, 1, 2, 2.6f, 1.0f, false, false, new Color(0.35f, 0.65f, 0.35f), Vector3.one * 1.15f);
        AddType(BuddyType.Thrower, "Thrower Gobbo", 7, 1, 0, 3.4f, 0.9f, false, false, new Color(0.65f, 0.85f, 0.45f), Vector3.one);
        AddType(BuddyType.Strong, "Strong Gobbo", 9, 2, 0, 3.1f, 0.95f, false, false, new Color(0.75f, 0.55f, 0.35f), Vector3.one * 1.08f);
        AddType(BuddyType.Fungal, "Fungal Gobbo", 8, 1, 0, 3.0f, 0.9f, false, false, new Color(0.6f, 0.95f, 0.45f), Vector3.one);
        AddType(BuddyType.Explosive, "Explosive Gobbo", 7, 2, 0, 3.4f, 1.0f, false, false, new Color(1f, 0.55f, 0.25f), Vector3.one);
    }

    void AddType(BuddyType type, string name, int hp, int damage, int defense, float speed, float cooldown, bool onlyAfterHit, bool collects, Color color, Vector3 scale)
    {
        buddyTypes.Add(new BuddyTypeSetup
        {
            buddyType = type,
            displayName = name,
            maxHealth = hp,
            damage = damage,
            defense = defense,
            moveSpeed = speed,
            attackCooldown = cooldown,
            onlyFightsAfterHit = onlyAfterHit,
            collectsFood = collects,
            bodyColor = color,
            prefabScale = scale,
            defaultVisualSetId = type == BuddyType.Baby ? "baby" : type.ToString().ToLowerInvariant() + "_young"
        });
    }
}
