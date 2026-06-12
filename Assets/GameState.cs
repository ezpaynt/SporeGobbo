using System.Collections.Generic;
using UnityEngine;

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

    public List<BuddyRunReport> activeBuddyReports = new List<BuddyRunReport>();
    public List<BuddyRunReport> reserveBuddyReports = new List<BuddyRunReport>();
    public List<string> leveledBuddyNames = new List<string>();
}


[System.Serializable]
public class BuddyRunReport
{
    public string buddyId = "";
    public string displayName = "Gobbo";
    public string role = "";
    public bool wasActive = false;
    public bool survived = true;
    public bool died = false;

    public int levelStart = 1;
    public int levelEnd = 1;
    public int xpStart = 0;
    public int xpEnd = 0;
    public int xpGained = 0;
    public int killsStart = 0;
    public int killsEnd = 0;
    public int killsGained = 0;
    public int nightsStart = 0;
    public int nightsEnd = 0;
    public int nightsGained = 0;
    public int happinessStart = 100;
    public int happinessEnd = 100;

    public bool leveledUp = false;
    public bool readyToGrow = false;
    public string traitLabel = "None";
}

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Run Progress")]
    public int currentRunNumber = 1;

    [Header("Unified Leader Save")]
    public GobboUnitSaveData leader = new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };

    [Header("Unified Roster Save")]
    public int maxActiveSquad = 5;
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();
    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";

    [Header("Camp Save")]
    public int campLevel = 1;
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("Death History Save")]
    public List<DeadBuddyRecord> deathHistory = new List<DeadBuddyRecord>();

    [Header("Last Run")]
    public RunSummaryData lastRun = new RunSummaryData();

    private float runStartTime = 0f;
    private GobboUnitSaveData runStartLeaderSnapshot;
    private List<GobboUnitSaveData> runStartGobboRosterSnapshot = new List<GobboUnitSaveData>();
    private List<string> runStartBuddyIds = new List<string>();
    private List<string> runStartActiveBuddyIds = new List<string>();
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
        if (leader == null) leader = new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };
        leader.isLeader = true;
        leader.isDead = false;
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName);
        leader.EnsureRuntimeDefaults();

        ownedGobbos ??= new List<GobboUnitSaveData>();
        activeSquadIds ??= new List<string>();
        unlockedStations ??= new List<string>();
        decorationsUnlocked ??= new List<string>();
        deathHistory ??= new List<DeadBuddyRecord>();
        if (lastRun == null) lastRun = new RunSummaryData();
        RepairRosterState();
    }

    public GobboUnitSaveData GetLeader()
    {
        EnsureRuntimeDefaults();
        return leader;
    }

    public void SetLeaderName(string newName)
    {
        EnsureRuntimeDefaults();

        if (string.IsNullOrWhiteSpace(newName))
            newName = "Gobbo";

        newName = newName.Trim();

        leader.displayName = newName;
        leader.isLeader = true;
        leader.EnsureIdentity(newName);
        leader.EnsureRuntimeDefaults();
    }

    public void SetLeader(GobboUnitSaveData newLeader)
    {
        if (newLeader == null)
            newLeader = new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };

        leader = newLeader.CloneUnit();
        leader.isLeader = true;
        leader.isDead = false;
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName);
        leader.EnsureRuntimeDefaults();
    }

    public void BeginRunSnapshot()
    {
        EnsureRuntimeDefaults();
        runStartTime = Time.time;
        runStartLeaderSnapshot = leader.CloneUnit();
        runStartGobboRosterSnapshot = CloneUnitList(ownedGobbos);
        runStartBuddyIds.Clear();
        runStartActiveBuddyIds.Clear();
        deadBuddyIdsThisRun.Clear();
        deadBuddyNamesThisRun.Clear();

        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            runStartBuddyIds.Add(unit.uniqueId);
            if (activeSquadIds != null && activeSquadIds.Contains(unit.uniqueId))
                runStartActiveBuddyIds.Add(unit.uniqueId);
        }

        lastRun = new RunSummaryData();
        lastRun.runNumber = currentRunNumber;
        lastRun.playerLevelStart = leader.level;
        lastRun.xpStart = leader.xp;
        lastRun.sporesStart = leader.spores;
        lastRun.mushroomsStart = leader.mushrooms;
        lastRun.moneyStart = leader.money;
        lastRun.shiniesStart = leader.shinies;
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
        GobboUnitSaveData before = runStartLeaderSnapshot != null ? runStartLeaderSnapshot.CloneUnit() : leader.CloneUnit();
        List<string> beforeIds = runStartBuddyIds.Count > 0 ? new List<string>(runStartBuddyIds) : GetOwnedGobboIds();
        List<GobboUnitSaveData> beforeGobbos = runStartGobboRosterSnapshot.Count > 0 ? CloneUnitList(runStartGobboRosterSnapshot) : CloneUnitList(ownedGobbos);

        if (player != null) SavePlayer(player);
        SaveVisibleRunBuddies();
        RepairRosterState();
        BuddyProgression.DistributeEndRunFoodXP(this, lastRun != null ? lastRun.foodValueGained : 0);
        bool survived = player != null && player.gameObject.activeInHierarchy;
        RunSummaryService.BuildRunSummary(this, before, beforeIds, beforeGobbos, runStartActiveBuddyIds, deadBuddyNamesThisRun, runStartTime, survived);
        currentRunNumber = Mathf.Max(1, currentRunNumber + 1);
        leader.runsSurvived++;
    }

    void SaveVisibleRunBuddies()
    {
        BuddyUnit[] visibleBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in visibleBuddies)
        {
            if (unit == null || unit.unitData == null) continue;
            unit.unitData.EnsureRuntimeDefaults();
            GobboUnitSaveData saved = FindOwnedGobbo(unit.unitData.uniqueId);
            if (saved == null) continue;
            unit.unitData.CopyInto(saved);
        }
        RepairRosterState();
    }

    public void SavePlayer(GobboController player)
    {
        if (player == null) return;
        EnsureRuntimeDefaults();
        leader.isLeader = true;
        leader.displayName = string.IsNullOrWhiteSpace(player.displayName) ? leader.displayName : player.displayName.Trim();
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName);
        leader.level = player.level;
        leader.xp = player.xp;
        leader.xpToNextLevel = player.xpToNextLevel;
        leader.gobboType = player.gobboType;
        leader.ageStage = player.ageStage;
        leader.visualSetId = player.visualSetId;
        leader.pendingEvolution = player.pendingEvolution;
        leader.evolutionLevelWaiting = player.evolutionLevelWaiting;
        leader.chosenCardIds = player.chosenCardIds != null ? new List<string>(player.chosenCardIds) : new List<string>();
        leader.maxHealth = player.maxHealth;
        leader.health = player.health;
        leader.attack = player.attack;
        leader.damage = player.attack;
        leader.defense = player.defense;
        leader.attackRange = player.attackRange;
        leader.attackRadius = player.attackRadius;
        leader.attackCooldown = player.attackCooldown;
        leader.critChance = player.critChance;
        leader.critDamageMultiplier = player.critDamageMultiplier;
        leader.knockbackForce = player.knockbackForce;
        leader.moveSpeed = player.moveSpeed;
        leader.dashSpeed = player.dashSpeed;
        leader.dashDuration = player.dashDuration;
        leader.dashCooldown = player.dashCooldown;
        leader.digPower = player.digPower;
        leader.digRadius = player.digRadius;
        leader.digRange = player.digRange;
        leader.digTickRate = player.digTickRate;
        leader.hasSporeMend = player.hasSporeMend;
        leader.hasDashBite = player.hasDashBite;
        leader.healthControlsSize = player.healthControlsSize;
        leader.healthSizeMultiplier = player.healthSizeMultiplier;
        SporeInventory inventory = player.GetComponent<SporeInventory>();
        leader.spores = inventory != null ? inventory.spores : player.sporeCount;
        leader.EnsureRuntimeDefaults();
    }

    public void ApplyToPlayer(GobboController player)
    {
        if (player == null) return;
        EnsureRuntimeDefaults();
        player.displayName = string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName;
        player.level = leader.level;
        player.xp = leader.xp;
        player.xpToNextLevel = leader.xpToNextLevel;
        player.gobboType = leader.gobboType;
        player.ageStage = leader.ageStage;
        player.visualSetId = leader.visualSetId;
        player.pendingEvolution = leader.pendingEvolution;
        player.evolutionLevelWaiting = leader.evolutionLevelWaiting;
        player.chosenCardIds = leader.chosenCardIds != null ? new List<string>(leader.chosenCardIds) : new List<string>();
        player.maxHealth = leader.maxHealth;
        player.health = Mathf.Clamp(leader.health, 1, Mathf.Max(1, leader.maxHealth));
        player.attack = leader.attack;
        player.defense = leader.defense;
        player.attackRange = leader.attackRange;
        player.attackRadius = leader.attackRadius;
        player.attackCooldown = leader.attackCooldown;
        player.critChance = leader.critChance;
        player.critDamageMultiplier = leader.critDamageMultiplier;
        player.knockbackForce = leader.knockbackForce;
        player.moveSpeed = leader.moveSpeed;
        player.dashSpeed = leader.dashSpeed;
        player.dashDuration = leader.dashDuration;
        player.dashCooldown = leader.dashCooldown;
        player.digPower = leader.digPower;
        player.digRadius = leader.digRadius;
        player.digRange = leader.digRange;
        player.digTickRate = leader.digTickRate;
        player.hasSporeMend = leader.hasSporeMend;
        player.hasDashBite = leader.hasDashBite;
        player.healthControlsSize = leader.healthControlsSize;
        player.healthSizeMultiplier = leader.healthSizeMultiplier;
        player.sporeCount = leader.spores;
        SporeInventory inventory = player.GetComponent<SporeInventory>();
        if (inventory != null)
        {
            inventory.spores = leader.spores;
            inventory.UpdateUI();
        }
        player.RefreshAfterSaveLoad();
    }

    public void SaveRoster(BuddyRoster roster)
    {
        if (roster == null) return;
        roster.RepairRosterState();
        maxActiveSquad = roster.maxActiveSquad;
        ownedGobbos = CloneUnitList(roster.ownedGobbos);
        activeSquadIds = new List<string>();
        foreach (GobboUnitSaveData unit in roster.activeSquad)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (!activeSquadIds.Contains(unit.uniqueId)) activeSquadIds.Add(unit.uniqueId);
        }
        RepairRosterState();
    }

    public void ApplyToRoster(BuddyRoster roster)
    {
        if (roster == null) return;
        RepairRosterState();
        roster.maxActiveSquad = maxActiveSquad;
        roster.LoadRoster(ownedGobbos, activeSquadIds);
    }

    public void RegisterXPGained(int amount) { if (amount <= 0) return; EnsureLastRun(); lastRun.xpGained += amount; }
    public void RegisterEnemyKilled() { EnsureLastRun(); lastRun.enemiesKilled++; leader.kills++; }

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

    public void RegisterBuddyFound(GobboUnitSaveData unit = null) => RegisterGobboFound(unit);

    public void RegisterGobboDeath(GobboUnitSaveData unit)
    {
        if (unit == null) return;

        unit.EnsureRuntimeDefaults();
        if (string.IsNullOrWhiteSpace(unit.causeOfDeath))
            unit.causeOfDeath = "Killed in the dirt.";

        if (!deadBuddyIdsThisRun.Contains(unit.uniqueId)) deadBuddyIdsThisRun.Add(unit.uniqueId);
        string label = GetGobboLabel(unit);
        if (!deadBuddyNamesThisRun.Contains(label)) deadBuddyNamesThisRun.Add(label);

        EnsureLastRun();
        if (!lastRun.deadBuddyNames.Contains(label)) lastRun.deadBuddyNames.Add(label);
        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;

        // Permanent memorial data belongs to GameState/save data.
        // The camp-side CampDeathHistoryStore mirrors this list when CampScene opens.
        AddDeathHistoryRecord(BuildDeathRecord(unit, currentRunNumber, unit.causeOfDeath, false));
        SporeSaveManager.SaveCurrentSlotFromGameState();
    }


    public DeadBuddyRecord BuildDeathRecord(GobboUnitSaveData gobbo, int runNumber, string cause, bool wasLeader)
    {
        if (gobbo == null) return null;
        gobbo.EnsureRuntimeDefaults();
        return new DeadBuddyRecord
        {
            gobboId = gobbo.uniqueId,
            displayName = string.IsNullOrWhiteSpace(gobbo.displayName) ? "Unknown Gobbo" : gobbo.displayName,
            gobboType = gobbo.gobboType.ToString(),
            level = Mathf.Max(1, gobbo.level),
            runNumber = Mathf.Max(1, runNumber),
            nightsSurvived = Mathf.Max(0, gobbo.nightsSurvived),
            kills = Mathf.Max(0, gobbo.kills),
            traitId = string.IsNullOrWhiteSpace(gobbo.primaryTraitId) ? "" : gobbo.primaryTraitId,
            cause = string.IsNullOrWhiteSpace(cause) ? "Lost in the dirt." : cause,
            wasLeader = wasLeader,
            memorialSeen = false
        };
    }

    public DeadBuddyRecord AddDeathHistoryRecord(DeadBuddyRecord record)
    {
        if (record == null) return null;
        deathHistory ??= new List<DeadBuddyRecord>();

        foreach (DeadBuddyRecord existing in deathHistory)
        {
            if (existing == null) continue;
            bool sameId = !string.IsNullOrWhiteSpace(record.gobboId) && existing.gobboId == record.gobboId;
            bool sameName = string.IsNullOrWhiteSpace(record.gobboId) && existing.displayName == record.displayName;
            if ((sameId || sameName) && existing.runNumber == record.runNumber && existing.wasLeader == record.wasLeader)
                return existing;
        }

        deathHistory.Add(record);
        return record;
    }

    public void SetDeathHistory(List<DeadBuddyRecord> records)
    {
        deathHistory = records != null ? new List<DeadBuddyRecord>(records) : new List<DeadBuddyRecord>();
    }

    public List<DeadBuddyRecord> GetDeathHistoryCopy()
    {
        deathHistory ??= new List<DeadBuddyRecord>();
        return new List<DeadBuddyRecord>(deathHistory);
    }

    public void RegisterBuddyDeath(GobboUnitSaveData unit) => RegisterGobboDeath(unit);
    public void RegisterBuddyDeath() { EnsureLastRun(); lastRun.buddiesLost++; }

    public void RegisterSporesGained(int amount) { if (amount <= 0) return; EnsureLastRun(); lastRun.sporesGained += amount; }
    public void RegisterMushroomsGained(int amount) { if (amount <= 0) return; EnsureLastRun(); lastRun.mushroomsGained += amount; }
    public void RegisterMoneyGained(int amount) => RegisterShiniesGained(amount);
    public void RegisterFoodValueGained(int amount) { if (amount <= 0) return; EnsureLastRun(); lastRun.foodValueGained += amount; }

    public void RegisterShiniesGained(int amount)
    {
        if (amount <= 0) return;
        leader.shinies += amount;
        leader.money = leader.shinies;
        EnsureLastRun();
        lastRun.shiniesGained += amount;
        lastRun.moneyGained += amount;
    }

    public bool TrySpendShinies(int amount)
    {
        if (amount <= 0) return true;
        if (leader.shinies < amount) return false;
        leader.shinies -= amount;
        leader.money = leader.shinies;
        return true;
    }

    public void RegisterUpgradeChosen(string upgradeName)
    {
        if (string.IsNullOrWhiteSpace(upgradeName)) return;
        EnsureLastRun();
        if (!lastRun.upgradesChosen.Contains(upgradeName)) lastRun.upgradesChosen.Add(upgradeName);
        if (!leader.unlockedUpgrades.Contains(upgradeName)) leader.unlockedUpgrades.Add(upgradeName);
    }

    public void RegisterCosmeticUnlocked(string cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId)) return;
        if (!leader.unlockedCosmetics.Contains(cosmeticId)) leader.unlockedCosmetics.Add(cosmeticId);
    }

    public void RegisterItemUnlocked(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (!leader.unlockedItems.Contains(itemId)) leader.unlockedItems.Add(itemId);
    }

    void EnsureLastRun()
    {
        if (lastRun == null) lastRun = new RunSummaryData();
        if (lastRun.runNumber <= 0) lastRun.runNumber = currentRunNumber;
        lastRun.newBuddyNames ??= new List<string>();
        lastRun.deadBuddyNames ??= new List<string>();
        lastRun.upgradesChosen ??= new List<string>();
        lastRun.activeBuddyReports ??= new List<BuddyRunReport>();
        lastRun.reserveBuddyReports ??= new List<BuddyRunReport>();
        lastRun.leveledBuddyNames ??= new List<string>();
    }

    public void RepairRosterState()
    {
        ownedGobbos ??= new List<GobboUnitSaveData>();
        activeSquadIds ??= new List<string>();
        ownedGobbos.RemoveAll(g => g == null);

        Dictionary<string, GobboUnitSaveData> unique = new Dictionary<string, GobboUnitSaveData>();
        List<GobboUnitSaveData> repaired = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.isLeader = false;
            unit.EnsureRuntimeDefaults();
            if (unique.ContainsKey(unit.uniqueId)) continue;
            unique.Add(unit.uniqueId, unit);
            repaired.Add(unit);
        }
        ownedGobbos = repaired;

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
    }

    public void AddGobbo(GobboUnitSaveData unit, bool preferActiveSquad = true)
    {
        if (unit == null) return;
        unit.isLeader = false;
        unit.EnsureRuntimeDefaults();
        GobboUnitSaveData existing = FindOwnedGobboRaw(unit.uniqueId);
        if (existing == null) ownedGobbos.Add(unit.CloneUnit());
        else unit.CopyInto(existing);
        RepairRosterState();
        if (preferActiveSquad && activeSquadIds.Count < maxActiveSquad && !activeSquadIds.Contains(unit.uniqueId))
            MoveBuddyToActiveSquad(unit.uniqueId);
    }

    public void AddBuddy(GobboUnitSaveData unit, bool preferActiveSquad = true) => AddGobbo(unit, preferActiveSquad);

    public void RemoveGobbo(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId)) return;
        ownedGobbos.RemoveAll(g => g == null || g.uniqueId == gobboId);
        activeSquadIds.RemoveAll(id => id == gobboId);
        if (markedSuccessorId == gobboId) markedSuccessorId = "";
        RepairRosterState();
    }

    public void RemoveBuddy(string buddyId) => RemoveGobbo(buddyId);

    public void RenameBuddy(string buddyId, string newName)
    {
        GobboUnitSaveData unit = FindOwnedGobbo(buddyId);
        if (unit == null || string.IsNullOrWhiteSpace(newName)) return;
        unit.displayName = newName.Trim();
        RepairRosterState();
    }

    public GobboUnitSaveData FindOwnedGobbo(string gobboId)
    {
        if (string.IsNullOrWhiteSpace(gobboId)) return null;
        RepairRosterStateNoFullRepair();
        return FindOwnedGobboRaw(gobboId);
    }

    public GobboUnitSaveData FindBuddy(string gobboId) => FindOwnedGobbo(gobboId);

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

    void RepairRosterStateNoFullRepair()
    {
        ownedGobbos ??= new List<GobboUnitSaveData>();
        activeSquadIds ??= new List<string>();
        ownedGobbos.RemoveAll(g => g == null);
        foreach (GobboUnitSaveData unit in ownedGobbos) unit.EnsureRuntimeDefaults();
    }

    public List<GobboUnitSaveData> GetActiveSquadUnits()
    {
        RepairRosterState();
        return GetActiveSquadUnitsInternal();
    }

    public List<GobboUnitSaveData> GetActiveSquad() => GetActiveSquadUnits();

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

    public List<GobboUnitSaveData> GetReserveGobboUnits()
    {
        RepairRosterState();
        return GetReserveGobboUnitsInternal();
    }

    public List<GobboUnitSaveData> GetReserveBuddies() => GetReserveGobboUnits();

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
        List<GobboUnitSaveData> reserve = GetReserveGobboUnits();
        if (reserve.Count == 0) return null;
        GobboUnitSaveData unit = reserve[0];
        return MoveBuddyToActiveSquad(unit.uniqueId) ? unit : null;
    }

    public GobboUnitSaveData PullFirstReserveBuddy() => PullFirstReserveGobbo();
    public bool HasReserveBuddy() => GetReserveGobboUnitsInternal().Count > 0;

    List<string> GetOwnedGobboIds()
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
            if (unit != null) result.Add(unit.CloneUnit());
        return result;
    }

    string GetGobboLabel(GobboUnitSaveData unit)
    {
        if (unit == null) return "Unknown Gobbo";
        unit.EnsureRuntimeDefaults();
        return unit.displayName + " the " + unit.gobboType;
    }
}
