using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DeadBuddyRecord
{
    public string uniqueId = "";
    public string buddyName = "Gobbo";
    public BuddyType buddyType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public int level = 1;
    public int runNumberDied = 1;
    public string causeOfDeath = "Lost in the caves";
    public bool memorialSeen = false;

    public string GetDisplayLine()
    {
        return "☠ " + buddyName +
               " the " + buddyType +
               " — " + ageStage +
               " Lv " + level +
               "\n   Run " + runNumberDied + " · " + causeOfDeath;
    }
}

/// <summary>
/// Additive death-history store that lives on the GameState object.
/// This avoids needing to heavily rewrite GameState right now.
/// CampBonesMemorialManager creates it automatically if missing.
/// </summary>
public class CampDeathHistoryStore : MonoBehaviour
{
    public List<DeadBuddyRecord> deadBuddyHistory = new List<DeadBuddyRecord>();

    public bool HasAnyDeaths()
    {
        return deadBuddyHistory != null && deadBuddyHistory.Count > 0;
    }

    public bool HasUnseenMemorials()
    {
        if (deadBuddyHistory == null)
            return false;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && !record.memorialSeen)
                return true;
        }

        return false;
    }

    public int CountUnseenMemorials()
    {
        int count = 0;

        if (deadBuddyHistory == null)
            return count;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && !record.memorialSeen)
                count++;
        }

        return count;
    }

    public void MarkAllSeen()
    {
        if (deadBuddyHistory == null)
            return;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null)
                record.memorialSeen = true;
        }
    }

    public void AddFromBuddy(BuddyData buddy, int runNumber, string cause = "Lost in the caves")
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        if (HasRecordForId(buddy.uniqueId))
            return;

        DeadBuddyRecord record = new DeadBuddyRecord();
        record.uniqueId = buddy.uniqueId;
        record.buddyName = buddy.buddyName;
        record.buddyType = buddy.buddyType;
        record.ageStage = buddy.ageStage;
        record.level = buddy.level;
        record.runNumberDied = Mathf.Max(1, runNumber);
        record.causeOfDeath = string.IsNullOrWhiteSpace(cause) ? "Lost in the caves" : cause;
        record.memorialSeen = false;

        deadBuddyHistory.Add(record);
    }

    public void AddFromLabel(string label, int runNumber, string cause = "Lost in the caves")
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        string safeName = label.Trim();

        // Avoid duplicates from the same summary label.
        if (HasRecordForLabel(safeName, runNumber))
            return;

        DeadBuddyRecord record = new DeadBuddyRecord();
        record.uniqueId = "summary_" + runNumber + "_" + safeName;
        record.buddyName = safeName;
        record.buddyType = BuddyType.Baby;
        record.ageStage = GobboAgeStage.Baby;
        record.level = 1;
        record.runNumberDied = Mathf.Max(1, runNumber);
        record.causeOfDeath = string.IsNullOrWhiteSpace(cause) ? "Lost in the caves" : cause;
        record.memorialSeen = false;

        deadBuddyHistory.Add(record);
    }

    bool HasRecordForId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || deadBuddyHistory == null)
            return false;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && record.uniqueId == id)
                return true;
        }

        return false;
    }

    bool HasRecordForLabel(string label, int runNumber)
    {
        if (deadBuddyHistory == null)
            return false;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record == null)
                continue;

            if (record.buddyName == label && record.runNumberDied == runNumber)
                return true;
        }

        return false;
    }

    public static CampDeathHistoryStore GetOrCreate()
    {
        if (GameState.Instance == null)
            return null;

        CampDeathHistoryStore store = GameState.Instance.GetComponent<CampDeathHistoryStore>();
        if (store == null)
            store = GameState.Instance.gameObject.AddComponent<CampDeathHistoryStore>();

        if (store.deadBuddyHistory == null)
            store.deadBuddyHistory = new List<DeadBuddyRecord>();

        return store;
    }
}
