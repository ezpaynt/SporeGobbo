public enum CampArrivalMode
{
    NewGameIntro,
    ReturnedFromRun,
    PostDeathSuccession,
    LoadedSave
}

public static class CampArrivalContext
{
    static bool hasPending;
    static CampArrivalMode pendingMode;
    static int generation;
    static string pendingId;
    public static bool HasPending => hasPending;
    public static string PendingId => hasPending ? pendingId : null;
    public static void RestorePending(CampArrivalMode mode, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !System.Guid.TryParseExact(id,"N",out _)) throw new System.InvalidOperationException("Invalid arrival transaction identity");
        SetPending(mode); pendingId = id;
    }
    public static int Generation => generation;
    public static CampArrivalMode PeekOrDefault(CampArrivalMode fallback = CampArrivalMode.LoadedSave) => hasPending ? pendingMode : fallback;
    public static bool Acknowledge(int expectedGeneration)
    {
        if (!hasPending || expectedGeneration != generation) return false;
        hasPending = false; return true;
    }

    public static void SetPending(CampArrivalMode mode)
    {
        pendingMode = mode;
        generation++;
        pendingId = System.Guid.NewGuid().ToString("N");
        hasPending = true;
    }

    public static CampArrivalMode ConsumeOrDefault(CampArrivalMode fallback = CampArrivalMode.LoadedSave)
    {
        if (!hasPending) return fallback;
        CampArrivalMode result = pendingMode;
        hasPending = false;
        return result;
    }

    public static void Clear() => hasPending = false;
}
