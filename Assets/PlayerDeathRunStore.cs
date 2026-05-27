using System.Collections.Generic;
using UnityEngine;

public class PlayerDeathRunStore : MonoBehaviour
{
    public static PlayerDeathRunStore Instance { get; private set; }

    [Header("Pending Death Flow")]
    public bool playerDiedThisRun = false;

    public string deadLeaderName = "Gobbo";
    public string deadLeaderType = "Gobbo";
    public int deadLeaderLevel = 1;
    public int deadLeaderRunNumber = 1;
    public string deathCause = "Got squished in the dirt.";
    public bool memorialAddedToHistory = false;

    [Header("Compatibility")]
    public string deadPlayerName = "Gobbo";
    public string deadPlayerType = "Gobbo";
    public int deadPlayerLevel = 1;
    public int runNumber = 1;

    [Header("Successor Lock")]
    public string lockedSuccessorId = "";

    [Header("Survivors")]
    public List<string> eligibleSuccessorIds = new List<string>();
    public List<BuddyData> survivorSnapshots = new List<BuddyData>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SyncCompatibilityFields();
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

    public void LockSuccessorForRun(string successorId)
    {
        lockedSuccessorId = string.IsNullOrWhiteSpace(successorId) ? "" : successorId.Trim();
        Debug.Log("[PlayerDeathRunStore] Locked run successor: " + (string.IsNullOrWhiteSpace(lockedSuccessorId) ? "none" : lockedSuccessorId));
    }

    public void BeginPlayerDeath(string leaderName, int leaderLevel, int currentRunNumber, string cause, List<string> successorIds)
    {
        BeginPlayerDeath(leaderName, "Gobbo", leaderLevel, currentRunNumber, cause, successorIds, null);
    }

    public void BeginPlayerDeath(string leaderName, string leaderType, int leaderLevel, int currentRunNumber, string cause, List<string> successorIds)
    {
        BeginPlayerDeath(leaderName, leaderType, leaderLevel, currentRunNumber, cause, successorIds, null);
    }

    public void BeginPlayerDeath(string leaderName, string leaderType, int leaderLevel, int currentRunNumber, string cause, List<string> successorIds, List<BuddyData> snapshots)
    {
        playerDiedThisRun = true;
        memorialAddedToHistory = false;

        deadLeaderName = string.IsNullOrWhiteSpace(leaderName) ? "Gobbo" : leaderName.Trim();
        deadLeaderType = string.IsNullOrWhiteSpace(leaderType) ? "Gobbo" : leaderType.Trim();
        deadLeaderLevel = Mathf.Max(1, leaderLevel);
        deadLeaderRunNumber = Mathf.Max(1, currentRunNumber);
        deathCause = string.IsNullOrWhiteSpace(cause) ? "Died in the dirt." : cause.Trim();

        eligibleSuccessorIds = successorIds != null ? new List<string>(successorIds) : new List<string>();
        survivorSnapshots = new List<BuddyData>();

        if (snapshots != null)
        {
            foreach (BuddyData buddy in snapshots)
            {
                if (buddy == null)
                    continue;

                BuddyData copy = buddy.Clone();
                copy.EnsureId();
                copy.EnsureRuntimeDefaults();
                survivorSnapshots.Add(copy);
            }
        }

        SyncCompatibilityFields();

        Debug.Log("[PlayerDeathRunStore] Began death. ids: " + eligibleSuccessorIds.Count + ", snapshots: " + survivorSnapshots.Count + ", locked: " + (string.IsNullOrWhiteSpace(lockedSuccessorId) ? "none" : lockedSuccessorId));
    }

    public void ClearPendingDeath()
    {
        playerDiedThisRun = false;
        memorialAddedToHistory = false;
        eligibleSuccessorIds.Clear();
        survivorSnapshots.Clear();
        lockedSuccessorId = "";
    }

    public void SyncCompatibilityFields()
    {
        if (!string.IsNullOrWhiteSpace(deadPlayerName) && string.IsNullOrWhiteSpace(deadLeaderName))
            deadLeaderName = deadPlayerName;

        if (!string.IsNullOrWhiteSpace(deadPlayerType) && string.IsNullOrWhiteSpace(deadLeaderType))
            deadLeaderType = deadPlayerType;

        if (deadPlayerLevel > 0 && deadLeaderLevel <= 0)
            deadLeaderLevel = deadPlayerLevel;

        if (runNumber > 0 && deadLeaderRunNumber <= 0)
            deadLeaderRunNumber = runNumber;

        deadPlayerName = string.IsNullOrWhiteSpace(deadLeaderName) ? "Gobbo" : deadLeaderName;
        deadPlayerType = string.IsNullOrWhiteSpace(deadLeaderType) ? "Gobbo" : deadLeaderType;
        deadPlayerLevel = Mathf.Max(1, deadLeaderLevel);
        runNumber = Mathf.Max(1, deadLeaderRunNumber);
    }
}
