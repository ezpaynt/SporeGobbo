public static class MushWormIntroEligibility
{
    public static bool IsEligible(int currentRunNumber, StoryProgressData storyProgress, bool markerAvailable)
    {
        return currentRunNumber >= 2 && markerAvailable &&
            !StoryEventService.IsCompleted(storyProgress, StoryEventIds.MushWormIntro);
    }
}
