using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SporeGobbo.Input
{
    /// <summary>
    /// Authoritative runtime input boundary. It exposes semantic intent, context availability,
    /// active control scheme, binding services, and development-only debug input.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class SporeInputReader : MonoBehaviour
    {
        private const double DefaultBufferSeconds = 0.12d;
        private const float MouseMovementThresholdPixels = 2f;
        private const float AnalogActivityThreshold = 0.2f;

        private static SporeInputReader instance;

        [SerializeField] private InputActionAsset sourceAsset;

        private InputActionAsset actions;
        private InputActionMap gameplayMap;
        private InputActionMap uiMap;
        private InputActionMap wheelMap;
        private InputActionMap debugMap;
        private InputAction moveAction;
        private InputAction aimPointerAction;
        private InputAction aimStickAction;
        private InputAction primaryAttackAction;
        private InputAction secondaryAbilityAction;
        private InputAction ultimateAction;
        private InputAction digAction;
        private InputAction dashAction;
        private InputAction interactAction;
        private InputAction commandWheelAction;
        private InputAction plantSporeAction;
        private InputAction pauseAction;
        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction cancelAction;
        private InputAction wheelSelectPointerAction;
        private InputAction wheelSelectStickAction;
        private InputAction wheelConfirmAction;
        private InputAction wheelCancelAction;
        private InputAction testZoomAction;
        private Vector2 lastObservedPointer;
        private bool hasObservedPointer;
        private bool suppressGameplayButtonsUntilReleased;

        public static SporeInputReader Instance => instance;

        public Vector2 Move { get; private set; }
        public Vector2 AimPointer { get; private set; }
        public Vector2 AimStick { get; private set; }

        public SemanticButtonState PrimaryAttack { get; private set; }
        public SemanticButtonState SecondaryAbility { get; private set; }
        public SemanticButtonState Ultimate { get; private set; }
        public SemanticButtonState Dig { get; private set; }
        public SemanticButtonState Dash { get; private set; }
        public SemanticButtonState Interact { get; private set; }
        public SemanticButtonState CommandWheel { get; private set; }
        public SemanticButtonState PlantSpore { get; private set; }
        public SemanticButtonState Pause { get; private set; }
        public Vector2 Navigate { get; private set; }
        public SemanticButtonState Submit { get; private set; }
        public SemanticButtonState Cancel { get; private set; }
        public Vector2 WheelSelectPointer { get; private set; }
        public Vector2 WheelSelectStick { get; private set; }
        public SemanticButtonState WheelConfirm { get; private set; }
        public SemanticButtonState WheelCancel { get; private set; }
        public Vector2 DebugTestZoom { get; private set; }
        public bool GameplayButtonsSuppressed => suppressGameplayButtonsUntilReleased;

        public SporeInputContext Context => ContextRouter.Current;
        public SporeControlScheme ActiveControlScheme { get; private set; } = SporeControlScheme.Unknown;
        public SporeInputContextRouter ContextRouter { get; private set; }
        public SemanticInputBuffer Buffer { get; } = new();
        public SporeBindingService Bindings { get; private set; }

        public event Action<SporeControlScheme> ActiveControlSchemeChanged;
        public event Action<SporeInputContext, SporeInputContext> ContextChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Duplicate SporeInputReader destroyed. Input has one authoritative runtime boundary.", this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (sourceAsset == null)
            {
                Debug.LogError("SporeInputReader requires the authoritative InputActionAsset.", this);
                enabled = false;
                return;
            }

            actions = Instantiate(sourceAsset);
            actions.name = sourceAsset.name + " (Runtime)";
            CacheRequiredActions();
            Bindings = new SporeBindingService(actions);
            Bindings.BindingsChanged += HandleBindingsChanged;
            ContextRouter = new SporeInputContextRouter();
            ContextRouter.Changed += HandleContextChanged;
            SubscribeDeviceObservation();
            ApplyContextPolicy();
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            if (ContextRouter != null)
                ContextRouter.Changed -= HandleContextChanged;

            UnsubscribeDeviceObservation();
            if (Bindings != null)
            {
                Bindings.BindingsChanged -= HandleBindingsChanged;
                Bindings.Dispose();
                Bindings = null;
            }
            if (actions != null)
                Destroy(actions);
            actions = null;
            instance = null;
        }

        private void Update()
        {
            if (actions == null)
                return;

            Move = ReadVector(moveAction);
            AimPointer = ReadVector(aimPointerAction);
            AimStick = ReadVector(aimStickAction);

            PrimaryAttack = ReadButton(primaryAttackAction);
            SecondaryAbility = ReadButton(secondaryAbilityAction);
            Ultimate = ReadButton(ultimateAction);
            Dig = ReadButton(digAction);
            Dash = ReadButton(dashAction);
            Interact = ReadButton(interactAction);
            CommandWheel = ReadButton(commandWheelAction);
            PlantSpore = ReadButton(plantSporeAction);
            Pause = ReadButton(pauseAction);
            Navigate = ReadVector(navigateAction);
            Submit = ReadButton(submitAction);
            Cancel = ReadButton(cancelAction);
            WheelSelectPointer = ReadVector(wheelSelectPointerAction);
            WheelSelectStick = ReadVector(wheelSelectStickAction);
            WheelConfirm = ReadButton(wheelConfirmAction);
            WheelCancel = ReadButton(wheelCancelAction);
            DebugTestZoom = ReadVector(testZoomAction);

            if (suppressGameplayButtonsUntilReleased)
            {
                bool anyWorldButtonHeld = primaryAttackAction.IsPressed() || secondaryAbilityAction.IsPressed() ||
                    ultimateAction.IsPressed() || digAction.IsPressed() || dashAction.IsPressed() ||
                    interactAction.IsPressed() || commandWheelAction.IsPressed() || plantSporeAction.IsPressed();
                PrimaryAttack = SemanticButtonState.None;
                SecondaryAbility = SemanticButtonState.None;
                Ultimate = SemanticButtonState.None;
                Dig = SemanticButtonState.None;
                Dash = SemanticButtonState.None;
                Interact = SemanticButtonState.None;
                CommandWheel = SemanticButtonState.None;
                PlantSpore = SemanticButtonState.None;
                if (!anyWorldButtonHeld) suppressGameplayButtonsUntilReleased = false;
            }

            if (PrimaryAttack.StartedThisFrame)
                Buffer.Record(BufferedInputAction.PrimaryAttack, Time.unscaledTimeAsDouble, DefaultBufferSeconds);
            if (Dash.StartedThisFrame)
                Buffer.Record(BufferedInputAction.Dash, Time.unscaledTimeAsDouble, DefaultBufferSeconds);
        }

        public bool SetContext(SporeInputContext context)
        {
            return ContextRouter != null && ContextRouter.SetContext(context);
        }

        public bool TryGetPointerWorldPosition(Camera worldCamera, out Vector2 worldPosition)
        {
            worldPosition = default;
            if (worldCamera == null)
                return false;

            Vector3 projected = worldCamera.ScreenToWorldPoint(AimPointer);
            worldPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        public string GetInteractBindingDisplay()
        {
            return Bindings == null ? "Interact" : Bindings.GetDisplay("Gameplay", "Interact",
                ActiveControlScheme == SporeControlScheme.Gamepad ? BindingScheme.Gamepad : BindingScheme.KeyboardMouse);
        }

        private void HandleBindingsChanged()
        {
            Buffer.Clear();
            ClearFrameState();
        }

        private void CacheRequiredActions()
        {
            gameplayMap = actions.FindActionMap("Gameplay", true);
            uiMap = actions.FindActionMap("UI", true);
            wheelMap = actions.FindActionMap("Wheel", true);
            debugMap = actions.FindActionMap("Debug", true);
            moveAction = gameplayMap.FindAction("Move", true);
            aimPointerAction = gameplayMap.FindAction("AimPointer", true);
            aimStickAction = gameplayMap.FindAction("AimStick", true);
            primaryAttackAction = gameplayMap.FindAction("PrimaryAttack", true);
            secondaryAbilityAction = gameplayMap.FindAction("SecondaryAbility", true);
            ultimateAction = gameplayMap.FindAction("Ultimate", true);
            digAction = gameplayMap.FindAction("Dig", true);
            dashAction = gameplayMap.FindAction("Dash", true);
            interactAction = gameplayMap.FindAction("Interact", true);
            commandWheelAction = gameplayMap.FindAction("CommandWheel", true);
            plantSporeAction = gameplayMap.FindAction("PlantSpore", true);
            pauseAction = gameplayMap.FindAction("Pause", true);
            navigateAction = uiMap.FindAction("Navigate", true);
            submitAction = uiMap.FindAction("Submit", true);
            cancelAction = uiMap.FindAction("Cancel", true);
            wheelSelectPointerAction = wheelMap.FindAction("WheelSelectPointer", true);
            wheelSelectStickAction = wheelMap.FindAction("WheelSelectStick", true);
            wheelConfirmAction = wheelMap.FindAction("WheelConfirm", true);
            wheelCancelAction = wheelMap.FindAction("WheelCancel", true);
            testZoomAction = debugMap.FindAction("TestZoom", true);
        }

        private static Vector2 ReadVector(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private static SemanticButtonState ReadButton(InputAction action)
        {
            if (action == null || !action.enabled)
                return SemanticButtonState.None;

            return new SemanticButtonState(
                action.WasPressedThisFrame(),
                action.IsPressed(),
                action.WasReleasedThisFrame());
        }

        private void HandleContextChanged(SporeInputContext previous, SporeInputContext next)
        {
            Buffer.Clear();
            ClearFrameState();
            if (next == SporeInputContext.Gameplay && previous != SporeInputContext.Gameplay)
                suppressGameplayButtonsUntilReleased = true;
            ApplyContextPolicy();
            ContextChanged?.Invoke(previous, next);
        }

        private void ApplyContextPolicy()
        {
            if (actions == null || ContextRouter == null)
                return;

            SporeInputAvailability availability = ContextRouter.GetAvailability();

            if (availability.GameplayMap)
            {
                gameplayMap.Enable();

                SetEnabled(moveAction, availability.Move);
                SetEnabled(aimPointerAction, availability.Aim);
                SetEnabled(aimStickAction, availability.Aim);
                SetEnabled(primaryAttackAction, availability.GameplayWorldActions);
                SetEnabled(secondaryAbilityAction, availability.GameplayWorldActions);
                SetEnabled(ultimateAction, availability.GameplayWorldActions);
                SetEnabled(digAction, availability.GameplayWorldActions);
                SetEnabled(dashAction, availability.GameplayWorldActions);
                SetEnabled(interactAction, availability.GameplayWorldActions);
                SetEnabled(plantSporeAction, availability.GameplayWorldActions);
                SetEnabled(commandWheelAction, availability.CommandWheel);
                SetEnabled(pauseAction, availability.Pause);
            }
            else gameplayMap.Disable();

            SetEnabled(wheelMap, availability.WheelMap);
            SetEnabled(uiMap, availability.UiMap);

            if (Application.isEditor || Debug.isDebugBuild)
                debugMap.Enable();
        }

        private static void SetEnabled(InputAction action, bool enabled)
        {
            if (enabled) action.Enable();
            else action.Disable();
        }

        private static void SetEnabled(InputActionMap map, bool enabled)
        {
            if (enabled) map.Enable();
            else map.Disable();
        }

        private void ClearFrameState()
        {
            Move = Vector2.zero;
            AimPointer = Vector2.zero;
            AimStick = Vector2.zero;
            PrimaryAttack = SemanticButtonState.None;
            SecondaryAbility = SemanticButtonState.None;
            Ultimate = SemanticButtonState.None;
            Dig = SemanticButtonState.None;
            Dash = SemanticButtonState.None;
            Interact = SemanticButtonState.None;
            CommandWheel = SemanticButtonState.None;
            PlantSpore = SemanticButtonState.None;
            Pause = SemanticButtonState.None;
            Navigate = Vector2.zero;
            Submit = SemanticButtonState.None;
            Cancel = SemanticButtonState.None;
            WheelSelectPointer = Vector2.zero;
            WheelSelectStick = Vector2.zero;
            WheelConfirm = SemanticButtonState.None;
            WheelCancel = SemanticButtonState.None;
            DebugTestZoom = Vector2.zero;
        }

        private void SubscribeDeviceObservation()
        {
            if (actions == null)
                return;

            foreach (InputActionMap map in actions.actionMaps)
            foreach (InputAction action in map.actions)
            {
                action.started += ObserveDevice;
                action.performed += ObserveDevice;
            }
        }

        private void UnsubscribeDeviceObservation()
        {
            if (actions == null)
                return;

            foreach (InputActionMap map in actions.actionMaps)
            foreach (InputAction action in map.actions)
            {
                action.started -= ObserveDevice;
                action.performed -= ObserveDevice;
            }
        }

        private void ObserveDevice(InputAction.CallbackContext callback)
        {
            InputDevice device = callback.control?.device;
            if (device is Gamepad)
            {
                if (callback.action.expectedControlType == "Vector2" &&
                    callback.ReadValue<Vector2>().sqrMagnitude < AnalogActivityThreshold * AnalogActivityThreshold)
                    return;

                SetActiveControlScheme(SporeControlScheme.Gamepad);
                return;
            }

            if (device is Mouse && (callback.action == aimPointerAction || callback.action == wheelSelectPointerAction))
            {
                Vector2 pointer = callback.ReadValue<Vector2>();
                if (hasObservedPointer &&
                    (pointer - lastObservedPointer).sqrMagnitude < MouseMovementThresholdPixels * MouseMovementThresholdPixels)
                    return;

                lastObservedPointer = pointer;
                hasObservedPointer = true;
            }

            if (device is Keyboard || device is Mouse)
                SetActiveControlScheme(SporeControlScheme.KeyboardMouse);
        }

        private void SetActiveControlScheme(SporeControlScheme scheme)
        {
            if (scheme == ActiveControlScheme)
                return;

            ActiveControlScheme = scheme;
            ActiveControlSchemeChanged?.Invoke(scheme);
        }
    }
}
