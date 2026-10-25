using NUnit.Framework;
using SporeGobbo.Input;
using UnityEngine;
using UnityEngine.UI;

public sealed class MushWormStoryEventTests
{
    [TestCase(1, false, true, false)]
    [TestCase(2, false, true, true)]
    [TestCase(10, false, true, true)]
    [TestCase(2, true, true, false)]
    [TestCase(2, false, false, false)]
    public void EligibilityIsPureAndExplicit(int run, bool completed, bool marker, bool expected)
    {
        var progress = new StoryProgressData();
        if (completed) StoryEventService.Complete(progress, StoryEventIds.MushWormIntro, run, 3);
        int recordsBefore = progress.completedEvents.Count;
        int sequenceBefore = progress.nextUnlockSequence;
        Assert.That(MushWormIntroEligibility.IsEligible(run, progress, marker), Is.EqualTo(expected));
        Assert.That(progress.completedEvents.Count, Is.EqualTo(recordsBefore));
        Assert.That(progress.nextUnlockSequence, Is.EqualTo(sequenceBefore));
    }

    [Test]
    public void CompletionIsIdempotentAndCapturesRunContext()
    {
        var progress = new StoryProgressData();
        Assert.That(StoryEventService.Complete(progress, StoryEventIds.MushWormIntro, 4, 2), Is.True);
        Assert.That(StoryEventService.Complete(progress, StoryEventIds.MushWormIntro, 9, 9), Is.False);
        Assert.That(progress.completedEvents, Has.Count.EqualTo(1));
        Assert.That(progress.completedEvents[0].eventId, Is.EqualTo(StoryEventIds.MushWormIntro));
        Assert.That(progress.completedEvents[0].runNumber, Is.EqualTo(4));
        Assert.That(progress.completedEvents[0].campCycle, Is.EqualTo(2));
    }

    [Test]
    public void StoryInputDisablesWorldActionsButKeepsPauseAndUi()
    {
        SporeInputAvailability availability = SporeInputContextRouter.GetAvailability(SporeInputContext.Story);
        Assert.That(availability.GameplayMap, Is.True);
        Assert.That(availability.GameplayWorldActions, Is.False);
        Assert.That(availability.Move, Is.False);
        Assert.That(availability.Aim, Is.False);
        Assert.That(availability.CommandWheel, Is.False);
        Assert.That(availability.Pause, Is.True);
        Assert.That(availability.UiMap, Is.True);
    }

    [Test]
    public void StoryRouteOpensPauseInsteadOfCancellingModal()
    {
        Assert.That(SemanticUiRouteDecider.Decide(SporeInputContext.Story, true, true, false, false),
            Is.EqualTo(SemanticUiRoute.OpenPause));
        Assert.That(SemanticUiRouteDecider.Decide(SporeInputContext.Story, true, false, true, false),
            Is.EqualTo(SemanticUiRoute.None));
        Assert.That(SemanticUiRouteDecider.Decide(SporeInputContext.Pause, true, true, false, false),
            Is.EqualTo(SemanticUiRoute.ClosePause));
    }

    [Test]
    public void PersistentLayoutAuthorsMovableMushWormMarker()
    {
        PersistentTestCaveLayout layout = UnityEditor.AssetDatabase.LoadAssetAtPath<PersistentTestCaveLayout>(
            "Assets/Persistent Test Cave Layout.asset");
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.TryGetEventLocation(MushWormIntroRuntime.MarkerKey, out var marker), Is.True);
        Assert.That(marker.departureWaypoints, Is.Not.Empty);
        Assert.That(marker.triggerRadius, Is.GreaterThan(0f));
    }

    [Test]
    public void MinimalConversationPresentationBuildsWithReusableInputs()
    {
        GameObject canvasObject = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject cameraObject = new GameObject("Test Camera", typeof(Camera), typeof(CameraFollow));
        GameObject host = new GameObject("Test Presentation");
        try
        {
            cameraObject.tag = "MainCamera";
            StoryEventPresentation presentation = host.AddComponent<StoryEventPresentation>();
            Assert.That(presentation.Prepare("Speaker", "Line", () => { }), Is.True);
            Assert.That(presentation.ContinueButton, Is.Not.Null);
            Assert.That(presentation.ContinueButton.GetComponentInChildren<Text>().text, Is.EqualTo("CONTINUE"));
            presentation.RestoreImmediately();
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void StoryCatalogAndJournalUseCompletionAsSingleTruth()
    {
        StoryEventCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<StoryEventCatalog>(
            "Assets/Story/Content/Story Event Catalog.asset");
        JournalContentLibrary journal = UnityEditor.AssetDatabase.LoadAssetAtPath<JournalContentLibrary>(
            "Assets/Story/Content/Journal Content Library.asset");
        Assert.That(catalog.events.Exists(e => e.eventId == StoryEventIds.MushWormIntro), Is.True);
        GameObject host = new GameObject("Story journal test state");
        try
        {
            GameState state = host.AddComponent<GameState>();
            state.storyProgress = new StoryProgressData();
            var before = JournalSnapshotBuilder.Build(journal, state);
            Assert.That(ContainsJournalEntry(before, "entry.mush_worm.intro"), Is.False);
            StoryEventService.Complete(state.storyProgress, StoryEventIds.MushWormIntro, 2, 1);
            var after = JournalSnapshotBuilder.Build(journal, state);
            Assert.That(ContainsJournalEntry(after, "entry.mush_worm.intro"), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    static bool ContainsJournalEntry(JournalSnapshot snapshot, string entryId)
    {
        foreach (JournalThreadSnapshot thread in snapshot.threads)
            if (thread.entries.Exists(e => e.entryId == entryId)) return true;
        return false;
    }
}
