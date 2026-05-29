using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GobboSaveData : GobboUnitSaveData
{
    // Compatibility alias. The real permanent id is uniqueId.
    public string gobboId
    {
        get => uniqueId;
        set => uniqueId = value;
    }

    public GobboSaveData()
    {
        isLeader = true;
        displayName = "Gobbo";
        maxHealth = 100;
        health = 100;
        attack = 5;
        damage = 5;
        defense = 2;
        moveSpeed = 5f;
        attackCooldown = 0.7f;
    }
}

[System.Serializable]
public class RunSummaryData
{
    public bool survived = true;
    public float timeSpent = 0f;
    public int runNumber = 1;

    public int playerLevelStart = 1;
    public int playerLevelEnd = 1;
    public int xpStart = 0;
    public int xpEnd = 0;
    public int xpGained = 0;

    public int sporesStart = 0;
    public int sporesEnd = 0;
    public int sporesGained = 0;

    public int mushroomsStart = 0;
    public int mushroomsEnd = 0;
    public int mushroomsGained = 0;

    public int moneyStart = 0;
    public int moneyEnd = 0;
    public int moneyGained = 0;

    public int foodValueGained = 0;

    public int shiniesStart = 0;
    public int shiniesEnd = 0;
    public int shiniesGained = 0;

    public int buddiesStart = 0;
    public int buddiesEnd = 0;
    public int buddiesFound = 0;
    public int buddiesLost = 0;
    public int enemiesKilled = 0;

