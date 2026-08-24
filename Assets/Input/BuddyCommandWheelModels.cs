using UnityEngine;

namespace SporeGobbo.Input
{
    public enum BuddyCommand
    {
        Follow,
        Stay,
        Aggressive,
        Passive
    }

    public static class BuddyCommandState
    {
        public static void Apply(BuddyCommand command, ref bool following, ref bool aggressive)
        {
            switch (command)
            {
                case BuddyCommand.Follow: following = true; break;
                case BuddyCommand.Stay: following = false; break;
                case BuddyCommand.Aggressive: aggressive = true; break;
                case BuddyCommand.Passive: aggressive = false; break;
            }
        }
    }

    public static class RadialSelectionMath
    {
        public static int GetSlice(Vector2 selection, float deadZone, int sliceCount, float firstSliceAngleDegrees)
        {
            if (sliceCount <= 0 || selection.sqrMagnitude < deadZone * deadZone)
                return -1;

            float angle = Mathf.Atan2(selection.y, selection.x) * Mathf.Rad2Deg;
            float sliceAngle = 360f / sliceCount;
            float relative = Mathf.Repeat(angle - firstSliceAngleDegrees + sliceAngle * 0.5f, 360f);
            return Mathf.FloorToInt(relative / sliceAngle) % sliceCount;
        }
    }

    public sealed class CommandWheelStateModel
    {
        public bool IsOpen { get; private set; }
        public bool AwaitingOpenRelease { get; private set; }
        public int SelectedIndex { get; private set; } = -1;

        public bool TryOpen()
        {
            if (IsOpen || AwaitingOpenRelease) return false;
            IsOpen = true;
            SelectedIndex = -1;
            return true;
        }

        public void Select(int index) { if (IsOpen) SelectedIndex = index; }

        public int Confirm()
        {
            if (!IsOpen || SelectedIndex < 0) return -1;
            int selected = SelectedIndex;
            CloseAndRequireRelease();
            return selected;
        }

        public void Cancel()
        {
            if (IsOpen) CloseAndRequireRelease();
        }

        public void ReleaseWithoutConfirm()
        {
            if (IsOpen) CloseAndRequireRelease();
            AwaitingOpenRelease = false;
        }

        public void NotifyOpenReleased() => AwaitingOpenRelease = false;

        private void CloseAndRequireRelease()
        {
            IsOpen = false;
            SelectedIndex = -1;
            AwaitingOpenRelease = true;
        }
    }
}
