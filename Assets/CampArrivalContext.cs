public enum CampArrivalMode
{
    NewGameIntro,
    ReturnedFromRun,
    LoadedSave
}

public static class CampArrivalContext
{
    static bool hasPending;
    static CampArrivalMode pendingMode;

    public static void SetPending(CampArrivalMode mode)
    {
        pendingMode = mode;
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