    public List<string> newBuddyNames = new List<string>();
    public List<string> deadBuddyNames = new List<string>();
    public List<string> upgradesChosen = new List<string>();
}

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Run Progress")]
    public int currentRunNumber = 1;

    [Header("Leader Save")]
    public GobboSaveData gobbo = new GobboSaveData();

    [Header("Unified Roster Save")]
    public int maxActiveSquad = 5;
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();
    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";

    [Header("Compatibility Roster Mirror - Do Not Treat As Source Of Truth")]
    public List<BuddyData> ownedBuddies = new List<BuddyData>();

    [Header("Camp Save")]
    public int campLevel = 1;
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("Last Run")]
    public RunSummaryData lastRun = new RunSummaryData();

    private float runStartTime = 0f;
    private GobboSaveData runStartGobboSnapshot;
    private List<GobboUnitSaveData> runStartGobboRosterSnapshot = new List<GobboUnitSaveData>();
    private List<string> runStartBuddyIds = new List<string>();
    private List<string> deadBuddyIdsThisRun = new List<string>();
    private List<string> deadBuddyNamesThisRun = new List<string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureRuntimeDefaults();
    }

    public void EnsureRuntimeDefaults()
    {
        if (gobbo == null) gobbo = new GobboSaveData();
        gobbo.EnsureLeaderIdentity(gobbo.displayName);

        ownedGobbos ??= new List<GobboUnitSaveData>();
        ownedBuddies ??= new List<BuddyData>();
        activeSquadIds ??= new List<string>();
        unlockedStations ??= new List<string>();
        decorationsUnlocked ??= new List<string>();
        if (lastRun == null) lastRun = new RunSummaryData();

        RepairRosterState();
    }

    public void BeginRunSnapshot()
    {
        EnsureRuntimeDefaults();
        runStartTime = Time.time;
        runStartGobboSnapshot = CloneGobboSave(gobbo);
        runStartGobboRosterSnapshot = CloneUnitList(ownedGobbos);
        runStartBuddyIds.Clear();
        deadBuddyIdsThisRun.Clear();
        deadBuddyNamesThisRun.Clear();

        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            runStartBuddyIds.Add(unit.uniqueId);
        }

        lastRun = new RunSummaryData();
        lastRun.runNumber = currentRunNumber;
        lastRun.playerLevelStart = gobbo.level;
        lastRun.xpStart = gobbo.xp;
        lastRun.sporesStart = gobbo.spores;
        lastRun.mushroomsStart = gobbo.mushrooms;
        lastRun.moneyStart = gobbo.money;
        lastRun.shiniesStart = gobbo.shinies;
        lastRun.buddiesStart = runStartBuddyIds.Count;
    }

    public void CaptureCurrentRunStartState()
    {
        GobboController player = Object.FindAnyObjectByType<GobboController>();
        if (player != null) SavePlayer(player);
        RepairRosterState();
        BeginRunSnapshot();
    }

    public void SaveFromRun()
    {
        GobboController player = Object.FindAnyObjectByType<GobboController>();
        GobboSaveData before = runStartGobboSnapshot != null ? runStartGobboSnapshot : CloneGobboSave(gobbo);
        List<string> beforeIds = runStartBuddyIds.Count > 0 ? new List<string>(runStartBuddyIds) : GetOwnedBuddyIds();
        List<GobboUnitSaveData> beforeGobbos = runStartGobboRosterSnapshot.Count > 0 ? CloneUnitList(runStartGobboRosterSnapshot) : CloneUnitList(ownedGobbos);

        if (player != null) SavePlayer(player);
        SaveVisibleRunBuddies();
        RepairRosterState();

        BuddyProgression.DistributeEndRunFoodXP(this, lastRun != null ? lastRun.foodValueGained : 0);

        bool survived = player != null && player.gameObject.activeInHierarchy;
        BuildRunSummary(before, beforeIds, beforeGobbos, survived);
        currentRunNumber = Mathf.Max(1, currentRunNumber + 1);
        gobbo.runsSurvived++;
    }

    void SaveVisibleRunBuddies()
    {
        BuddyUnit[] visibleBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in visibleBuddies)
        {
            if (unit == null) continue;

            GobboUnitSaveData source = null;
            if (unit.unitData != null) source = unit.unitData;
            else if (unit.data != null) source = unit.data;
            if (source == null) continue;

            source.EnsureRuntimeDefaults();
            GobboUnitSaveData saved = FindOwnedGobbo(source.uniqueId);
            if (saved == null) continue;
            source.CopyInto(saved);
        }

        RepairRosterState();
    }

    public void SavePlayer(GobboController player)
    {
        if (player == null) return;
        if (gobbo == null) gobbo = new GobboSaveData();

        gobbo.EnsureLeaderIdentity(gobbo.displayName);
        gobbo.level = player.level;
        gobbo.xp = player.xp;
        gobbo.xpToNextLevel = player.xpToNextLevel;
        gobbo.gobboType = player.gobboType;
        gobbo.ageStage = player.ageStage;
        gobbo.visualSetId = player.visualSetId;
        gobbo.pendingEvolution = player.pendingEvolution;
        gobbo.evolutionLevelWaiting = player.evolutionLevelWaiting;
        gobbo.chosenCardIds = player.chosenCardIds != null ? new List<string>(player.chosenCardIds) : new List<string>();

        gobbo.maxHealth = player.maxHealth;
        gobbo.health = player.health;
        gobbo.attack = player.attack;
        gobbo.damage = player.attack;
        gobbo.defense = player.defense;
        gobbo.attackRange = player.attackRange;
        gobbo.attackRadius = player.attackRadius;
        gobbo.attackCooldown = player.attackCooldown;
        gobbo.critChance = player.critChance;
        gobbo.critDamageMultiplier = player.critDamageMultiplier;
        gobbo.knockbackForce = player.knockbackForce;
        gobbo.moveSpeed = player.moveSpeed;
        gobbo.dashSpeed = player.dashSpeed;
        gobbo.dashDuration = player.dashDuration;
        gobbo.dashCooldown = player.dashCooldown;
        gobbo.digPower = player.digPower;
        gobbo.digRadius = player.digRadius;
        gobbo.digRange = player.digRange;
        gobbo.digTickRate = player.digTickRate;
        gobbo.hasSporeMend = player.hasSporeMend;
        gobbo.hasDashBite = player.hasDashBite;
        gobbo.healthControlsSize = player.healthControlsSize;
        gobbo.healthSizeMultiplier = player.healthSizeMultiplier;

        SporeInventory inventory = player.GetComponent<SporeInventory>();
        gobbo.spores = inventory != null ? inventory.spores : player.sporeCount;
        gobbo.EnsureLeaderIdentity(gobbo.displayName);
    }

    public void ApplyToPlayer(GobboController player)
    {
        if (player == null) return;
        if (gobbo == null) gobbo = new GobboSaveData();
        gobbo.EnsureLeaderIdentity(gobbo.displayName);

        player.level = gobbo.level;
        player.xp = gobbo.xp;
        player.xpToNextLevel = gobbo.xpToNextLevel;
        player.gobboType = gobbo.gobboType;
        player.ageStage = gobbo.ageStage;
        player.visualSetId = gobbo.visualSetId;
        player.pendingEvolution = gobbo.pendingEvolution;
        player.evolutionLevelWaiting = gobbo.evolutionLevelWaiting;
        player.chosenCardIds = gobbo.chosenCardIds != null ? new List<string>(gobbo.chosenCardIds) : new List<string>();

        player.maxHealth = gobbo.maxHealth;
        player.health = Mathf.Clamp(gobbo.health, 1, Mathf.Max(1, gobbo.maxHealth));
        player.attack = gobbo.attack;
        player.defense = gobbo.defense;
        player.attackRange = gobbo.attackRange;
        player.attackRadius = gobbo.attackRadius;
        player.attackCooldown = gobbo.attackCooldown;
        player.critChance = gobbo.critChance;
        player.critDamageMultiplier = gobbo.critDamageMultiplier;
        player.knockbackForce = gobbo.knockbackForce;
        player.moveSpeed = gobbo.moveSpeed;
        player.dashSpeed = gobbo.dashSpeed;
        player.dashDuration = gobbo.dashDuration;
        player.dashCooldown = gobbo.dashCooldown;
        player.digPower = gobbo.digPower;
        player.digRadius = gobbo.digRadius;
        player.digRange = gobbo.digRange;
        player.digTickRate = gobbo.digTickRate;
        player.hasSporeMend = gobbo.hasSporeMend;
        player.hasDashBite = gobbo.hasDashBite;
        player.healthControlsSize = gobbo.healthControlsSize;
        player.healthSizeMultiplier = gobbo.healthSizeMultiplier;
        player.sporeCount = gobbo.spores;

        SporeInventory inventory = player.GetComponent<SporeInventory>();
        if (inventory != null)
        {
            inventory.spores = gobbo.spores;
            inventory.UpdateUI();
        }

        player.RefreshAfterSaveLoad();
    }

    public void SaveRoster(BuddyRoster roster)
    {
        if (roster == null) return;
        roster.RepairRosterState();
        maxActiveSquad = roster.maxActiveSquad;
        ownedGobbos.Clear();
        activeSquadIds.Clear();

        foreach (BuddyData buddy in roster.ownedBuddies)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            GobboUnitSaveData copy = buddy.CloneUnit();
            copy.isLeader = false;
            ownedGobbos.Add(copy);
        }

        foreach (BuddyData buddy in roster.activeSquad)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            if (!activeSquadIds.Contains(buddy.uniqueId)) activeSquadIds.Add(buddy.uniqueId);
        }

        RepairRosterState();
    }

    public void ApplyToRoster(BuddyRoster roster)
    {
        if (roster == null) return;
        RepairRosterState();
        roster.maxActiveSquad = maxActiveSquad;
        roster.LoadRoster(ownedBuddies, activeSquadIds);
    }

    public void RegisterXPGained(int amount)
    {
        if (amount <= 0) return;
        EnsureLastRun();
        lastRun.xpGained += amount;
    }

    public void RegisterEnemyKilled()
    {
        EnsureLastRun();
        lastRun.enemiesKilled++;
        gobbo.kills++;
    }

    public void RegisterGobboFound(GobboUnitSaveData unit = null)
    {
        EnsureLastRun();
        if (unit != null)
        {
            unit.EnsureRuntimeDefaults();
            string label = GetGobboLabel(unit);
            if (!lastRun.newBuddyNames.Contains(label)) lastRun.newBuddyNames.Add(label);
        }
        lastRun.buddiesFound = Mathf.Max(lastRun.buddiesFound, lastRun.newBuddyNames.Count);
    }

    // Compatibility wrapper. New code should call RegisterGobboFound(GobboUnitSaveData).
    public void RegisterBuddyFound(BuddyData buddy = null)
    {
        RegisterGobboFound(buddy);
    }

    // Compatibility wrapper. New code should call RegisterGobboDeath(GobboUnitSaveData).
    public void RegisterBuddyDeath(BuddyData buddy)
    {
        if (buddy == null) return;
        RegisterGobboDeath(buddy);
    }

    public void RegisterGobboDeath(GobboUnitSaveData unit)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        if (!deadBuddyIdsThisRun.Contains(unit.uniqueId)) deadBuddyIdsThisRun.Add(unit.uniqueId);
        string label = GetGobboLabel(unit);
        if (!deadBuddyNamesThisRun.Contains(label)) deadBuddyNamesThisRun.Add(label);
        EnsureLastRun();
        if (!lastRun.deadBuddyNames.Contains(label)) lastRun.deadBuddyNames.Add(label);
        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;
    }

    public void RegisterBuddyDeath()
    {
        EnsureLastRun();
        lastRun.buddiesLost++;
    }

    public void RegisterSporesGained(int amount)
    {
        if (amount <= 0) return;
        EnsureLastRun();
        lastRun.sporesGained += amount;
    }

    public void RegisterMushroomsGained(int amount)
    {
        if (amount <= 0) return;
        EnsureLastRun();
        lastRun.mushroomsGained += amount;
    }

    public void RegisterMoneyGained(int amount) => RegisterShiniesGained(amount);

    public void RegisterFoodValueGained(int amount)
    {
        if (amount <= 0) return;
        EnsureLastRun();
        lastRun.foodValueGained += amount;
    }

    public void RegisterShiniesGained(int amount)
    {
        if (amount <= 0) return;
        gobbo.shinies += amount;
        gobbo.money = gobbo.shinies;
        EnsureLastRun();
        lastRun.shiniesGained += amount;
        lastRun.moneyGained += amount;
    }

    public bool TrySpendShinies(int amount)
    {
        if (amount <= 0) return true;
        if (gobbo.shinies < amount) return false;
        gobbo.shinies -= amount;
        gobbo.money = gobbo.shinies;
        return true;
    }

    public void RegisterUpgradeChosen(string upgradeName)
    {
        if (string.IsNullOrWhiteSpace(upgradeName)) return;
        EnsureLastRun();
        if (!lastRun.upgradesChosen.Contains(upgradeName)) lastRun.upgradesChosen.Add(upgradeName);
        if (!gobbo.unlockedUpgrades.Contains(upgradeName)) gobbo.unlockedUpgrades.Add(upgradeName);
    }

    public void RegisterCosmeticUnlocked(string cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId)) return;
        if (!gobbo.unlockedCosmetics.Contains(cosmeticId)) gobbo.unlockedCosmetics.Add(cosmeticId);
    }

    public void RegisterItemUnlocked(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (!gobbo.unlockedItems.Contains(itemId)) gobbo.unlockedItems.Add(itemId);
    }

    void EnsureLastRun()
    {
        if (lastRun == null) lastRun = new RunSummaryData();
        if (lastRun.runNumber <= 0) lastRun.runNumber = currentRunNumber;
        lastRun.newBuddyNames ??= new List<string>();
        lastRun.deadBuddyNames ??= new List<string>();
        lastRun.upgradesChosen ??= new List<string>();
    }

    public void RepairRosterState()
    {
        ownedGobbos ??= new List<GobboUnitSaveData>();
        ownedBuddies ??= new List<BuddyData>();
        activeSquadIds ??= new List<string>();

        // Bring any legacy BuddyData mutations back into the unified roster first.
        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            GobboUnitSaveData existing = FindOwnedGobboRaw(buddy.uniqueId);
            if (existing == null)
            {
                GobboUnitSaveData copy = buddy.CloneUnit();
                copy.isLeader = false;
                ownedGobbos.Add(copy);
            }
            else
            {
                buddy.CopyInto(existing);
                existing.isLeader = false;
            }
        }

        ownedGobbos.RemoveAll(g => g == null);

        Dictionary<string, GobboUnitSaveData> unique = new Dictionary<string, GobboUnitSaveData>();
        List<GobboUnitSaveData> repairedGobbos = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.isLeader = false;
            unit.EnsureRuntimeDefaults();
            if (unique.ContainsKey(unit.uniqueId)) continue;
            unique.Add(unit.uniqueId, unit);
            repairedGobbos.Add(unit);
        }
        ownedGobbos = repairedGobbos;

        HashSet<string> ownedIds = new HashSet<string>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            unit.isInActiveSquad = false;
            ownedIds.Add(unit.uniqueId);
        }

        List<string> repairedActive = new List<string>();
        foreach (string id in activeSquadIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!ownedIds.Contains(id)) continue;
            if (repairedActive.Contains(id)) continue;
            if (repairedActive.Count >= Mathf.Max(1, maxActiveSquad)) break;
            repairedActive.Add(id);
        }
        activeSquadIds = repairedActive;

        foreach (GobboUnitSaveData unit in ownedGobbos)
            unit.isInActiveSquad = activeSquadIds.Contains(unit.uniqueId);

        if (!string.IsNullOrWhiteSpace(markedSuccessorId) && !ownedIds.Contains(markedSuccessorId))
            markedSuccessorId = "";

        RebuildBuddyMirror();
    }

    public void AddBuddy(BuddyData buddy, bool preferActiveSquad = true)
    {
        AddGobbo(buddy, preferActiveSquad);
    }

    public void AddGobbo(GobboUnitSaveData unit, bool preferActiveSquad = true)
    {
        if (unit == null) return;
        unit.isLeader = false;
        unit.EnsureRuntimeDefaults();

        GobboUnitSaveData existing = FindOwnedGobboRaw(unit.uniqueId);
        if (existing == null)
        {
            ownedGobbos.Add(unit.CloneUnit());
        }
        else
        {
            unit.CopyInto(existing);
            existing.isLeader = false;
        }

        RepairRosterState();
        if (preferActiveSquad && activeSquadIds.Count < maxActiveSquad && !activeSquadIds.Contains(unit.uniqueId))
            MoveBuddyToActiveSquad(unit.uniqueId);
    }

    public void RemoveBuddy(string buddyId) => RemoveGobbo(buddyId);

    public void RemoveGobbo(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId)) return;
        ownedGobbos.RemoveAll(g => g == null || g.uniqueId == gobboId);
        ownedBuddies.RemoveAll(b => b == null || b.uniqueId == gobboId);
        activeSquadIds.RemoveAll(id => id == gobboId);
        if (markedSuccessorId == gobboId) markedSuccessorId = "";
        RepairRosterState();
    }

    public void RenameBuddy(string buddyId, string newName)
    {
        GobboUnitSaveData unit = FindOwnedGobbo(buddyId);
        if (unit == null || string.IsNullOrWhiteSpace(newName)) return;
        unit.displayName = newName.Trim();
        RepairRosterState();
    }

    public BuddyData FindBuddy(string buddyId)
    {
        GobboUnitSaveData unit = FindOwnedGobbo(buddyId);
        return unit != null ? unit.AsBuddyData() : null;
    }

    public GobboUnitSaveData FindOwnedGobbo(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId)) return null;
        RepairRosterStateNoMirrorMerge();
        return FindOwnedGobboRaw(gobboId);
    }

    GobboUnitSaveData FindOwnedGobboRaw(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId) || ownedGobbos == null) return null;
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (unit.uniqueId == gobboId) return unit;
        }
        return null;
    }

    void RepairRosterStateNoMirrorMerge()
    {
        ownedGobbos ??= new List<GobboUnitSaveData>();
        activeSquadIds ??= new List<string>();
        ownedGobbos.RemoveAll(g => g == null);
        foreach (GobboUnitSaveData unit in ownedGobbos) unit.EnsureRuntimeDefaults();
    }

    public List<BuddyData> GetActiveSquad()
    {
        RepairRosterState();
        List<BuddyData> result = new List<BuddyData>();
        foreach (GobboUnitSaveData unit in GetActiveSquadUnitsInternal())
        {
            BuddyData buddy = unit.AsBuddyData();
            if (buddy != null) result.Add(buddy);
        }
        return result;
    }

    public List<GobboUnitSaveData> GetActiveSquadUnitsInternal()
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        foreach (string id in activeSquadIds)
        {
            GobboUnitSaveData unit = FindOwnedGobboRaw(id);
            if (unit != null && !unit.isDead) result.Add(unit);
        }
        return result;
    }

    public List<BuddyData> GetReserveBuddies()
    {
        RepairRosterState();
        List<BuddyData> result = new List<BuddyData>();
        foreach (GobboUnitSaveData unit in GetReserveGobboUnitsInternal())
        {
            BuddyData buddy = unit.AsBuddyData();
            if (buddy != null) result.Add(buddy);
        }
        return result;
    }

    public List<GobboUnitSaveData> GetReserveGobboUnitsInternal()
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (unit.isDead) continue;
            if (!activeSquadIds.Contains(unit.uniqueId)) result.Add(unit);
        }
        return result;
    }

    public bool MoveBuddyToActiveSquad(string buddyId)
    {
        RepairRosterState();
        GobboUnitSaveData unit = FindOwnedGobboRaw(buddyId);
        if (unit == null) return false;
        if (activeSquadIds.Contains(buddyId)) return true;
        if (activeSquadIds.Count >= maxActiveSquad) return false;
        activeSquadIds.Add(buddyId);
        unit.isInActiveSquad = true;
        RepairRosterState();
        return true;
    }

    public bool MoveBuddyToReserve(string buddyId)
    {
        RepairRosterState();
        GobboUnitSaveData unit = FindOwnedGobboRaw(buddyId);
        if (unit == null) return false;
        activeSquadIds.Remove(buddyId);
        unit.isInActiveSquad = false;
        RepairRosterState();
        return true;
    }

    public bool SwapBuddies(string activeBuddyId, string reserveBuddyId)
    {
        RepairRosterState();
        GobboUnitSaveData activeUnit = FindOwnedGobboRaw(activeBuddyId);
        GobboUnitSaveData reserveUnit = FindOwnedGobboRaw(reserveBuddyId);
        if (activeUnit == null || reserveUnit == null) return false;
        int index = activeSquadIds.IndexOf(activeBuddyId);
        if (index < 0) return false;
        activeSquadIds[index] = reserveBuddyId;
        activeUnit.isInActiveSquad = false;
        reserveUnit.isInActiveSquad = true;
        RepairRosterState();
        return true;
    }

    public GobboUnitSaveData PullFirstReserveGobbo()
    {
        RepairRosterState();
        List<GobboUnitSaveData> reserve = GetReserveGobboUnitsInternal();
        if (reserve.Count == 0) return null;
        GobboUnitSaveData unit = reserve[0];
        if (unit == null) return null;
        if (MoveBuddyToActiveSquad(unit.uniqueId)) return unit;
        return null;
    }

    // Compatibility wrapper. New code should call PullFirstReserveGobbo().
    public BuddyData PullFirstReserveBuddy()
    {
        GobboUnitSaveData unit = PullFirstReserveGobbo();
        return unit != null ? unit.AsBuddyData() : null;
    }

    public bool HasReserveBuddy() => GetReserveGobboUnitsInternal().Count > 0;

    void BuildRunSummary(GobboSaveData before, List<string> beforeIds, List<GobboUnitSaveData> beforeGobbos, bool survived)
    {
        EnsureLastRun();

        int trackedXpGained = lastRun.xpGained;
        int trackedSporesGained = lastRun.sporesGained;
        int trackedMushroomsGained = lastRun.mushroomsGained;
        int trackedMoneyGained = lastRun.moneyGained;
        int trackedFoodValueGained = lastRun.foodValueGained;
        int trackedShiniesGained = lastRun.shiniesGained;
        int trackedEnemiesKilled = lastRun.enemiesKilled;
        List<string> trackedUpgrades = new List<string>(lastRun.upgradesChosen);

        lastRun.survived = survived;
        lastRun.timeSpent = runStartTime > 0f ? Time.time - runStartTime : 0f;
        lastRun.runNumber = currentRunNumber;
        lastRun.playerLevelStart = before.level;
        lastRun.playerLevelEnd = gobbo.level;
        lastRun.xpStart = before.xp;
        lastRun.xpEnd = gobbo.xp;
        lastRun.xpGained = trackedXpGained > 0 ? trackedXpGained : Mathf.Max(0, gobbo.xp - before.xp);
        lastRun.sporesStart = before.spores;
        lastRun.sporesEnd = gobbo.spores;
        lastRun.sporesGained = trackedSporesGained > 0 ? trackedSporesGained : Mathf.Max(0, gobbo.spores - before.spores);
        lastRun.mushroomsStart = before.mushrooms;
        lastRun.mushroomsEnd = gobbo.mushrooms;
        lastRun.mushroomsGained = trackedMushroomsGained > 0 ? trackedMushroomsGained : Mathf.Max(0, gobbo.mushrooms - before.mushrooms);
        lastRun.moneyStart = before.money;
        lastRun.moneyEnd = gobbo.money;
        lastRun.moneyGained = trackedMoneyGained > 0 ? trackedMoneyGained : Mathf.Max(0, gobbo.money - before.money);
        lastRun.foodValueGained = trackedFoodValueGained;
        lastRun.shiniesStart = before.shinies;
        lastRun.shiniesEnd = gobbo.shinies;
        lastRun.shiniesGained = trackedShiniesGained > 0 ? trackedShiniesGained : Mathf.Max(0, gobbo.shinies - before.shinies);
        lastRun.enemiesKilled = trackedEnemiesKilled;
        lastRun.upgradesChosen = trackedUpgrades;
        lastRun.buddiesStart = beforeIds.Count;
        lastRun.buddiesEnd = ownedGobbos.Count;

        lastRun.newBuddyNames.Clear();
        lastRun.deadBuddyNames.Clear();

        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (!beforeIds.Contains(unit.uniqueId))
                lastRun.newBuddyNames.Add(GetGobboLabel(unit));
        }

        foreach (string deadName in deadBuddyNamesThisRun)
        {
            if (!lastRun.deadBuddyNames.Contains(deadName)) lastRun.deadBuddyNames.Add(deadName);
        }

        foreach (GobboUnitSaveData oldUnit in beforeGobbos)
        {
            if (oldUnit == null) continue;
            bool stillOwned = FindOwnedGobboRaw(oldUnit.uniqueId) != null;
            if (!stillOwned)
            {
                string label = GetGobboLabel(oldUnit);
                if (!lastRun.deadBuddyNames.Contains(label)) lastRun.deadBuddyNames.Add(label);
            }
        }

        lastRun.buddiesFound = lastRun.newBuddyNames.Count;
        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;
    }

    List<string> GetOwnedBuddyIds()
    {
        RepairRosterState();
        List<string> result = new List<string>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            result.Add(unit.uniqueId);
        }
        return result;
    }

    List<GobboUnitSaveData> CloneUnitList(List<GobboUnitSaveData> source)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (source == null) return result;
        foreach (GobboUnitSaveData unit in source)
        {
            if (unit != null) result.Add(unit.CloneUnit());
        }
        return result;
    }

    GobboSaveData CloneGobboSave(GobboSaveData source)
    {
        return source == null ? new GobboSaveData() : source.CloneLeader();
    }

    void RebuildBuddyMirror()
    {
        ownedBuddies ??= new List<BuddyData>();
        ownedBuddies.Clear();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            BuddyData buddy = unit.AsBuddyData();
            if (buddy != null) ownedBuddies.Add(buddy);
        }
    }

    string GetGobboLabel(GobboUnitSaveData unit)
    {
        if (unit == null) return "Unknown Gobbo";
        unit.EnsureRuntimeDefaults();
        string name = unit.displayName;
        string type = unit.gobboType.ToString();
        if (unit is BuddyData buddy)
        {
            name = buddy.buddyName;
            type = buddy.buddyType.ToString();
        }
        return name + " the " + type;
    }
}
