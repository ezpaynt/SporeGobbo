using System;
using UnityEngine.InputSystem;

namespace SporeGobbo.Input
{
    public enum BindingScheme { KeyboardMouse, Gamepad }

    public readonly struct BindingConflict
    {
        public BindingConflict(Guid targetId, Guid conflictingId, string controlPath, string conflictingAction)
        { TargetBindingId = targetId; ConflictingBindingId = conflictingId; ControlPath = controlPath; ConflictingAction = conflictingAction; }
        public Guid TargetBindingId { get; }
        public Guid ConflictingBindingId { get; }
        public string ControlPath { get; }
        public string ConflictingAction { get; }
    }

    public static class SporeBindingRules
    {
        public static bool IsSameContextConflict(InputAction target, InputAction other, string targetPath, string otherPath)
        {
            if (target == null || other == null || target.actionMap == null || other.actionMap == null) return false;
            return target.actionMap == other.actionMap &&
                   !string.IsNullOrEmpty(targetPath) &&
                   string.Equals(targetPath, otherPath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsControlAllowed(BindingScheme scheme, string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath)) return false;
            return scheme == BindingScheme.Gamepad
                ? controlPath.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase)
                : controlPath.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
                  controlPath.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase);
        }

        public static string FriendlyDisplay(string display)
        {
            if (string.IsNullOrWhiteSpace(display)) return "Unbound";
            return display.Replace("Left Button", "LMB").Replace("Right Button", "RMB")
                .Replace("Right Trigger", "RT").Replace("Left Trigger", "LT")
                .Replace("Right Shoulder", "RB").Replace("Left Shoulder", "LB")
                .Replace("Button South", "A").Replace("Button East", "B")
                .Replace("Button West", "X").Replace("Button North", "Y")
                .Replace("Start", "Menu");
        }
    }
}
