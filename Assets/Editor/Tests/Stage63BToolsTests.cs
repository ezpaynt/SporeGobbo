#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Pure UI/query projections must not repair canonical data as a side effect.</summary>
public sealed class Stage63BToolsTests
{
    GameObject host;
    GameState state;
    JournalContentLibrary library;
    [SetUp] public void SetUp()
    {
        if (GameState.Instance != null) Object.DestroyImmediate(GameState.Instance.gameObject);
        host = new GameObject("V1 read-only projection fixture");
        state = host.AddComponent<GameState>();
        state.EnsureRuntimeDefaults();
    }
    [TearDown] public void TearDown()
    {
        if (library != null) Object.DestroyImmediate(library);
        if (host != null) Object.DestroyImmediate(host);
    }
    string StateBytes() => JsonUtility.ToJson(state);

    [Test] public void InventoryQueriesObserveMergedValuesWithoutChangingCanonicalStacks()
    {
        state.itemStacks = new List<InventoryStackSaveData>
        {
            new InventoryStackSaveData(" v1.read.fixture ", 2),
            new InventoryStackSaveData("v1.read.fixture", 3),
            new InventoryStackSaveData("v1.read.fixture", -5), null
        };
        string before = StateBytes();
        Assert.That(InventoryService.GetQuantity(state, "v1.read.fixture"), Is.EqualTo(5));
        Assert.That(InventoryService.Has(state, " v1.read.fixture ", 5), Is.True);
        var view = InventoryService.GetStacks(state);
        Assert.That(view.Count, Is.EqualTo(1));
        Assert.That(view[0].quantity, Is.EqualTo(5));
        Assert.That(InventoryService.EnumerateOwnedDefinitions(state).ToArray(), Is.Empty);
        view[0].quantity = 123;
        Assert.That(StateBytes(), Is.EqualTo(before));
    }

    [Test] public void InventoryMutationStillNormalizesAndAccountsForExistingDuplicates()
    {
        state.itemStacks = new List<InventoryStackSaveData>
        {
            new InventoryStackSaveData(" v1.read.fixture ", 2),
            new InventoryStackSaveData("v1.read.fixture", 3)
        };
        Assert.That(InventoryService.TryAddById(state, "v1.read.fixture", 2), Is.True);
        Assert.That(InventoryService.GetQuantity(state, "v1.read.fixture"), Is.EqualTo(7));
        Assert.That(state.itemStacks.Count, Is.EqualTo(1));
        Assert.That(InventoryService.TryRemove(state, "v1.read.fixture", 4), Is.True);
        Assert.That(InventoryService.GetQuantity(state, "v1.read.fixture"), Is.EqualTo(3));
    }

    [Test] public void JournalNormalizesDetachedRecordsAndPreservesUnlockOrder()
    {
        state.storyProgress = new StoryProgressData
        {
            nextUnlockSequence = -3,
            completedEvents = new List<StoryEventRecord>
            {
                new StoryEventRecord { eventId = " one ", unlockSequence = 0, runNumber = -1 },
                null,
                new StoryEventRecord { eventId = "two", unlockSequence = 1 },
                new StoryEventRecord { eventId = "one", unlockSequence = 3 }
            }
        };
        library = ScriptableObject.CreateInstance<JournalContentLibrary>();
        library.threads.Add(new JournalThreadDefinition
        {
            threadId = "fixture", entries = new List<JournalEntryDefinition>
            {
                new JournalEntryDefinition { entryId = "entry.one", requiredStoryEventId = "one" },
                new JournalEntryDefinition { entryId = "entry.two", requiredStoryEventId = "two" }
            }
        });
        string before = StateBytes();
        var view = JournalSnapshotBuilder.Build(library, state);
        Assert.That(view.threads.Single().entries.Select(e => e.entryId), Is.EqualTo(new[] { "entry.two", "entry.one" }));
        Assert.That(view.threads[0].entries[1].unlockSequence, Is.EqualTo(3));
        Assert.That(StateBytes(), Is.EqualTo(before));
    }

    [Test] public void DataProjectionHandlesMissingDefaultsWithoutRepairingCampaign()
    {
        state.leader = null;
        state.campaignFacts = null;
        state.lastRun = null;
        state.ownedGobbos = null;
        state.itemStacks = null;
        state.storyProgress = null;
        state.campTerrainState = null;
        state.persistentTestCaveTerrain = null;
        state.deathHistory = null;
        string before = StateBytes();
        var sections = DevStateSnapshot.Build(state);
        Assert.That(sections.Any(s => s.heading.StartsWith("LEADER")), Is.True);
        Assert.That(DevStateSnapshot.Format(sections), Does.Contain("Missing"));
        Assert.That(StateBytes(), Is.EqualTo(before));
    }

    [Test] public void DataProjectionToleratesNullListEntriesAndPendingQueues()
    {
        state.leader.displayName = null;
        state.leader.pendingGrowthQueue = null;
        state.ownedGobbos = new List<GobboUnitSaveData> { null };
        state.storyProgress.completedEvents = new List<StoryEventRecord> { null };
        state.deathHistory = new List<DeadBuddyRecord> { null };
        state.campTerrainState.clearedCellCoordinates = null;
        string before = StateBytes();
        Assert.That(DevStateSnapshot.Build(state), Is.Not.Empty);
        Assert.That(StateBytes(), Is.EqualTo(before));
    }

    [Test] public void FakeRunValidationIsReadOnlyAndRejectsUnavailableParticipants()
    {
        state.ownedGobbos = null;
        state.leader.displayName = null;
        string before = StateBytes();
        Assert.That(FakeCompletedRunBuilder.Validate(state, new FakeCompletedRunInput(1, 0, 0), out var reason), Is.False);
        Assert.That(reason, Does.Contain("living existing"));
        Assert.That(StateBytes(), Is.EqualTo(before));
    }
}
#endif
