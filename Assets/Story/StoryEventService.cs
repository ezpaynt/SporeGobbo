using System;

public static class StoryEventIds
{
    public const string ASporeHatchesNamed = "story.a_spore_hatches.named";
    public const string ASporeHatchesCampDiscovered = "story.a_spore_hatches.camp_discovered";
}

public static class StoryEventService
{
    public static event Action<StoryEventRecord> StoryEventCompleted;

    public static bool IsCompleted(GameState state, string eventId)
    {
        return state != null && IsCompleted(state.storyProgress, eventId);
    }

    public static bool IsCompleted(StoryProgressData progress, string eventId)
    {
        return progress != null && progress.Contains(eventId);
    }

    public static bool Complete(GameState state, string eventId)
    {
        if (state == null) return false;
        state.EnsureRuntimeDefaults();
        bool completed = Complete(state.storyProgress, eventId, state.currentRunNumber, state.campCycleNumber, out StoryEventRecord record);
        if (completed) StoryEventCompleted?.Invoke(record);
        return completed;
    }

    public static bool Complete(StoryProgressData progress, string eventId, int runNumber, int campCycle)
    {
        return Complete(progress, eventId, runNumber, campCycle, out _);
    }

    static bool Complete(StoryProgressData progress, string eventId, int runNumber, int campCycle, out StoryEventRecord record)
    {
        record = null;
        if (progress == null || string.IsNullOrWhiteSpace(eventId)) return false;
        progress.Normalize();
        string normalizedId = eventId.Trim();
        if (progress.Contains(normalizedId)) return false;

        record = new StoryEventRecord
        {
            eventId = normalizedId,
            unlockSequence = progress.nextUnlockSequence,
            runNumber = Math.Max(0, runNumber),
            campCycle = Math.Max(0, campCycle)
        };
        progress.completedEvents.Add(record);
        progress.nextUnlockSequence++;
        return true;
    }
}
