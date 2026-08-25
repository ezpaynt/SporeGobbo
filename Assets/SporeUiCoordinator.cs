using System;
using System.Collections.Generic;
using SporeGobbo.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public interface ISporePauseScreen
{
    bool IsPauseOpen { get; }
    bool HasPauseSubpage { get; }
    void OpenPause();
    void ClosePause();
    void BackPauseSubpage();
    Selectable PauseDefaultSelectable { get; }
}

public static class SporePauseService
{
    private static readonly PauseOwnerModel owners = new();
    public static bool IsPaused => owners.IsPaused;

    public static void Acquire(UnityEngine.Object owner)
    {
        if (owner == null) return;
        owners.Acquire(owner.GetInstanceID());
        Apply();
    }

    public static void Release(UnityEngine.Object owner)
    {
        if (owner == null) return;
        Release(owner.GetInstanceID(), owner.name, owner);
    }

    internal static void Release(int ownerId, string ownerName)
    {
        Release(ownerId, ownerName, null);
    }

    private static void Release(int ownerId, string ownerName, UnityEngine.Object context)
    {
        if (!owners.Release(ownerId) && (Application.isEditor || Debug.isDebugBuild))
            Debug.LogWarning("Pause owner released without an active reason: " + ownerName, context);
        Apply();
    }

    public static void ResetAll()
    {
        owners.Clear();
        Apply();
    }

    private static void Apply() => Time.timeScale = owners.IsPaused ? 0f : 1f;
}

public static class UiFocusUtility
{
    public static bool IsValid(Selectable selectable)
    {
        return ModalFocusCandidatePolicy.IsValid(selectable != null,
            selectable != null && selectable.gameObject.activeInHierarchy,
            selectable != null && selectable.enabled,
            selectable != null && selectable.IsInteractable(), true);
    }

    public static Selectable FindFirst(GameObject root, Selectable preferred = null)
    {
        if (IsValid(preferred) && (root == null || preferred.transform.IsChildOf(root.transform))) return preferred;
        if (root == null) return null;
        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            if (IsValid(selectable)) return selectable;
        return null;
    }

    public static void Select(Selectable selectable)
    {
        if (EventSystem.current != null && IsValid(selectable))
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    public static void EnsureVisible(GameObject selected)
    {
        if (selected == null) return;
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        RectTransform item = selected.transform as RectTransform;
        RectTransform viewport = scroll != null ? (scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform) : null;
        if (scroll == null || scroll.content == null || item == null || viewport == null) return;

        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
        Rect view = viewport.rect;
        Vector2 anchored = scroll.content.anchoredPosition;
        if (bounds.max.y > view.yMax) anchored.y -= bounds.max.y - view.yMax;
        else if (bounds.min.y < view.yMin) anchored.y += view.yMin - bounds.min.y;
        scroll.content.anchoredPosition = anchored;
    }
}

[DefaultExecutionOrder(-800)]
public sealed class SporeUiCoordinator : MonoBehaviour
{
    private sealed class ModalRegistration
    {
        public UnityEngine.Object Owner;
        public int OwnerId;
        public string OwnerName;
        public Action Cancel;
        public bool Pauses;
        public Selectable DefaultSelectable;
        public Selectable PreviousSelectable;
        public GameObject ModalRoot;
    }

    private static SporeUiCoordinator instance;
    private readonly List<ModalRegistration> modals = new();
    private SporeInputReader inputReader;
    private ISporePauseScreen pauseScreen;

    public event Action PresentationChanged;

    public void NotifyPresentationChanged() => PresentationChanged?.Invoke();

    public static SporeUiCoordinator Instance => EnsureInstance();
    public static bool HasModal => instance != null && instance.modals.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => EnsureInstance();

    static SporeUiCoordinator EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<SporeUiCoordinator>();
        if (instance == null)
        {
            var host = new GameObject("Spore UI Coordinator");
            instance = host.AddComponent<SporeUiCoordinator>();
            DontDestroyOnLoad(host);
        }
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (inputReader != null)
        {
            inputReader.ActiveControlSchemeChanged -= HandleSchemeChanged;
            if (inputReader.Bindings != null) inputReader.Bindings.BindingsChanged -= NotifyPresentationChanged;
        }
        instance = null;
    }

    void Update()
    {
        AttachReader();
        if (inputReader == null) return;

        RemoveDestroyedModals();
        RefreshContext();
        SemanticUiRoute route = SemanticUiRouteDecider.Decide(
            inputReader.Context, modals.Count > 0,
            inputReader.Pause.StartedThisFrame &&
                !(inputReader.Context == SporeInputContext.Wheel && inputReader.WheelCancel.StartedThisFrame),
            inputReader.Cancel.StartedThisFrame,
            pauseScreen != null && pauseScreen.HasPauseSubpage);

        switch (route)
        {
            case SemanticUiRoute.CancelTopModal: CancelTopModal(); break;
            case SemanticUiRoute.OpenPause: OpenPause(); break;
            case SemanticUiRoute.ClosePause: ClosePause(); break;
            case SemanticUiRoute.PauseBack: pauseScreen?.BackPauseSubpage(); break;
        }

        if (inputReader.ActiveControlScheme == SporeControlScheme.Gamepad)
        {
            Selectable selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>() : null;
            if (!UiFocusUtility.IsValid(selected)) RestoreTopFocus();
        }
        if (inputReader.ActiveControlScheme == SporeControlScheme.Gamepad && EventSystem.current != null)
            UiFocusUtility.EnsureVisible(EventSystem.current.currentSelectedGameObject);
    }

