using System.Collections.Generic;

public static class CampStationUnlockService
{
    public static bool IsUnlocked(GameState state, string id)
    {
        if (state == null || string.IsNullOrWhiteSpace(id) || state.unlockedStations == null) return false;
        return state.unlockedStations.Contains(id);
    }

    public static bool Unlock(GameState state, string id, bool saveImmediately = true)
    {
        if (state == null || string.IsNullOrWhiteSpace(id)) return false;

        state.unlockedStations ??= new List<string>();
        if (state.unlockedStations.Contains(id)) return false;

        state.unlockedStations.Add(id);
        if (saveImmediately) SporeSaveManager.SaveCurrentSlotFromGameState();
        return true;
    }
}
