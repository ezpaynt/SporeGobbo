using System;
using UnityEngine;

/// <summary>
/// Shared modal lock for camp UI.
/// While a camp menu is open, the player is frozen and CampInteractionDetector stops opening new objects.
/// Pressing E or Escape can close the current modal through CloseCurrent().
/// </summary>
public static class CampMenuModal
{
    public static bool IsOpen { get; private set; }
    public static UnityEngine.Object CurrentOwner { get; private set; }

    private static Action currentCloseAction;
    private static GobboController lockedPlayer;
    private static Rigidbody2D lockedRb;
    private static bool previousPlayerEnabled = true;
    private static bool closingNow = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticsOnEnterPlayMode()
    {
        IsOpen = false;
        CurrentOwner = null;
        currentCloseAction = null;
        lockedPlayer = null;
        lockedRb = null;
        previousPlayerEnabled = true;
        closingNow = false;
    }

    public static void Open(GobboController player, UnityEngine.Object owner, Action closeAction = null)
    {
        if (IsOpen && CurrentOwner == owner)
            return;

        if (IsOpen)
            CloseCurrent();

        CurrentOwner = owner;
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

        IsOpen = true;
    }

    public static void Close(UnityEngine.Object owner)
    {
        if (!IsOpen)
            return;

        if (CurrentOwner != null && owner != null && CurrentOwner != owner)
            return;

        UnlockPlayerOnly();

        CurrentOwner = null;
        currentCloseAction = null;
        IsOpen = false;
    }

    public static void CloseCurrent()
    {
        if (!IsOpen || closingNow)
            return;

        closingNow = true;

        Action close = currentCloseAction;
        if (close != null)
            close.Invoke();
        else
            Close(CurrentOwner);

        closingNow = false;
    }

    public static bool IsOwnedBy(UnityEngine.Object owner)
    {
        return IsOpen && CurrentOwner == owner;
    }

    public static void ForceClear()
    {
        UnlockPlayerOnly();

        CurrentOwner = null;
        currentCloseAction = null;
        IsOpen = false;
        closingNow = false;
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
