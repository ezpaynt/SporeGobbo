using System;
using UnityEngine;

public enum CampResourceType
{
    Mushrooms,
    Shinies
}

public static class CampResourceService
{
    public static event Action ResourcesChanged;

    public static int GetAmount(GameState state, CampResourceType resourceType)
    {
        GobboUnitSaveData leader = GetLeader(state);
        if (leader == null) return 0;

        switch (resourceType)
        {
            case CampResourceType.Mushrooms:
                return Mathf.Max(0, leader.mushrooms);
            case CampResourceType.Shinies:
                return Mathf.Max(0, leader.shinies);
            default:
                return 0;
        }
    }

    public static string GetDisplayName(CampResourceType resourceType)
    {
        switch (resourceType)
        {
            case CampResourceType.Mushrooms:
                return "Mushrooms";
            case CampResourceType.Shinies:
                return "Shinies";
            default:
                return resourceType.ToString();
        }
    }

    public static void Add(GameState state, CampResourceType resourceType, int amount, bool saveImmediately = false)
    {
        if (state == null || amount <= 0) return;

        GobboUnitSaveData leader = state.GetLeader();
        if (leader == null) return;

        switch (resourceType)
        {
            case CampResourceType.Mushrooms:
                leader.mushrooms += amount;
                state.RegisterMushroomsGained(amount);
                break;
            case CampResourceType.Shinies:
                state.RegisterShiniesGained(amount);
                break;
        }

        if (saveImmediately) SporeSaveManager.SaveCurrentSlotFromGameState();
        NotifyResourcesChanged();
    }

    public static bool TrySpend(GameState state, CampResourceType resourceType, int amount, bool saveImmediately = true)
    {
        if (amount <= 0) return true;
        if (state == null) return false;

        GobboUnitSaveData leader = state.GetLeader();
        if (leader == null) return false;
        if (GetAmount(state, resourceType) < amount) return false;

        switch (resourceType)
        {
            case CampResourceType.Mushrooms:
                leader.mushrooms = Mathf.Max(0, leader.mushrooms - amount);
                break;
            case CampResourceType.Shinies:
                if (!state.TrySpendShinies(amount)) return false;
                break;
        }

        if (saveImmediately) SporeSaveManager.SaveCurrentSlotFromGameState();
        NotifyResourcesChanged();
        return true;
    }

    public static void NotifyResourcesChanged()
    {
        ResourcesChanged?.Invoke();
    }

    static GobboUnitSaveData GetLeader(GameState state)
    {
        if (state == null) return null;
        state.EnsureRuntimeDefaults();
        return state.GetLeader();
    }
}
