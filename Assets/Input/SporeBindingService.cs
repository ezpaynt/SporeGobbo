using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SporeGobbo.Input
{
    public sealed class SporeBindingService : IDisposable
    {
        public const string PreferencesKey = "SporeGobbo.InputBindingOverrides.v1";
        readonly InputActionAsset actions;
        InputActionRebindingExtensions.RebindingOperation operation;
        InputAction pendingAction;
        int pendingIndex = -1;
        string pendingPath;
        BindingConflict pendingConflict;
        bool pendingWasEnabled;
        bool pendingUiWasEnabled;
        string pendingOriginalOverridePath;

        public SporeBindingService(InputActionAsset actions, bool loadSaved = true)
        {
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (loadSaved) Load();
        }

        public bool IsRebinding => operation != null;
        public bool HasPendingConflict => pendingAction != null && pendingIndex >= 0 && !string.IsNullOrEmpty(pendingPath);
        public event Action BindingsChanged;
        public event Action<InputAction, Guid> RebindStarted;
        public event Action RebindCanceled;
        public event Action<BindingConflict> ConflictFound;

        public InputAction FindAction(string map, string action) => actions.FindActionMap(map, true).FindAction(action, true);

        public int FindBindingIndex(InputAction action, Guid bindingId)
        {
            if (action == null) return -1;
            for (int i = 0; i < action.bindings.Count; i++) if (action.bindings[i].id == bindingId) return i;
            return -1;
        }

        public string GetDisplay(string map, string actionName, BindingScheme scheme, string partName = null)
        {
            InputAction action = FindAction(map, actionName);
            string group = scheme.ToString();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!string.IsNullOrEmpty(partName) && !string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!BindingHasGroup(binding, group)) continue;
                return SporeBindingRules.FriendlyDisplay(action.GetBindingDisplayString(i));
            }
            return "—";
        }

        public Guid GetBindingId(string map, string actionName, BindingScheme scheme, string partName = null)
        {
            InputAction action = FindAction(map, actionName);
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!string.IsNullOrEmpty(partName) && !string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase)) continue;
                if (BindingHasGroup(binding, scheme.ToString())) return binding.id;
            }
            return Guid.Empty;
        }

        public bool BeginRebind(string map, string actionName, Guid bindingId, BindingScheme scheme)
        {
            CancelRebind();
            InputAction action = FindAction(map, actionName);
            int index = FindBindingIndex(action, bindingId);
            if (index < 0 || action.bindings[index].isComposite) return false;
            string group = scheme.ToString();
            if (!BindingHasGroup(action.bindings[index], group)) return false;

            pendingAction = action; pendingIndex = index; pendingPath = null; pendingWasEnabled = action.enabled;
            pendingOriginalOverridePath = action.bindings[index].overridePath;
            InputActionMap ui = actions.FindActionMap("UI", false); pendingUiWasEnabled = ui != null && ui.enabled; ui?.Disable();
            action.Disable();
            operation = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithCancelingThrough("<Gamepad>/buttonEast")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Gamepad>/start")
                .WithControlsExcluding(scheme == BindingScheme.Gamepad ? "<Keyboard>/*" : "<Gamepad>/*")
                .WithControlsExcluding(scheme == BindingScheme.Gamepad ? "<Mouse>/*" : "<Joystick>/*")
                .OnCancel(_ => FinishCanceled())
                .OnComplete(_ => FinishCandidate(scheme));
            RebindStarted?.Invoke(action, bindingId);
            operation.Start();
            return true;
        }

        void FinishCandidate(BindingScheme scheme)
        {
            string path = pendingAction.bindings[pendingIndex].overridePath;
            DisposeOperation();
            if (!SporeBindingRules.IsControlAllowed(scheme, path)) { ClearPendingOverride(); FinishCanceled(); return; }
            InputAction conflictAction = null; int conflictIndex = -1;
            foreach (InputAction other in pendingAction.actionMap.actions)
            for (int i = 0; i < other.bindings.Count; i++)
            {
                if (other == pendingAction && i == pendingIndex) continue;
                InputBinding binding = other.bindings[i];
                if (binding.isComposite || !SporeBindingRules.IsSameContextConflict(pendingAction, other, path, binding.effectivePath)) continue;
                conflictAction = other; conflictIndex = i; break;
            }
            if (conflictAction != null)
            {
                pendingPath = path;
                RestoreOriginalOverride(); RestoreEnabledStates();
                pendingConflict = new BindingConflict(pendingAction.bindings[pendingIndex].id,
                    conflictAction.bindings[conflictIndex].id, path, conflictAction.name);
                ConflictFound?.Invoke(pendingConflict);
                return;
            }
            RestoreEnabledStates(); CompleteAndSave();
        }

        public void ResolveConflict(bool replace)
        {
            if (!HasPendingConflict) return;
            if (replace)
            {
                foreach (InputAction other in pendingAction.actionMap.actions)
                for (int i = 0; i < other.bindings.Count; i++)
                    if (other.bindings[i].id == pendingConflict.ConflictingBindingId) other.ApplyBindingOverride(i, "");
                pendingAction.ApplyBindingOverride(pendingIndex, pendingPath);
                RestoreEnabledStates(); CompleteAndSave();
            }
            else FinishCanceled();
        }

        public void CancelRebind()
        {
            if (operation != null) { operation.Cancel(); return; }
            if (HasPendingConflict) FinishCanceled();
        }

        void ClearPendingOverride()
        {
            RestoreOriginalOverride(); RestoreEnabledStates();
        }
        void RestoreOriginalOverride()
        {
            if (pendingAction == null || pendingIndex < 0) return;
            if (string.IsNullOrEmpty(pendingOriginalOverridePath)) pendingAction.RemoveBindingOverride(pendingIndex);
            else pendingAction.ApplyBindingOverride(pendingIndex, pendingOriginalOverridePath);
        }
        void RestoreEnabledStates()
        {
            if (pendingAction != null && pendingWasEnabled) pendingAction.Enable();
            InputActionMap ui = actions.FindActionMap("UI", false); if (ui != null && pendingUiWasEnabled) ui.Enable();
        }
        void FinishCanceled() { DisposeOperation(); ClearPendingOverride(); ClearPending(); RebindCanceled?.Invoke(); }
        void CompleteAndSave() { Save(); ClearPending(); BindingsChanged?.Invoke(); }
        void ClearPending() { pendingAction = null; pendingIndex = -1; pendingPath = null; pendingConflict = default; pendingWasEnabled = false; pendingUiWasEnabled = false; pendingOriginalOverridePath = null; }
        void DisposeOperation() { operation?.Dispose(); operation = null; }

        public string SaveJson() => actions.SaveBindingOverridesAsJson();
        public void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            if (!json.TrimStart().StartsWith("{") || !json.Contains("\"bindings\"")) throw new FormatException("Binding override JSON is invalid.");
            actions.LoadBindingOverridesFromJson(json, true); BindingsChanged?.Invoke();
        }
        public void Save() { PlayerPrefs.SetString(PreferencesKey, SaveJson()); PlayerPrefs.Save(); }
        public void Load()
        {
            string json = PlayerPrefs.GetString(PreferencesKey, "");
            if (string.IsNullOrWhiteSpace(json)) return;
            try { LoadJson(json); }
            catch (Exception ex) { actions.RemoveAllBindingOverrides(); PlayerPrefs.DeleteKey(PreferencesKey); Debug.LogWarning("Invalid saved controls were reset: " + ex.Message); }
        }
        public void ResetAll()
        {
            CancelRebind(); actions.RemoveAllBindingOverrides(); PlayerPrefs.DeleteKey(PreferencesKey); PlayerPrefs.Save(); BindingsChanged?.Invoke();
        }
        public void Dispose() { CancelRebind(); DisposeOperation(); }
        static bool BindingHasGroup(InputBinding binding, string group) =>
            !string.IsNullOrEmpty(binding.groups) && Array.Exists(binding.groups.Split(';'), value => value == group);
    }
}
