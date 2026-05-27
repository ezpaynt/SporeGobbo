using System.Collections.Generic;
using UnityEngine;

public class CampTestRosterSeeder : MonoBehaviour
{
    [Header("When to Seed")]
    public bool seedOnAwake = true;
    public bool onlyIfRosterEmpty = true;
    public bool seedOnlyInCampScene = true;

    [Header("Test Roster")]
    public int activeBuddies = 5;
    public int reserveBuddies = 2;
    public int maxActiveSquad = 5;
    public bool includeHurtBuddies = true;
    public bool includePendingEvolutionBuddy = false;
    public bool markFirstActiveAsSuccessorIfNone = true;

    [Header("Test Player")]
    public bool hurtPlayerForCampTest = false;
    public int testPlayerHealth = 37;
    public int testSpores = 4;
    public int testMushrooms = 9;
    public int testShinies = 3;

    void Awake()
    {
        if (seedOnAwake)
            SeedIfNeeded();
    }

    [ContextMenu("Seed Test Camp Roster")]
    public void SeedIfNeeded()
    {
        if (seedOnlyInCampScene && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CampScene")
            return;

        GameState state = EnsureGameState();
        if (state == null)
            return;

        if (onlyIfRosterEmpty && state.ownedBuddies != null && state.ownedBuddies.Count > 0)
        {
            Debug.Log("CampTestRosterSeeder skipped because GameState already has buddies: " + state.ownedBuddies.Count);
            EnsureActiveLimit(state);
            EnsureSuccessorForTest(state);
            return;
        }

        Seed(state);
    }

    [ContextMenu("Force Reseed Test Camp Roster")]
    public void ForceReseed()
    {
        GameState state = EnsureGameState();
        if (state != null)
            Seed(state);
    }

    void Seed(GameState state)
    {
        state.ownedBuddies = new List<BuddyData>();
        state.activeSquadIds = new List<string>();
        state.maxActiveSquad = Mathf.Max(1, maxActiveSquad);

        if (state.gobbo == null)
            state.gobbo = new GobboSaveData();

        state.gobbo.level = Mathf.Max(1, state.gobbo.level);
        state.gobbo.maxHealth = Mathf.Max(50, state.gobbo.maxHealth);
        state.gobbo.health = hurtPlayerForCampTest ? Mathf.Clamp(testPlayerHealth, 1, state.gobbo.maxHealth) : state.gobbo.maxHealth;
        state.gobbo.spores = testSpores;
        state.gobbo.mushrooms = testMushrooms;
        state.gobbo.shinies = testShinies;
        state.gobbo.money = testShinies;

        BuddyType[] typeCycle =
        {
            BuddyType.Fast,
            BuddyType.Fat,
            BuddyType.Scavenger,
            BuddyType.Tank,
            BuddyType.Strong,
            BuddyType.Fungal,
            BuddyType.Thrower,
            BuddyType.Baby
        };

        string[] names = { "Pip", "Mug", "Bunk", "Snorp", "Wim", "Grot", "Nub", "Boil", "Lump" };
        int activeCount = Mathf.Max(0, activeBuddies);
        int reserveCount = Mathf.Max(0, reserveBuddies);
        int total = activeCount + reserveCount;

        for (int i = 0; i < total; i++)
        {
            BuddyData buddy = MakeBuddy(names[i % names.Length], typeCycle[i % typeCycle.Length], i);
            buddy.isInActiveSquad = i < activeCount;

            if (includeHurtBuddies && i % 2 == 0)
                buddy.health = Mathf.Max(1, buddy.maxHealth / 2);

            if (includePendingEvolutionBuddy && i == 0)
            {
                buddy.buddyType = BuddyType.Baby;
                buddy.ageStage = GobboAgeStage.Baby;
                buddy.level = 2;
                buddy.pendingEvolution = true;
                buddy.evolutionLevelWaiting = 2;
                buddy.visualSetId = "baby";
            }

            state.ownedBuddies.Add(buddy);

            if (buddy.isInActiveSquad)
                state.activeSquadIds.Add(buddy.uniqueId);
        }

        state.lastRun = new RunSummaryData
        {
            survived = true,
            runNumber = Mathf.Max(1, state.currentRunNumber),
            playerLevelStart = Mathf.Max(1, state.gobbo.level),
            playerLevelEnd = state.gobbo.level,
            xpGained = 0,
            foodValueGained = 0,
            sporesGained = 0,
            mushroomsGained = 0,
            shiniesGained = 0,
            enemiesKilled = 0,
            buddiesStart = total,
            buddiesEnd = total
        };

        EnsureSuccessorForTest(state);

        Debug.Log("Seeded test camp roster: " + activeCount + " active, " + reserveCount + " reserve. GameState now has " + state.ownedBuddies.Count + " buddies.");
    }

    void EnsureActiveLimit(GameState state)
    {
        if (state == null)
            return;

        state.maxActiveSquad = Mathf.Max(state.maxActiveSquad, maxActiveSquad);
    }

    void EnsureSuccessorForTest(GameState state)
    {
        if (!markFirstActiveAsSuccessorIfNone || state == null || state.ownedBuddies == null || state.ownedBuddies.Count == 0)
            return;

        CampSuccessorPreferenceStore preference = CampSuccessorPreferenceStore.GetOrCreate();
        preference.ValidateAgainstRoster();

        if (!string.IsNullOrWhiteSpace(preference.GetMarkedSuccessorId()))
            return;

        BuddyData chosen = null;
        foreach (BuddyData buddy in state.ownedBuddies)
        {
            if (buddy != null && buddy.isInActiveSquad)
            {
                chosen = buddy;
                break;
            }
        }

        if (chosen == null)
            chosen = state.ownedBuddies[0];

        if (chosen != null)
        {
            chosen.EnsureId();
            preference.SetMarkedSuccessor(chosen.uniqueId);
            Debug.Log("CampTestRosterSeeder marked test successor: " + chosen.buddyName);
        }
    }

    BuddyData MakeBuddy(string buddyName, BuddyType type, int index)
    {
        BuddyData buddy = new BuddyData();
        buddy.EnsureId();
        buddy.buddyName = buddyName;
        buddy.buddyType = type;
        buddy.ageStage = type == BuddyType.Baby ? GobboAgeStage.Baby : GobboAgeStage.Young;
        buddy.level = type == BuddyType.Baby ? 1 : 3 + index;
        buddy.campLevel = buddy.level;
        buddy.xp = 0;
        buddy.xpToNextLevel = 5;
        buddy.maxHealth = type == BuddyType.Fat || type == BuddyType.Tank ? 12 : 8;
        buddy.health = buddy.maxHealth;
        buddy.damage = type == BuddyType.Strong ? 2 : 1;
        buddy.defense = type == BuddyType.Tank ? 2 : 0;
        buddy.moveSpeed = type == BuddyType.Fast ? 5.2f : 3.4f;
        buddy.attackCooldown = 0.9f;
        buddy.collectsFood = type == BuddyType.Scavenger;
        buddy.onlyFightsAfterHit = type == BuddyType.Scavenger;
        buddy.visualSetId = type == BuddyType.Baby ? "baby" : type.ToString().ToLowerInvariant() + "_young";
        buddy.bodyColor = Color.Lerp(Color.green, Color.yellow, (index % 5) / 5f);
        buddy.happiness = 100;
        buddy.loyalty = 100;
        buddy.survivedLastRun = true;
        return buddy;
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
