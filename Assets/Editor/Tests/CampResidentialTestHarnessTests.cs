#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class FakeRunReturnTestHarnessTests
{
    GameObject host;
    GameState state;

    [SetUp]
    public void SetUp()
    {
        if (GameState.Instance != null) Object.DestroyImmediate(GameState.Instance.gameObject);
        host = new GameObject("FakeRunHarnessTestState");
        state = host.AddComponent<GameState>();
        state.EnsureRuntimeDefaults();
    }

    [TearDown]
    public void TearDown() { if (host != null) Object.DestroyImmediate(host); }

    [Test]
    public void ValidationRejectsDeathsBeyondParticipants()
    {
        Assert.That(FakeCompletedRunBuilder.Validate(state, new FakeCompletedRunInput(1, 2, 0), out string reason), Is.False);
        Assert.That(reason, Does.Contain("deaths cannot exceed").IgnoreCase);
    }

    [Test]
    public void ValidationRejectsParticipantsBeyondLivingRoster()
    {
        state.AddGobbo(CreateExisting("b", 1), false);
        Assert.That(FakeCompletedRunBuilder.Validate(state, new FakeCompletedRunInput(2, 0, 0), out string reason), Is.False);
        Assert.That(reason, Does.Contain("living existing"));
    }

    [Test]
    public void ParticipantAndDeathSelectionIsDeterministicAndDeathsAreParticipants()
    {
        state.AddGobbo(CreateExisting("z", 1), false);
        state.AddGobbo(CreateExisting("a", 2), false);
        state.AddGobbo(CreateExisting("m", 3), false);
        var participants = FakeCompletedRunBuilder.SelectParticipants(state, 2);
        var deaths = FakeCompletedRunBuilder.SelectDeaths(participants, 1);
        Assert.That(participants.Select(unit => unit.uniqueId), Is.EqualTo(new[] { "a", "m" }));
        Assert.That(deaths.Single().uniqueId, Is.EqualTo("a"));
        Assert.That(participants.Any(unit => unit.uniqueId == deaths.Single().uniqueId), Is.True);
    }

    [Test]
    public void NewBuddiesAreValidBabiesAndUnassigned()
    {
        FakeCompletedRunResult result = FakeCompletedRunBuilder.Apply(state, new FakeCompletedRunInput(0, 0, 3));
        Assert.That(result.NewBuddyIds.Count, Is.EqualTo(3));
        foreach (string id in result.NewBuddyIds)
        {
            GobboUnitSaveData buddy = state.FindOwnedGobbo(id);
            Assert.That(buddy.gobboType, Is.EqualTo(BuddyType.Baby));
            Assert.That(buddy.ageStage, Is.EqualTo(GobboAgeStage.Baby));
            Assert.That(buddy.isDead, Is.False);
            Assert.That(buddy.campResidentialSlotId, Is.Zero);
        }
    }

    [Test]
    public void FakeBuddySequenceNeverReusesADeadNumber()
    {
        GobboUnitSaveData one = FakeCompletedRunBuilder.CreateNewBuddy(state, 1);
        state.AddGobbo(one, false);
        GobboUnitSaveData two = FakeCompletedRunBuilder.CreateNewBuddy(state, 2);
        state.AddGobbo(two, false);
        GobboUnitSaveData three = FakeCompletedRunBuilder.CreateNewBuddy(state, 3);
        state.AddGobbo(three, false);
        state.RegisterGobboDeath(state.FindOwnedGobbo(one.uniqueId));
        state.RemoveGobbo(one.uniqueId);

        GobboUnitSaveData four = FakeCompletedRunBuilder.CreateNewBuddy(state, 1);

        Assert.That(one.displayName, Is.EqualTo("Run Gobbo 01"));
        Assert.That(two.displayName, Is.EqualTo("Run Gobbo 02"));
        Assert.That(three.displayName, Is.EqualTo("Run Gobbo 03"));
        Assert.That(four.displayName, Is.EqualTo("Run Gobbo 04"));
        Assert.That(four.uniqueId, Does.EndWith("000004"));
    }

    [Test]
    public void SurvivorsKeepIdentityAndHomeAndResidentialStateIsNotMutated()
    {
        state.AddGobbo(CreateExisting("survivor", 4), false);
        state.campTerrainState.residentialSlotsEstablished = 7;
        state.campTerrainState.clearedCellCoordinates.Add(new CampCellCoordinate(12, 12));
        FakeCompletedRunBuilder.Apply(state, new FakeCompletedRunInput(1, 0, 0));
        Assert.That(state.FindOwnedGobbo("survivor"), Is.SameAs(state.ownedGobbos.Single()));
        Assert.That(state.FindOwnedGobbo("survivor").campResidentialSlotId, Is.EqualTo(4));
        Assert.That(state.campTerrainState.residentialSlotsEstablished, Is.EqualTo(7));
        Assert.That(state.campTerrainState.clearedCellCoordinates.Count, Is.EqualTo(1));
    }

    [Test]
    public void FakeResultFeedsProductionRunSummaryForMultipleNewAndOrdinaryDeaths()
    {
        state.AddGobbo(CreateExisting("a", 1), false);
        state.AddGobbo(CreateExisting("b", 2), false);
        state.AddGobbo(CreateExisting("c", 3), false);
        state.AddGobbo(CreateExisting("d", 4), false);
        FakeCompletedRunResult result = FakeCompletedRunBuilder.Apply(state, new FakeCompletedRunInput(4, 2, 3));
        Assert.That(result.ParticipantIds.Count, Is.EqualTo(4));
        Assert.That(result.DeathIds.Count, Is.EqualTo(2));
        Assert.That(state.lastRun.activeBuddyReports.Count, Is.EqualTo(4));
        Assert.That(state.lastRun.activeBuddyReports.Count(report => report.died), Is.EqualTo(2));
        Assert.That(state.lastRun.buddiesLost, Is.EqualTo(2));
        Assert.That(state.lastRun.buddiesFound, Is.EqualTo(3));
        Assert.That(state.lastRun.newBuddyNames.Count, Is.EqualTo(3));
        Assert.That(state.deathHistory.Count, Is.EqualTo(2));
    }

    [Test]
    public void CapturedParticipantsDoNotIncludeNonparticipantCurrentSquadMembers()
    {
        state.AddGobbo(CreateExisting("a", 1), false);
        state.AddGobbo(CreateExisting("b", 2), false);
        state.MoveBuddyToActiveSquad("a");
        state.MoveBuddyToActiveSquad("b");

        FakeCompletedRunBuilder.Apply(state, new FakeCompletedRunInput(1, 0, 0));

        Assert.That(state.lastRun.activeBuddyReports.Select(report => report.buddyId), Is.EqualTo(new[] { "a" }));
        Assert.That(state.lastRun.reserveBuddyReports.Any(report => report.buddyId == "b"), Is.True);
    }

    [Test]
    public void ProductionReportDisplaysParticipationReturnDeathAndNewCounts()
    {
        state.AddGobbo(CreateExisting("a", 1), false);
        state.AddGobbo(CreateExisting("b", 2), false);
        FakeCompletedRunBuilder.Apply(state, new FakeCompletedRunInput(2, 1, 2));
        string report = CampReportTextBuilder.BuildMiddleSurvivorSummary(state.lastRun, null, state.ownedGobbos.Count);
        Assert.That(report, Does.Contain("Participated: 2"));
        Assert.That(report, Does.Contain("Returned: 1"));
        Assert.That(report, Does.Contain("Buddies lost: 1"));
        Assert.That(report, Does.Contain("New buddies: 2"));
    }

    [Test]
    public void HarnessHasNoResidentialConsequenceMethods()
    {
        string[] forbidden = { "Assign", "Vacate", "Excavate", "Rebuild", "Establish", "RunResidentialSlotArrival" };
        string[] methods = typeof(FakeRunReturnTestWindow).GetMethods(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Select(method => method.Name).ToArray();
        foreach (string fragment in forbidden)
            Assert.That(methods.Any(method => method.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
    }

    [Test]
    public void ReturnedFromRunContextCanBeConsumedByProductionCampController()
    {
        CampArrivalContext.Clear();
        CampArrivalContext.SetPending(CampArrivalMode.ReturnedFromRun);
        Assert.That(CampArrivalContext.ConsumeOrDefault(), Is.EqualTo(CampArrivalMode.ReturnedFromRun));
        Assert.That(CampArrivalContext.ConsumeOrDefault(), Is.EqualTo(CampArrivalMode.LoadedSave));
    }

    [Test]
    public void GameStateDetachesFromSceneHierarchySoRunResultSurvivesCampReload()
    {
        Object.DestroyImmediate(host);
        host = null;
        GameObject parent = new GameObject("SceneSystems");
        GameObject child = new GameObject("NestedGameState");
        child.transform.SetParent(parent.transform);

        try
        {
            GameState nestedState = child.AddComponent<GameState>();
            typeof(GameState).GetMethod("DetachFromSceneHierarchy",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { nestedState.transform });
            Assert.That(nestedState.transform.parent == null, Is.True,
                "GameState must be a root object before DontDestroyOnLoad is applied.");
        }
        finally
        {
            Object.DestroyImmediate(child);
            Object.DestroyImmediate(parent);
        }
    }

    static GobboUnitSaveData CreateExisting(string id, int slot)
    {
        var buddy = new GobboUnitSaveData
        {
            uniqueId = id, displayName = "Existing " + id, isLeader = false, isDead = false,
            health = 10, maxHealth = 10, gobboType = BuddyType.Baby, ageStage = GobboAgeStage.Baby,
            campResidentialSlotId = slot
        };
        buddy.EnsureRuntimeDefaults();
        return buddy;
    }
}
#endif
