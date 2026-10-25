#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class FactualHistoryTests
{
    GameObject host;
    GameState state;

    [SetUp]
    public void SetUp()
    {
        if (GameState.Instance != null) Object.DestroyImmediate(GameState.Instance.gameObject);
        host = new GameObject("FactualHistoryTestState");
        state = host.AddComponent<GameState>();
        state.SetLeader(Unit("leader", "Grub", true));
        state.EnsureRuntimeDefaults();
        state.lastRun = new RunSummaryData();
    }

    [TearDown]
    public void TearDown() { if (host != null) Object.DestroyImmediate(host); }

    [Test]
    public void MushroomConsumptionRegistersRunCampaignAndActualEater()
    {
        state.RegisterMushroomEaten("leader");
        Assert.That(state.lastRun.mushroomsEaten, Is.EqualTo(1));
        Assert.That(state.campaignFacts.mushroomsEaten, Is.EqualTo(1));
        Assert.That(state.GetLeader().mushroomsEaten, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void LeaderTypedKillCreditsOnlyLeader(bool weevil)
    {
        bool credited = weevil ? state.RegisterWeevilKilled("leader") : state.RegisterBlobKilled("leader");
        Assert.That(credited, Is.True);
        Assert.That(state.lastRun.enemiesKilled, Is.EqualTo(1));
        Assert.That(state.GetLeader().kills, Is.EqualTo(1));
        Assert.That(weevil ? state.campaignFacts.weevilsKilled : state.campaignFacts.blobsKilled, Is.EqualTo(1));
        Assert.That(weevil ? state.GetLeader().weevilsKilled : state.GetLeader().blobsKilled, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BuddyTypedKillCreditsOnlyBuddy(bool weevil)
    {
        GobboUnitSaveData buddy = Unit("buddy", "Nib", false); state.AddGobbo(buddy, false);
        bool credited = weevil ? state.RegisterWeevilKilled("buddy") : state.RegisterBlobKilled("buddy");
        GobboUnitSaveData savedBuddy = state.FindGobboById("buddy");
        Assert.That(credited, Is.True);
        Assert.That(savedBuddy.kills, Is.EqualTo(1));
        Assert.That(weevil ? savedBuddy.weevilsKilled : savedBuddy.blobsKilled, Is.EqualTo(1));
        Assert.That(state.GetLeader().kills, Is.Zero);
    }

    [Test]
    public void UnattributedTypedDeathCountsGenericRunDeathOnly()
    {
        Assert.That(state.RegisterWeevilKilled("missing"), Is.False);
        Assert.That(state.lastRun.enemiesKilled, Is.EqualTo(1));
        Assert.That(state.lastRun.weevilsKilled, Is.Zero);
        Assert.That(state.campaignFacts.weevilsKilled, Is.Zero);
        Assert.That(state.GetLeader().kills, Is.Zero);
    }

    [Test]
    public void RunSnapshotGuardDoesNotEraseCurrentFactsAndFinalizationClearsGuard()
    {
        Assert.That(state.TryBeginRunSnapshot(), Is.True);
        state.RegisterMushroomEaten("leader");
        Assert.That(state.TryBeginRunSnapshot(), Is.False);
        Assert.That(state.lastRun.mushroomsEaten, Is.EqualTo(1));
        state.EndRunSnapshot();
        Assert.That(state.HasActiveRunSnapshot, Is.False);
        Assert.That(state.TryBeginRunSnapshot(), Is.True);
        Assert.That(state.lastRun.mushroomsEaten, Is.Zero);
    }

    [Test]
    public void SuccessionPreservesOneGobboBiographyAndCampaignFacts()
    {
        GobboUnitSaveData nib = Unit("nib", "Nib", false); nib.kills = 7; nib.weevilsKilled = 5; nib.blobsKilled = 2;
        state.AddGobbo(nib, false); state.campaignFacts.weevilsKilled = 12;
        Assert.That(state.PromoteBuddyToLeader("nib"), Is.True);
        Assert.That(state.GetLeader().uniqueId, Is.EqualTo("nib"));
        Assert.That(state.GetLeader().kills, Is.EqualTo(7));
        Assert.That(state.GetLeader().weevilsKilled, Is.EqualTo(5));
        Assert.That(state.GetLeader().blobsKilled, Is.EqualTo(2));
        Assert.That(state.campaignFacts.weevilsKilled, Is.EqualTo(12));
    }

    [Test]
    public void OldSaveNormalizationCreatesZeroedFactsWithoutInference()
    {
        SporeSaveSlotData slot = new SporeSaveSlotData { campaignFacts = null, leader = Unit("old", "Old", true) };
        slot.leader.kills = 99; slot.Normalize();
        Assert.That(slot.campaignFacts, Is.Not.Null);
        Assert.That(slot.campaignFacts.weevilsKilled, Is.Zero);
        Assert.That(slot.leader.weevilsKilled, Is.Zero);
    }

    [Test]
    public void DevStateReadsAllThreeFactScopesWithoutMutatingThem()
    {
        state.TryBeginRunSnapshot();
        state.RegisterMushroomEaten("leader"); state.RegisterBlobKilled("leader");
        string text = DevStateSnapshot.Format(DevStateSnapshot.Build(state));
        Assert.That(text, Does.Contain("CAMPAIGN / LINEAGE"));
        Assert.That(text, Does.Contain("CURRENT EXPEDITION"));
        Assert.That(text, Does.Contain("LEADER — GRUB"));
        Assert.That(text, Does.Contain("WORLD / MAP"));
        Assert.That(text, Does.Contain("DEATHS / MEMORIALS"));
        Assert.That(state.campaignFacts.mushroomsEaten, Is.EqualTo(1));
    }

    static GobboUnitSaveData Unit(string id, string name, bool leader) => new GobboUnitSaveData
    { uniqueId = id, displayName = name, isLeader = leader, health = 10, maxHealth = 10 };
}
#endif
