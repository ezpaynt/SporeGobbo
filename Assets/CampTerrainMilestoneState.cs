using SporeGobbo.CampLifecycle;

/// <summary>
/// Adapts durable production save facts to pure Camp terrain milestone policy.
/// No second milestone flag is persisted.
/// </summary>
public static class CampTerrainMilestoneState
{
    public static bool IsSatisfied(CampTerrainMilestone milestone, GameState state)
    {
        if (state == null) return false;
        if (milestone == CampTerrainMilestone.FirstDeath)
            return CampTerrainProgressionPolicy.IsFirstDeath(
                state.deathHistory?.FindAll(record => record != null).Count ?? 0);
        if (milestone != CampTerrainMilestone.FirstBuddyRecruited) return false;
        int ownedBuddyCount = state.ownedGobbos?.FindAll(unit => unit != null).Count ?? 0;
        int establishedHomes = state.campTerrainState?.residentialSlotsEstablished ?? 0;
        int deadBuddyCount = state.deathHistory?.FindAll(record => record != null && !record.wasLeader).Count ?? 0;
        return CampTerrainProgressionPolicy.IsFirstBuddyRecruited(
            ownedBuddyCount, establishedHomes, deadBuddyCount);
    }
}
