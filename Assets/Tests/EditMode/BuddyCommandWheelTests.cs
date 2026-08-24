using NUnit.Framework;
using SporeGobbo.Input;
using UnityEngine;

public class BuddyCommandWheelTests
{
    [Test]
    public void DeadZoneReturnsNoSlice()
    {
        Assert.That(RadialSelectionMath.GetSlice(Vector2.zero, 0.25f, 4, 90f), Is.EqualTo(-1));
        Assert.That(RadialSelectionMath.GetSlice(Vector2.up * 0.2f, 0.25f, 4, 90f), Is.EqualTo(-1));
    }

    [TestCase(0f, 1f, 0)]
    [TestCase(-1f, 0f, 1)]
    [TestCase(0f, -1f, 2)]
    [TestCase(1f, 0f, 3)]
    public void CardinalDirectionsResolveConsistently(float x, float y, int expected)
    {
        Assert.That(RadialSelectionMath.GetSlice(new Vector2(x, y), 0.1f, 4, 90f), Is.EqualTo(expected));
    }

    [Test]
    public void DiagonalBoundaryIsDeterministic()
    {
        Vector2 boundary = new(-1f, 1f);
        Assert.That(RadialSelectionMath.GetSlice(boundary, 0.1f, 4, 90f), Is.EqualTo(1));
        Assert.That(RadialSelectionMath.GetSlice(boundary, 0.1f, 4, 90f), Is.EqualTo(1));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void VariableCommandCountsReturnValidSlices(int count)
    {
        int result = RadialSelectionMath.GetSlice(new Vector2(0.7f, 0.7f), 0.1f, count, 90f);
        Assert.That(result, Is.InRange(0, count - 1));
    }

    [Test]
    public void AngularOffsetRotatesFirstSlice()
    {
        Assert.That(RadialSelectionMath.GetSlice(Vector2.right, 0.1f, 4, 0f), Is.Zero);
        Assert.That(RadialSelectionMath.GetSlice(Vector2.up, 0.1f, 4, 90f), Is.Zero);
    }

    [Test]
    public void StartedOpensAndReleaseClosesWithoutExecution()
    {
        var state = new CommandWheelStateModel();
        Assert.That(state.TryOpen(), Is.True);
        state.Select(2);
        state.ReleaseWithoutConfirm();
        Assert.That(state.IsOpen, Is.False);
        Assert.That(state.SelectedIndex, Is.EqualTo(-1));
    }

    [Test]
    public void ConfirmExecutesOneSelectionAndCloses()
    {
        var state = new CommandWheelStateModel();
        state.TryOpen();
        state.Select(3);
        Assert.That(state.Confirm(), Is.EqualTo(3));
        Assert.That(state.Confirm(), Is.EqualTo(-1));
        Assert.That(state.IsOpen, Is.False);
    }

    [Test]
    public void ConfirmWithoutSelectionDoesNothingAndStaysOpen()
    {
        var state = new CommandWheelStateModel();
        state.TryOpen();
        Assert.That(state.Confirm(), Is.EqualTo(-1));
        Assert.That(state.IsOpen, Is.True);
    }

    [Test]
    public void CancelExecutesNothingAndRequiresFreshOpenPress()
    {
        var state = new CommandWheelStateModel();
        state.TryOpen();
        state.Select(1);
        state.Cancel();
        Assert.That(state.IsOpen, Is.False);
        Assert.That(state.TryOpen(), Is.False);
        state.NotifyOpenReleased();
        Assert.That(state.TryOpen(), Is.True);
    }

    [TestCase(BuddyCommand.Follow, true, false)]
    [TestCase(BuddyCommand.Stay, false, false)]
    [TestCase(BuddyCommand.Aggressive, false, true)]
    [TestCase(BuddyCommand.Passive, false, false)]
    public void CommandAdapterAppliesExplicitState(BuddyCommand command, bool expectedFollow, bool expectedAggressive)
    {
        bool following = false;
        bool aggressive = false;
        BuddyCommandState.Apply(command, ref following, ref aggressive);
        Assert.That(following, Is.EqualTo(expectedFollow));
        Assert.That(aggressive, Is.EqualTo(expectedAggressive));
    }

    [Test]
    public void ReapplyingSameCommandIsSafe()
    {
        bool following = true;
        bool aggressive = true;
        BuddyCommandState.Apply(BuddyCommand.Follow, ref following, ref aggressive);
        BuddyCommandState.Apply(BuddyCommand.Aggressive, ref following, ref aggressive);
        Assert.That(following, Is.True);
        Assert.That(aggressive, Is.True);
    }
}
