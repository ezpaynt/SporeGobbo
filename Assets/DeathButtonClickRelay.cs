using UnityEngine;
using UnityEngine.EventSystems;

public class DeathButtonClickRelay : MonoBehaviour, IPointerClickHandler
{
    public enum DeathButtonAction
    {
        AcceptMarkedSuccessor,
        LetCampChoose,
        ReturnToMainMenu
    }

    [SerializeField] private CampSuccessionUI owner;
    [SerializeField] private DeathButtonAction action;
    [SerializeField] private bool relayEnabled = true;
    public bool logDebugMessages = false;

    public void Configure(CampSuccessionUI newOwner, DeathButtonAction newAction, bool enabled)
    {
        Configure(newOwner, newAction, enabled, false);
    }

    public void Configure(CampSuccessionUI newOwner, DeathButtonAction newAction, bool enabled, bool debug)
    {
        owner = newOwner;
        action = newAction;
        relayEnabled = enabled;
        logDebugMessages = debug;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!relayEnabled)
            return;

        if (owner == null)
        {
            if (logDebugMessages)
                Debug.LogWarning($"[DeathButtonClickRelay] Missing CampSuccessionUI owner on {name}.");
            return;
        }

        if (logDebugMessages)
            Debug.Log($"[DeathButtonClickRelay] Clicked {action} on {name}.");

        switch (action)
        {
            case DeathButtonAction.AcceptMarkedSuccessor:
                owner.AcceptMarkedSuccessor();
                break;
            case DeathButtonAction.LetCampChoose:
                owner.LetCampChoose();
                break;
            case DeathButtonAction.ReturnToMainMenu:
                owner.ReturnToMainMenu();
                break;
        }
    }
}
