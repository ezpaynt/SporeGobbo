public enum SampleSceneMode
{
    Intro,
    NormalRun
}

public static class SampleSceneModeContext
{
    static bool hasPending;
    static SampleSceneMode pendingMode;
    static RunProfile pendingProfileOverride;

    public static void SetPending(SampleSceneMode mode)
    {
        pendingMode = mode;
        pendingProfileOverride = null;
        hasPending = true;
    }

    public static void SetPending(SampleSceneMode mode, RunProfile profileOverride)
    {
        pendingMode = mode;
        pendingProfileOverride = profileOverride;
        hasPending = true;
    }

    public static RunProfile ConsumeProfileOverride()
    {
        RunProfile result = pendingProfileOverride;
        pendingProfileOverride = null;
        return result;
    }

    public static SampleSceneMode ConsumeOrDefault(SampleSceneMode fallback = SampleSceneMode.NormalRun)
    {
        if (!hasPending) return fallback;
        SampleSceneMode result = pendingMode;
        hasPending = false;
        return result;
    }

    public static void Clear()
    {
        hasPending = false;
        pendingProfileOverride = null;
    }
}
