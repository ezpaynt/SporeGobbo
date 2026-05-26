using System.Collections.Generic;
using UnityEngine;

public class PlayerDeathRunStore : MonoBehaviour
{
    public static PlayerDeathRunStore Instance { get; private set; }

    [Header("Pending Death Flow")]
    public bool playerDiedThisRun = false;

    // Newer names used by the succession/death flow.
    public string deadLeaderName = "Gobbo";
    public string deadLeaderType = "Gobbo";
    public int deadLeaderLevel = 1;
    public int deadLeaderRunNumber = 1;
    public string deathCause = "Got squished in the dirt.";

    // Compatibility aliases used by earlier CampDeathHistoryStore versions.
    // Keep these synced with the leader fields so old/new scripts both compile.
    public string deadPlayerName = "Gobbo";
    public string deadPlayerType = "Gobbo";
    public int deadPlayerLevel = 1;
    public int runNumber = 1;

    [Header("Survivors")]
    public List<string> eligibleSuccessorIds = new List<string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureLists();
        SyncAliasesFromLeaderFields();
    }

    public static PlayerDeathRunStore GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        PlayerDeathRunStore found = Object.FindAnyObjectByType<PlayerDeathRunStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject obj = new GameObject("PlayerDeathRunStore");
        return obj.AddComponent<PlayerDeathRunStore>();
    }

    public void BeginPlayerDeath(string leaderName, int leaderLevel, int runNumber, string cause, List<string> successorIds)
    {
        BeginPlayerDeath(leaderName, "Gobbo", leaderLevel, runNumber, cause, successorIds);
    }

    public void BeginPlayerDeath(string leaderName, string leaderType, int leaderLevel, int runNumber, string cause, List<string> successorIds)
    {
        playerDiedThisRun = true;

        deadLeaderName = string.IsNullOrWhiteSpace(leaderName) ? "Gobbo" : leaderName.Trim();
        deadLeaderType = string.IsNullOrWhiteSpace(leaderType) ? "Gobbo" : leaderType.Trim();
        deadLeaderLevel = Mathf.Max(1, leaderLevel);
        deadLeaderRunNumber = Mathf.Max(1, runNumber);
        deathCause = string.IsNullOrWhiteSpace(cause) ? "Died in the dirt." : cause.Trim();
        eligibleSuccessorIds = successorIds != null ? new List<string>(successorIds) : new List<string>();

        SyncAliasesFromLeaderFields();
    }

    public void ClearPendingDeath()
    {
        playerDiedThisRun = false;
        EnsureLists();
        eligibleSuccessorIds.Clear();
    }

    public void SyncAliasesFromLeaderFields()
    {
        deadPlayerName = deadLeaderName;
        deadPlayerType = deadLeaderType;
        deadPlayerLevel = deadLeaderLevel;
        runNumber = deadLeaderRunNumber;
    }

    public void SyncLeaderFieldsFromAliases()
    {
        deadLeaderName = deadPlayerName;
        deadLeaderType = deadPlayerType;
        deadLeaderLevel = deadPlayerLevel;
        deadLeaderRunNumber = runNumber;
    }

    void EnsureLists()
    {
        if (eligibleSuccessorIds == null)
            eligibleSuccessorIds = new List<string>();
    }
}
