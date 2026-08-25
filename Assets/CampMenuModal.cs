using System;
using SporeGobbo.Input;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared modal lock for camp UI.
/// While a camp menu is open, the player is frozen and CampInteractionDetector stops opening new objects.
/// Escape can close the current modal through CloseCurrent().
/// </summary>
public static class CampMenuModal
{
    private static bool isOpen;
    private static UnityEngine.Object currentOwner;
    public static bool IsOpen { get { ValidateCurrentOwner(); return isOpen; } }
    public static UnityEngine.Object CurrentOwner { get { ValidateCurrentOwner(); return currentOwner; } }

    private static Action currentCloseAction;
    private static GobboController lockedPlayer;
    private static Rigidbody2D lockedRb;
    private static bool previousPlayerEnabled = true;
    private static bool closingNow = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsOnEnterPlayMode()
    {
        isOpen = false;
        currentOwner = null;
        currentCloseAction = null;
        lockedPlayer = null;
        lockedRb = null;
        previousPlayerEnabled = true;
        closingNow = false;
    }

    public static void Open(GobboController player, UnityEngine.Object owner, Action closeAction = null,
        Selectable defaultSelectable = null, GameObject modalRoot = null)
    {
        ValidateCurrentOwner();
        if (isOpen && currentOwner == owner)
            return;

        if (isOpen)
            CloseCurrent();

        currentOwner = owner;
        currentCloseAction = closeAction;
        lockedPlayer = player != null ? player : UnityEngine.Object.FindAnyObjectByType<GobboController>();

        if (lockedPlayer != null)
        {
            previousPlayerEnabled = lockedPlayer.enabled;
            lockedPlayer.enabled = false;

            lockedRb = lockedPlayer.GetComponent<Rigidbody2D>();
            if (lockedRb != null)
                lockedRb.linearVelocity = Vector2.zero;
        }

        isOpen = true;
        SporeUiCoordinator.Instance.PushModal(owner, closeAction, false, defaultSelectable, modalRoot);
    }

    public static void Close(UnityEngine.Object owner)
    {
        ValidateCurrentOwner();
        if (!isOpen)
            return;

        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        UnlockPlayerOnly();
        SporeUiCoordinator.Instance.PopModal(owner);

        currentOwner = null;
        currentCloseAction = null;
        isOpen = false;
    }

    public static void CloseCurrent()
    {
        ValidateCurrentOwner();
        if (!isOpen || closingNow)
            return;

        closingNow = true;

        Action close = currentCloseAction;
        if (close != null)
            close.Invoke();
        else
            Close(currentOwner);

        closingNow = false;
    }

    public static bool IsOwnedBy(UnityEngine.Object owner)
    {
        ValidateCurrentOwner();
        return isOpen && currentOwner == owner;
    }

    public static void ForceClear()
    {
        UnlockPlayerOnly();

        UnityEngine.Object owner = currentOwner;
        if (owner != null)
            SporeUiCoordinator.Instance.PopModal(owner, false);

        currentOwner = null;
        currentCloseAction = null;
        isOpen = false;
        closingNow = false;
    }

    static void ValidateCurrentOwner()
    {
        bool ownerExists = currentOwner != null;
        bool ownerActive = ownerExists && IsOwnerActive(currentOwner);
        if (ModalLifecyclePolicy.ShouldForceClear(isOpen, ownerExists, ownerActive))
            ForceClear();
    }

    static bool IsOwnerActive(UnityEngine.Object owner)
    {
        if (owner is Component component) return component.gameObject.activeInHierarchy;
        if (owner is GameObject gameObject) return gameObject.activeInHierarchy;
        return owner != null;
    }

    static void UnlockPlayerOnly()
    {
        if (lockedPlayer != null)
            lockedPlayer.enabled = previousPlayerEnabled;

        if (lockedRb != null)
            lockedRb.linearVelocity = Vector2.zero;

        lockedPlayer = null;
        lockedRb = null;
        previousPlayerEnabled = true;
    }
}
