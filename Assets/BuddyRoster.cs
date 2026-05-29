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
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();
    public List<GobboUnitSaveData> activeSquad = new List<GobboUnitSaveData>();

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

    public GobboUnitSaveData CreateNewBuddy() => CreateNewBuddy(BuddyType.Baby, "");

    public GobboUnitSaveData CreateNewBuddy(BuddyType type, string chosenName = "")
    {
        BuddyTypeSetup setup = GetSetup(type);
        GobboUnitSaveData unit = new GobboUnitSaveData();
        unit.EnsureId();
        unit.gobboType = type;
        unit.ageStage = type == BuddyType.Baby ? GobboAgeStage.Baby : GobboAgeStage.Young;
        unit.displayName = string.IsNullOrWhiteSpace(chosenName) ? GetRandomBuddyName() : chosenName;
        if (type == BuddyType.Baby) BuddyProgression.PrepareNewBaby(unit);
        ApplySetupToBuddy(unit, setup);
        ownedGobbos.Add(unit);
        if (activeSquad.Count < maxActiveSquad)
        {
            unit.isInActiveSquad = true;
            activeSquad.Add(unit);
        }
        else unit.isInActiveSquad = false;
        return unit;
    }

    public void ApplySetupToBuddy(GobboUnitSaveData unit, BuddyTypeSetup setup)
    {
        if (unit == null) return;
        unit.EnsureId();
        unit.EnsureRuntimeDefaults();
        if (setup == null) setup = GetSetup(unit.gobboType);
        if (setup == null) return;
        unit.maxHealth = setup.maxHealth;
        unit.health = setup.maxHealth;
        unit.damage = setup.damage;
        unit.attack = setup.damage;
        unit.defense = setup.defense;
        unit.moveSpeed = setup.moveSpeed;
        unit.attackCooldown = setup.attackCooldown;
        unit.bodyColor = setup.bodyColor;
        unit.onlyFightsAfterHit = setup.onlyFightsAfterHit;
        unit.collectsFood = setup.collectsFood;
        unit.hasBeenHit = false;
        unit.visualSetId = !string.IsNullOrWhiteSpace(setup.defaultVisualSetId)
            ? setup.defaultVisualSetId
            : unit.gobboType.ToString().ToLowerInvariant() + "_" + unit.ageStage.ToString().ToLowerInvariant();
    }

    public void RemoveBuddy(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId)) return;
        ownedGobbos.RemoveAll(g => g == null || g.uniqueId == gobboId);
        activeSquad.RemoveAll(g => g == null || g.uniqueId == gobboId);
        RepairRosterState();
    }

    public void RemoveBuddy(GobboUnitSaveData unit)
    {
        if (unit == null) return;
        unit.EnsureId();
        RemoveBuddy(unit.uniqueId);
    }

    public List<BuddyTypeSetup> GetRandomBuddyChoices(int amount)
    {
        List<BuddyTypeSetup> pool = new List<BuddyTypeSetup>();
        foreach (BuddyTypeSetup setup in buddyTypes)
            if (setup != null && setup.buddyType != BuddyType.Baby) pool.Add(setup);
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
            if (setup != null && setup.buddyType == type) return setup;
        return null;
    }

    public GobboUnitSaveData FindBuddyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureId();
            unit.EnsureRuntimeDefaults();
            if (unit.uniqueId == id) return unit;
        }
        return null;
    }

    public List<GobboUnitSaveData> GetReserveBuddies()
    {
        RepairRosterState();
        List<GobboUnitSaveData> reserve = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
            if (unit != null && !unit.isInActiveSquad) reserve.Add(unit);
        return reserve;
    }

    public void RenameBuddy(string id, string newName)
    {
        GobboUnitSaveData unit = FindBuddyById(id);
        if (unit == null || string.IsNullOrWhiteSpace(newName)) return;
        unit.displayName = newName.Trim();
    }

    public bool MoveBuddyToActiveSquad(string id)
    {
        RepairRosterState();
        GobboUnitSaveData unit = FindBuddyById(id);
        if (unit == null) return false;
        if (unit.isInActiveSquad) return true;
        if (activeSquad.Count >= maxActiveSquad) return false;
        unit.isInActiveSquad = true;
        activeSquad.Add(unit);
        return true;
    }

    public bool MoveBuddyToReserve(string id)
    {
        RepairRosterState();
        GobboUnitSaveData unit = FindBuddyById(id);
        if (unit == null) return false;
        unit.isInActiveSquad = false;
        activeSquad.RemoveAll(g => g == null || g.uniqueId == id);
        return true;
    }

    public bool SwapBuddies(string activeBuddyId, string reserveBuddyId)
    {
        RepairRosterState();
        GobboUnitSaveData activeUnit = FindBuddyById(activeBuddyId);
        GobboUnitSaveData reserveUnit = FindBuddyById(reserveBuddyId);
        if (activeUnit == null || reserveUnit == null) return false;
        activeUnit.isInActiveSquad = false;
        reserveUnit.isInActiveSquad = true;
        RebuildActiveSquadFromOwned();
        return true;
    }

    public void LoadRoster(List<GobboUnitSaveData> owned, List<string> activeIds)
    {
        ownedGobbos = new List<GobboUnitSaveData>();
        activeSquad = new List<GobboUnitSaveData>();
        if (owned != null)
        {
            foreach (GobboUnitSaveData unit in owned)
            {
                if (unit == null) continue;
                GobboUnitSaveData copy = unit.CloneUnit();
                copy.EnsureId();
                copy.isInActiveSquad = false;
                ownedGobbos.Add(copy);
            }
        }
        if (activeIds != null)
        {
            foreach (string id in activeIds)
            {
                GobboUnitSaveData unit = FindBuddyById(id);
                if (unit == null || activeSquad.Count >= maxActiveSquad) continue;
                unit.isInActiveSquad = true;
                activeSquad.Add(unit);
            }
        }
        RepairRosterState();
    }

    public void RepairRosterState()
    {
        ownedGobbos.RemoveAll(g => g == null);
        foreach (GobboUnitSaveData unit in ownedGobbos) unit.EnsureId();
        activeSquad.RemoveAll(g => g == null || !ownedGobbos.Contains(g));
        foreach (GobboUnitSaveData unit in ownedGobbos) unit.isInActiveSquad = activeSquad.Contains(unit);
        while (activeSquad.Count > maxActiveSquad)
        {
            GobboUnitSaveData removed = activeSquad[activeSquad.Count - 1];
            activeSquad.RemoveAt(activeSquad.Count - 1);
            if (removed != null) removed.isInActiveSquad = false;
        }
    }

    void RebuildActiveSquadFromOwned()
    {
        activeSquad.Clear();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null || !unit.isInActiveSquad) continue;
            if (activeSquad.Count >= maxActiveSquad)
            {
                unit.isInActiveSquad = false;
                continue;
            }
            activeSquad.Add(unit);
        }
    }

    string GetRandomBuddyName()
    {
        string[] names = { "Grub", "Pip", "Mug", "Bunk", "Snorp", "Wim", "Grot", "Bibble", "Nub", "Boil", "Lump", "Pickle" };
        return names[Random.Range(0, names.Length)];
    }

    void CreateDefaultBuddyTypesIfEmpty()
    {
        if (buddyTypes != null && buddyTypes.Count > 0) return;
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
