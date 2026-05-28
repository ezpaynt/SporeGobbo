using UnityEngine;

public static class GobboSaveDataExtensions
{
    public static void EnsureLeaderIdentity(this GobboSaveData data)
    {
        EnsureLeaderIdentity(data, "Gobbo");
    }

    public static void EnsureLeaderIdentity(this GobboSaveData data, string preferredName)
    {
        if (data == null)
            return;

        data.isLeader = true;

        if (!string.IsNullOrWhiteSpace(preferredName) &&
            (string.IsNullOrWhiteSpace(data.displayName) || data.displayName == "Gobbo"))
        {
            data.displayName = preferredName.Trim();
        }

        data.EnsureRuntimeDefaults();

        if (data.maxHealth < 1)
            data.maxHealth = 100;

        if (data.health < 1 || data.health > data.maxHealth)
            data.health = data.maxHealth;

        if (data.moveSpeed <= 0f)
            data.moveSpeed = 5f;
    }

    public static GobboUnitSaveData ToUnitSave(this GobboSaveData data)
    {
        if (data == null)
        {
            GobboUnitSaveData fallback = new GobboUnitSaveData { isLeader = true };
            fallback.EnsureRuntimeDefaults();
            return fallback;
        }

        data.EnsureLeaderIdentity(data.displayName);

        GobboUnitSaveData unit = new GobboUnitSaveData();
        data.CopyInto(unit);
        unit.isLeader = true;
        unit.EnsureRuntimeDefaults();
        return unit;
    }

    public static GobboSaveData ToLeaderSave(this GobboUnitSaveData unit)
    {
        GobboSaveData leader = new GobboSaveData();

        if (unit != null)
            unit.CopyInto(leader);

        leader.isLeader = true;
        leader.EnsureLeaderIdentity(leader.displayName);
        return leader;
    }

    public static GobboSaveData CloneLeader(this GobboSaveData data)
    {
        return data == null ? new GobboSaveData() : data.ToUnitSave().ToLeaderSave();
    }

    public static BuddyData AsBuddyData(this GobboUnitSaveData unit)
    {
        if (unit == null)
            return null;

        if (unit is BuddyData existingBuddy)
        {
            existingBuddy.EnsureRuntimeDefaults();
            return existingBuddy;
        }

        BuddyData buddy = BuddyData.FromUnit(unit);
        if (buddy != null)
            buddy.EnsureRuntimeDefaults();

        return buddy;
    }
}
