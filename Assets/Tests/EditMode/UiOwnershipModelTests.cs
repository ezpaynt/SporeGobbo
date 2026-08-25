using NUnit.Framework;
using SporeGobbo.Input;

public class UiOwnershipModelTests
{
    [Test]
    public void DestroyedModalPauseLeaseReleasesOnlyItsOwner()
    {
        var owners = new PauseOwnerModel();
        Assert.That(owners.Acquire(1), Is.True);
        Assert.That(owners.Acquire(2), Is.True);
        Assert.That(owners.Release(1), Is.True);
        Assert.That(owners.IsPaused, Is.True);
        Assert.That(owners.Count, Is.EqualTo(1));
        Assert.That(owners.Release(2), Is.True);
        Assert.That(owners.IsPaused, Is.False);
    }

    [Test]
    public void DuplicatePauseReleaseIsSafe()
    {
        var owners = new PauseOwnerModel();
        owners.Acquire(1);
        Assert.That(owners.Release(1), Is.True);
        Assert.That(owners.Release(1), Is.False);
    }

    [Test]
    public void PauseResetClearsAllOwners()
    {
        var owners = new PauseOwnerModel();
        owners.Acquire(1);
        owners.Acquire(2);
        owners.Clear();
        Assert.That(owners.IsPaused, Is.False);
        Assert.That(owners.Count, Is.Zero);
    }

    [Test]
    public void GameplayPauseRoutesToOpenPause()
    {
        Assert.That(Decide(SporeInputContext.Gameplay, false, true, false),
            Is.EqualTo(SemanticUiRoute.OpenPause));
    }

    [Test]
    public void ModalCancelHasPriorityAndDoesNotOpenPause()
    {
        Assert.That(Decide(SporeInputContext.Modal, true, true, true),
            Is.EqualTo(SemanticUiRoute.CancelTopModal));
    }

    [Test]
    public void PauseActionClosesPause()
    {
        Assert.That(Decide(SporeInputContext.Pause, false, true, false),
            Is.EqualTo(SemanticUiRoute.ClosePause));
    }

    [Test]
    public void PauseCancelBacksSubpageBeforeClosing()
    {
        Assert.That(Decide(SporeInputContext.Pause, false, false, true, true),
            Is.EqualTo(SemanticUiRoute.PauseBack));
        Assert.That(Decide(SporeInputContext.Pause, false, false, true, false),
            Is.EqualTo(SemanticUiRoute.ClosePause));
    }

    [Test]
    public void WheelPauseRoutesToPause()
    {
        Assert.That(Decide(SporeInputContext.Wheel, false, true, false),
            Is.EqualTo(SemanticUiRoute.OpenPause));
    }

    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    public void InvalidOpenCampModalMustForceClear(bool isOpen, bool ownerExists, bool ownerActive)
    {
        Assert.That(ModalLifecyclePolicy.ShouldForceClear(isOpen, ownerExists, ownerActive), Is.True);
    }

    [Test]
    public void ValidActiveCampModalRemainsOpen()
    {
        Assert.That(ModalLifecyclePolicy.ShouldForceClear(true, true, true), Is.False);
    }

    [TestCase(true, true, true, true, true, true)]
    [TestCase(true, false, true, true, true, false)]
    [TestCase(true, true, false, true, true, false)]
    [TestCase(true, true, true, false, true, false)]
    [TestCase(true, true, true, true, false, false)]
    public void ModalDefaultRejectsHiddenDisabledNonInteractableOrForeignCandidates(
        bool exists, bool active, bool enabled, bool interactable, bool belongs, bool expected)
    {
        Assert.That(ModalFocusCandidatePolicy.IsValid(exists, active, enabled, interactable, belongs),
            Is.EqualTo(expected));
    }

    [Test]
    public void InvalidPreferredModalActionFallsBackDeterministically()
    {
        Assert.That(ModalFocusCandidatePolicy.ChooseDefaultIndex(0, new[] { true, true }), Is.EqualTo(0),
            "Available Fire recovery should remain preferred.");
        Assert.That(ModalFocusCandidatePolicy.ChooseDefaultIndex(0, new[] { false, true }), Is.EqualTo(1),
            "Disabled Fire recovery should fall back to Close.");
        Assert.That(ModalFocusCandidatePolicy.ChooseDefaultIndex(1, new[] { true, true }), Is.EqualTo(1),
            "Portal should preserve its explicitly safe Cancel preference.");
        Assert.That(ModalFocusCandidatePolicy.ChooseDefaultIndex(0, new[] { false, false }), Is.EqualTo(-1));
    }

    [TestCase(false, SporeInputContext.Gameplay)]
    [TestCase(true, SporeInputContext.Modal)]
    public void SingleSceneLoadNormalizesAwayFromWheel(bool isMainMenu, SporeInputContext expected)
    {
        Assert.That(SceneInputContextPolicy.NormalizeAfterSingleSceneLoad(isMainMenu), Is.EqualTo(expected));
    }

    private static SemanticUiRoute Decide(
        SporeInputContext context,
        bool hasModal,
        bool pause,
        bool cancel,
        bool subpage = false)
    {
        return SemanticUiRouteDecider.Decide(context, hasModal, pause, cancel, subpage);
    }
}