    public void PushModal(UnityEngine.Object owner, Action cancel, bool pausesSimulation,
        Selectable defaultSelectable = null, GameObject modalRoot = null)
    {
        if (owner == null) return;
        BuddyCommandWheelController.Active?.CancelWithoutCommand(false);
        PopModal(owner, false);
        Selectable previous = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            ? EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>() : null;
        modals.Add(new ModalRegistration
        {
            Owner = owner,
            OwnerId = owner.GetInstanceID(),
            OwnerName = owner.name,
            Cancel = cancel,
            Pauses = pausesSimulation,
            DefaultSelectable = defaultSelectable,
            PreviousSelectable = previous,
            ModalRoot = modalRoot
        });
        if (pausesSimulation) SporePauseService.Acquire(owner);
        RefreshContext();
        SelectModalDefault(modals[modals.Count - 1]);
        StartCoroutine(SelectModalDefaultNextFrame(modals[modals.Count - 1]));
    }

    public void PopModal(UnityEngine.Object owner, bool restoreFocus = true)
    {
        for (int i = modals.Count - 1; i >= 0; i--)
        {
            if (modals[i].Owner != owner) continue;
            ModalRegistration removed = modals[i];
            modals.RemoveAt(i);
            if (removed.Pauses) SporePauseService.Release(removed.OwnerId, removed.OwnerName);
            if (restoreFocus) UiFocusUtility.Select(removed.PreviousSelectable);
            RefreshContext();
            return;
        }
    }

    private void CancelTopModal()
    {
        if (modals.Count == 0) return;
        ModalRegistration top = modals[modals.Count - 1];
        top.Cancel?.Invoke();
    }

    private void OpenPause()
    {
        BuddyCommandWheelController.Active?.CancelWithoutCommand(false);
        FindPauseScreen();
        if (pauseScreen == null) return;
        pauseScreen.OpenPause();
        RefreshContext();
        UiFocusUtility.Select(pauseScreen.PauseDefaultSelectable);
    }

    private void ClosePause()
    {
        if (pauseScreen == null) return;
        pauseScreen.ClosePause();
        RefreshContext();
    }

    private void FindPauseScreen()
    {
        if (SceneManager.GetActiveScene().name == "CampScene")
        {
            CampSceneController camp = FindAnyObjectByType<CampSceneController>();
            if (camp != null && camp.isActiveAndEnabled) { pauseScreen = camp; return; }
        }
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour is ISporePauseScreen found && behaviour.isActiveAndEnabled)
            { pauseScreen = found; return; }
    }

    private void RefreshContext()
    {
        if (inputReader == null) return;
        SporeInputContext context = modals.Count > 0 ? SporeInputContext.Modal
            : pauseScreen != null && pauseScreen.IsPauseOpen ? SporeInputContext.Pause
            : SceneManager.GetActiveScene().name == "MainMenu" ? SporeInputContext.Modal
            : inputReader.Context == SporeInputContext.Wheel ? SporeInputContext.Wheel
            : SporeInputContext.Gameplay;
        inputReader.SetContext(context);
    }

    private void AttachReader()
    {
        if (inputReader == SporeInputReader.Instance) return;
        if (inputReader != null)
        {
            inputReader.ActiveControlSchemeChanged -= HandleSchemeChanged;
            if (inputReader.Bindings != null) inputReader.Bindings.BindingsChanged -= NotifyPresentationChanged;
        }
        inputReader = SporeInputReader.Instance;
        if (inputReader != null)
        {
            inputReader.ActiveControlSchemeChanged += HandleSchemeChanged;
            if (inputReader.Bindings != null) inputReader.Bindings.BindingsChanged += NotifyPresentationChanged;
            HandleSchemeChanged(inputReader.ActiveControlScheme);
        }
    }

    private void HandleSchemeChanged(SporeControlScheme scheme)
    {
        bool keyboardMouse = scheme != SporeControlScheme.Gamepad;
        Cursor.visible = keyboardMouse;
        Cursor.lockState = CursorLockMode.None;
        if (!keyboardMouse) RestoreTopFocus();
        PresentationChanged?.Invoke();
    }

    private void RestoreTopFocus()
    {
        if (modals.Count > 0) SelectModalDefault(modals[modals.Count - 1]);
        else if (pauseScreen != null && pauseScreen.IsPauseOpen)
            UiFocusUtility.Select(pauseScreen.PauseDefaultSelectable);
    }

    private static void SelectModalDefault(ModalRegistration registration)
    {
        GameObject root = registration.ModalRoot != null ? registration.ModalRoot :
            registration.Owner is Component component ? component.gameObject : null;
        Selectable selected = UiFocusUtility.FindFirst(root, registration.DefaultSelectable);
        UiFocusUtility.Select(selected);
        if (selected == null && (Application.isEditor || Debug.isDebugBuild))
            Debug.LogWarning("Modal opened without a valid default selectable: " + registration.Owner.name, registration.Owner);
    }

    private System.Collections.IEnumerator SelectModalDefaultNextFrame(ModalRegistration registration)
    {
        yield return null;
        if (registration != null && registration.Owner != null && modals.Contains(registration))
            SelectModalDefault(registration);
    }

    private void RemoveDestroyedModals()
    {
        for (int i = modals.Count - 1; i >= 0; i--)
        {
            ModalRegistration registration = modals[i];
            if (registration.Owner != null) continue;
            modals.RemoveAt(i);
            if (registration.Pauses)
                SporePauseService.Release(registration.OwnerId, registration.OwnerName);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachReader();
        if (mode == LoadSceneMode.Single)
        {
            modals.Clear();
            pauseScreen = null;
            SporePauseService.ResetAll();
            if (inputReader != null)
                inputReader.SetContext(SceneInputContextPolicy.NormalizeAfterSingleSceneLoad(scene.name == "MainMenu"));
        }
        RefreshContext();
    }
}
