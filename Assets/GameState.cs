using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GobboSaveData
{
    [Header("Core")]
    public int level = 1;
    public int xp = 0;
    public int xpToNextLevel = 10;
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public string visualSetId = "baby";
    public bool pendingEvolution = false;
    public int evolutionLevelWaiting = 0;

    [Header("Combat Stats")]
    public int maxHealth = 100;
    public int health = 100;
    public int attack = 5;
    public int defense = 2;

    public float attackRange = 0.85f;
    public float attackRadius = 0.45f;
    public float attackCooldown = 0.7f;

    public float critChance = 0f;
    public float critDamageMultiplier = 1.5f;
    public float knockbackForce = 6f;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float dashSpeed = 12f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.7f;

    [Header("Digging")]
    public int digPower = 1;
    public float digRadius = 0.65f;
    public float digRange = 0.8f;
    public float digTickRate = 0.05f;

    [Header("Abilities")]
    public bool hasSporeMend = false;
    public bool hasDashBite = false;
    public bool healthControlsSize = false;
    public float healthSizeMultiplier = 0f;

    [Header("Resources")]
    public int spores = 0;
    public int mushrooms = 0;
    public int money = 0; // legacy name; use shinies going forward
    public int shinies = 0;

    [Header("Unlocks")]
    public List<string> unlockedUpgrades = new List<string>();
    public List<string> unlockedAbilities = new List<string>();
    public List<string> unlockedCosmetics = new List<string>();
    public List<string> equippedCosmetics = new List<string>();
    public List<string> unlockedItems = new List<string>();
    public List<string> chosenCardIds = new List<string>();
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

    [Header("Player Save")]
    public GobboSaveData gobbo = new GobboSaveData();

    [Header("Roster Save")]
    public int maxActiveSquad = 5;
    public List<BuddyData> ownedBuddies = new List<BuddyData>();
    public List<string> activeSquadIds = new List<string>();

    [Header("Camp Save")]
    public int campLevel = 1;
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("Last Run")]
    public RunSummaryData lastRun = new RunSummaryData();

    private float runStartTime = 0f;
    private GobboSaveData runStartGobboSnapshot;
    private List<BuddyData> runStartBuddySnapshot = new List<BuddyData>();
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
    }

    public void BeginRunSnapshot()
    {
        runStartTime = Time.time;
        runStartGobboSnapshot = CloneGobboSave(gobbo);

        runStartBuddySnapshot.Clear();
        runStartBuddyIds.Clear();
        deadBuddyIdsThisRun.Clear();
        deadBuddyNamesThisRun.Clear();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            BuddyData copy = buddy.Clone();
            runStartBuddySnapshot.Add(copy);
            runStartBuddyIds.Add(copy.uniqueId);
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


    public bool HasRunSnapshot()
    {
        return runStartGobboSnapshot != null;
    }

    public void CaptureCurrentRunStartState()
    {
        GobboController player = Object.FindAnyObjectByType<GobboController>();

        if (player != null)
            SavePlayer(player);

        // GameState owns the roster. Do not pull a scene BuddyRoster here, because
        // an empty SampleScene roster can wipe the camp-selected squad.
        RepairRosterState();
        BeginRunSnapshot();
    }

    public void SaveFromRun()
    {
        GobboController player = Object.FindAnyObjectByType<GobboController>();
        BuddyRoster sceneRoster = Object.FindAnyObjectByType<BuddyRoster>();

        GobboSaveData before = runStartGobboSnapshot != null
            ? runStartGobboSnapshot
            : CloneGobboSave(gobbo);

        List<string> beforeIds = runStartBuddyIds.Count > 0
            ? new List<string>(runStartBuddyIds)
            : GetOwnedBuddyIds();

        List<BuddyData> beforeBuddies = runStartBuddySnapshot.Count > 0
            ? new List<BuddyData>(runStartBuddySnapshot)
            : CloneBuddyList(ownedBuddies);

        if (player != null)
            SavePlayer(player);

        // Merge scene-created buddies, but never let an empty scene roster replace
        // the saved camp roster. This keeps camp squad selection stable.
        MergeRosterFromScene(sceneRoster);
        RepairRosterState();

        BuddyProgression.DistributeEndRunFoodXP(this, lastRun != null ? lastRun.foodValueGained : 0);

        bool survived = player != null && player.gameObject.activeInHierarchy;
        BuildRunSummary(before, beforeIds, beforeBuddies, survived);

        currentRunNumber = Mathf.Max(1, currentRunNumber + 1);
    }

    public void SavePlayer(GobboController player)
    {
        if (player == null)
            return;

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

        if (inventory != null)
            gobbo.spores = inventory.spores;
        else
            gobbo.spores = player.sporeCount;
    }

    public void ApplyToPlayer(GobboController player)
    {
        if (player == null)
            return;

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
        player.health = Mathf.Clamp(gobbo.health, 1, gobbo.maxHealth);
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
        if (roster == null)
            return;

        roster.RepairRosterState();

        maxActiveSquad = Mathf.Max(1, roster.maxActiveSquad);
        ownedBuddies.Clear();
        activeSquadIds.Clear();

        foreach (BuddyData buddy in roster.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            ownedBuddies.Add(buddy.Clone());
        }

        foreach (BuddyData buddy in roster.activeSquad)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();

            if (!activeSquadIds.Contains(buddy.uniqueId))
                activeSquadIds.Add(buddy.uniqueId);
        }

        RepairRosterState();
    }

    public void ApplyToRoster(BuddyRoster roster)
    {
        if (roster == null)
            return;

        roster.maxActiveSquad = maxActiveSquad;
        roster.LoadRoster(ownedBuddies, activeSquadIds);
    }

    public void RegisterXPGained(int amount)
    {
        if (amount <= 0)
            return;

        EnsureLastRun();
        lastRun.xpGained += amount;
    }

    public void RegisterEnemyKilled()
    {
        EnsureLastRun();
        lastRun.enemiesKilled++;
    }

    public void RegisterBuddyFound(BuddyData buddy = null)
    {
        EnsureLastRun();

        if (buddy != null)
        {
            buddy.EnsureId();
            string label = buddy.buddyName + " the " + buddy.buddyType;

            if (!lastRun.newBuddyNames.Contains(label))
                lastRun.newBuddyNames.Add(label);
        }

        lastRun.buddiesFound = Mathf.Max(lastRun.buddiesFound, lastRun.newBuddyNames.Count);
    }

    public void RegisterBuddyDeath(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();

        if (!deadBuddyIdsThisRun.Contains(buddy.uniqueId))
            deadBuddyIdsThisRun.Add(buddy.uniqueId);

        string label = buddy.buddyName + " the " + buddy.buddyType;

        if (!deadBuddyNamesThisRun.Contains(label))
            deadBuddyNamesThisRun.Add(label);

        EnsureLastRun();

        if (!lastRun.deadBuddyNames.Contains(label))
            lastRun.deadBuddyNames.Add(label);

        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;
    }

    public void RegisterBuddyDeath()
    {
        EnsureLastRun();
        lastRun.buddiesLost++;
    }

    public void RegisterSporesGained(int amount)
    {
        if (amount <= 0)
            return;

        EnsureLastRun();
        lastRun.sporesGained += amount;
    }

    public void RegisterMushroomsGained(int amount)
    {
        if (amount <= 0)
            return;

        EnsureLastRun();
        lastRun.mushroomsGained += amount;
    }

    public void RegisterMoneyGained(int amount)
    {
        RegisterShiniesGained(amount);
    }


    public void RegisterFoodValueGained(int amount)
    {
        if (amount <= 0)
            return;

        EnsureLastRun();
        lastRun.foodValueGained += amount;
    }

    public void RegisterShiniesGained(int amount)
    {
        if (amount <= 0)
            return;

        gobbo.shinies += amount;
        gobbo.money = gobbo.shinies;
        EnsureLastRun();
        lastRun.shiniesGained += amount;
        lastRun.moneyGained += amount;
    }

    public bool TrySpendShinies(int amount)
    {
        if (amount <= 0)
            return true;

        if (gobbo.shinies < amount)
            return false;

        gobbo.shinies -= amount;
        gobbo.money = gobbo.shinies;
        return true;
    }

    public void RegisterUpgradeChosen(string upgradeName)
    {
        if (string.IsNullOrWhiteSpace(upgradeName))
            return;

        EnsureLastRun();

        if (!lastRun.upgradesChosen.Contains(upgradeName))
            lastRun.upgradesChosen.Add(upgradeName);

        if (!gobbo.unlockedUpgrades.Contains(upgradeName))
            gobbo.unlockedUpgrades.Add(upgradeName);
    }

    public void RegisterCosmeticUnlocked(string cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
            return;

        if (!gobbo.unlockedCosmetics.Contains(cosmeticId))
            gobbo.unlockedCosmetics.Add(cosmeticId);
    }

    public void RegisterItemUnlocked(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!gobbo.unlockedItems.Contains(itemId))
            gobbo.unlockedItems.Add(itemId);
    }

    void EnsureLastRun()
    {
        if (lastRun == null)
            lastRun = new RunSummaryData();

        if (lastRun.runNumber <= 0)
            lastRun.runNumber = currentRunNumber;
    }

    public void RenameBuddy(string buddyId, string newName)
    {
        BuddyData buddy = FindBuddy(buddyId);

        if (buddy == null || string.IsNullOrWhiteSpace(newName))
            return;

        buddy.buddyName = newName.Trim();
    }

    public void RepairRosterState()
    {
        if (ownedBuddies == null)
            ownedBuddies = new List<BuddyData>();

        if (activeSquadIds == null)
            activeSquadIds = new List<string>();

        maxActiveSquad = Mathf.Max(1, maxActiveSquad);
        ownedBuddies.RemoveAll(b => b == null);

        foreach (BuddyData buddy in ownedBuddies)
        {
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            buddy.isInActiveSquad = false;
        }

        HashSet<string> seen = new HashSet<string>();
        List<string> cleanedActiveIds = new List<string>();

        foreach (string id in activeSquadIds)
        {
            if (string.IsNullOrWhiteSpace(id) || seen.Contains(id))
                continue;

            BuddyData buddy = FindBuddy(id);
            if (buddy == null)
                continue;

            if (cleanedActiveIds.Count >= maxActiveSquad)
                break;

            cleanedActiveIds.Add(id);
            seen.Add(id);
            buddy.isInActiveSquad = true;
        }

        activeSquadIds = cleanedActiveIds;
    }

    public BuddyData FindBuddy(string buddyId)
    {
        if (string.IsNullOrWhiteSpace(buddyId) || ownedBuddies == null)
            return null;

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            if (buddy.uniqueId == buddyId)
                return buddy;
        }

        return null;
    }

    public void AddOrUpdateBuddy(BuddyData source, bool addToActiveIfRequested)
    {
        if (source == null)
            return;

        source.EnsureId();
        source.EnsureRuntimeDefaults();

        BuddyData existing = FindBuddy(source.uniqueId);
        if (existing == null)
        {
            ownedBuddies.Add(source.Clone());
        }
        else
        {
            CopyBuddyRuntimeData(source, existing);
        }

        if (addToActiveIfRequested)
            MoveBuddyToActiveSquad(source.uniqueId);
        else
            RepairRosterState();
    }

    public void MergeRosterFromScene(BuddyRoster roster)
    {
        if (roster == null || roster.ownedBuddies == null || roster.ownedBuddies.Count == 0)
            return;

        roster.RepairRosterState();
        maxActiveSquad = Mathf.Max(maxActiveSquad, roster.maxActiveSquad);

        foreach (BuddyData buddy in roster.ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            bool wantsActive = buddy.isInActiveSquad || roster.activeSquad.Contains(buddy);
            AddOrUpdateBuddy(buddy, wantsActive);
        }

        RepairRosterState();
    }

    void CopyBuddyRuntimeData(BuddyData source, BuddyData target)
    {
        if (source == null || target == null)
            return;

        bool wasActive = target.isInActiveSquad;
        string id = target.uniqueId;

        BuddyData copy = source.Clone();
        target.uniqueId = id;
        target.buddyName = copy.buddyName;
        target.buddyType = copy.buddyType;
        target.ageStage = copy.ageStage;
        target.level = copy.level;
        target.xp = copy.xp;
        target.xpToNextLevel = copy.xpToNextLevel;
        target.campLevel = copy.campLevel;
        target.pendingEvolution = copy.pendingEvolution;
        target.evolutionLevelWaiting = copy.evolutionLevelWaiting;
        target.runsWaitingForEvolution = copy.runsWaitingForEvolution;
        target.neglectedElder = copy.neglectedElder;
        target.happiness = copy.happiness;
        target.loyalty = copy.loyalty;
        target.maxHealth = copy.maxHealth;
        target.health = copy.health;
        target.damage = copy.damage;
        target.defense = copy.defense;
        target.moveSpeed = copy.moveSpeed;
        target.attackCooldown = copy.attackCooldown;
        target.onlyFightsAfterHit = copy.onlyFightsAfterHit;
        target.collectsFood = copy.collectsFood;
        target.hasBeenHit = copy.hasBeenHit;
        target.survivedLastRun = copy.survivedLastRun;
        target.bodyColor = copy.bodyColor;
        target.visualSetId = copy.visualSetId;
        target.portraitId = copy.portraitId;
        target.equippedHat = copy.equippedHat;
        target.chosenCardIds = copy.chosenCardIds;
        target.mutationIds = copy.mutationIds;
        target.upgradeIds = copy.upgradeIds;
        target.equippedItem = copy.equippedItem;
        target.isInActiveSquad = wasActive;
    }

    public void RemoveBuddy(string buddyId)
    {
        if (string.IsNullOrWhiteSpace(buddyId))
            return;

        if (ownedBuddies != null)
            ownedBuddies.RemoveAll(b => b == null || b.uniqueId == buddyId);

        if (activeSquadIds != null)
            activeSquadIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || id == buddyId);

        RepairRosterState();
    }

    public List<BuddyData> GetActiveSquad()
    {
        RepairRosterState();
        List<BuddyData> result = new List<BuddyData>();

        foreach (string id in activeSquadIds)
        {
            BuddyData buddy = FindBuddy(id);
            if (buddy != null)
                result.Add(buddy);
        }

        return result;
    }

    public List<BuddyData> GetReserveBuddies()
    {
        RepairRosterState();
        List<BuddyData> result = new List<BuddyData>();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            if (!activeSquadIds.Contains(buddy.uniqueId))
                result.Add(buddy);
        }

        return result;
    }

    public bool MoveBuddyToActiveSquad(string buddyId)
    {
        RepairRosterState();
        BuddyData buddy = FindBuddy(buddyId);

        if (buddy == null)
            return false;

        if (activeSquadIds.Contains(buddyId))
            return true;

        if (activeSquadIds.Count >= maxActiveSquad)
            return false;

        activeSquadIds.Add(buddyId);
        buddy.isInActiveSquad = true;
        return true;
    }

    public bool MoveBuddyToReserve(string buddyId)
    {
        RepairRosterState();
        BuddyData buddy = FindBuddy(buddyId);

        if (buddy == null)
            return false;

        activeSquadIds.Remove(buddyId);
        buddy.isInActiveSquad = false;
        return true;
    }

    public bool SwapBuddies(string activeBuddyId, string reserveBuddyId)
    {
        RepairRosterState();
        BuddyData activeBuddy = FindBuddy(activeBuddyId);
        BuddyData reserveBuddy = FindBuddy(reserveBuddyId);

        if (activeBuddy == null || reserveBuddy == null)
            return false;

        int index = activeSquadIds.IndexOf(activeBuddyId);
        if (index < 0)
            return false;

        activeSquadIds[index] = reserveBuddyId;
        activeBuddy.isInActiveSquad = false;
        reserveBuddy.isInActiveSquad = true;
        RepairRosterState();
        return true;
    }

    public BuddyData PullFirstReserveBuddy()
    {
        List<BuddyData> reserve = GetReserveBuddies();

        if (reserve.Count == 0)
            return null;

        BuddyData buddy = reserve[0];
        return MoveBuddyToActiveSquad(buddy.uniqueId) ? buddy : null;
    }

    public bool HasReserveBuddy()
    {
        return GetReserveBuddies().Count > 0;
    }

    void BuildRunSummary(GobboSaveData before, List<string> beforeIds, List<BuddyData> beforeBuddies, bool survived)
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
        lastRun.buddiesEnd = ownedBuddies.Count;

        lastRun.newBuddyNames.Clear();
        lastRun.deadBuddyNames.Clear();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();

            if (!beforeIds.Contains(buddy.uniqueId))
                lastRun.newBuddyNames.Add(buddy.buddyName + " the " + buddy.buddyType);
        }

        foreach (string deadName in deadBuddyNamesThisRun)
        {
            if (!lastRun.deadBuddyNames.Contains(deadName))
                lastRun.deadBuddyNames.Add(deadName);
        }

        foreach (BuddyData oldBuddy in beforeBuddies)
        {
            if (oldBuddy == null)
                continue;

            bool stillOwned = false;

            foreach (BuddyData current in ownedBuddies)
            {
                if (current != null && current.uniqueId == oldBuddy.uniqueId)
                {
                    stillOwned = true;
                    break;
                }
            }

            if (!stillOwned)
            {
                string label = oldBuddy.buddyName + " the " + oldBuddy.buddyType;

                if (!lastRun.deadBuddyNames.Contains(label))
                    lastRun.deadBuddyNames.Add(label);
            }
        }

        lastRun.buddiesFound = lastRun.newBuddyNames.Count;
        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;
    }

    List<string> GetOwnedBuddyIds()
    {
        List<string> result = new List<string>();

        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null)
                continue;

            buddy.EnsureId();
            result.Add(buddy.uniqueId);
        }

        return result;
    }

    List<BuddyData> CloneBuddyList(List<BuddyData> source)
    {
        List<BuddyData> result = new List<BuddyData>();

        if (source == null)
            return result;

        foreach (BuddyData buddy in source)
        {
            if (buddy != null)
                result.Add(buddy.Clone());
        }

        return result;
    }

    GobboSaveData CloneGobboSave(GobboSaveData source)
    {
        GobboSaveData copy = new GobboSaveData();

        if (source == null)
            return copy;

        copy.level = source.level;
        copy.xp = source.xp;
        copy.xpToNextLevel = source.xpToNextLevel;
        copy.gobboType = source.gobboType;
        copy.ageStage = source.ageStage;
        copy.visualSetId = source.visualSetId;
        copy.pendingEvolution = source.pendingEvolution;
        copy.evolutionLevelWaiting = source.evolutionLevelWaiting;

        copy.maxHealth = source.maxHealth;
        copy.health = source.health;
        copy.attack = source.attack;
        copy.defense = source.defense;

        copy.attackRange = source.attackRange;
        copy.attackRadius = source.attackRadius;
        copy.attackCooldown = source.attackCooldown;
        copy.critChance = source.critChance;
        copy.critDamageMultiplier = source.critDamageMultiplier;
        copy.knockbackForce = source.knockbackForce;

        copy.moveSpeed = source.moveSpeed;
        copy.dashSpeed = source.dashSpeed;
        copy.dashDuration = source.dashDuration;
        copy.dashCooldown = source.dashCooldown;

        copy.digPower = source.digPower;
        copy.digRadius = source.digRadius;
        copy.digRange = source.digRange;
        copy.digTickRate = source.digTickRate;

        copy.hasSporeMend = source.hasSporeMend;
        copy.hasDashBite = source.hasDashBite;
        copy.healthControlsSize = source.healthControlsSize;
        copy.healthSizeMultiplier = source.healthSizeMultiplier;

        copy.spores = source.spores;
        copy.mushrooms = source.mushrooms;
        copy.money = source.money;
        copy.shinies = source.shinies;

        copy.unlockedUpgrades = new List<string>(source.unlockedUpgrades);
        copy.unlockedAbilities = new List<string>(source.unlockedAbilities);
        copy.unlockedCosmetics = new List<string>(source.unlockedCosmetics);
        copy.equippedCosmetics = new List<string>(source.equippedCosmetics);
        copy.unlockedItems = new List<string>(source.unlockedItems);
        copy.chosenCardIds = source.chosenCardIds != null ? new List<string>(source.chosenCardIds) : new List<string>();

        return copy;
    }
}
