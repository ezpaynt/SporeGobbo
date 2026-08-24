public enum SampleSceneMode
{
    Intro,
    NormalRun
}

public static class SampleSceneModeContext
{
    static bool hasPending;
    static SampleSceneMode pendingMode;

    public static void SetPending(SampleSceneMode mode)
    {
        pendingMode = mode;
        hasPending = true;
    }

    public static SampleSceneMode ConsumeOrDefault(SampleSceneMode fallback = SampleSceneMode.NormalRun)
    {
        if (!hasPending) return fallback;
        SampleSceneMode result = pendingMode;
        hasPending = false;
        return result;
    }

    public static void Clear() => hasPending = false;
}
