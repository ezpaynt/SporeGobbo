using UnityEngine;
using UnityEngine.UI;

public sealed class StoryEventCoordinator : MonoBehaviour
{
    private static StoryEventCoordinator instance;
    private Object activeOwner;

    public static bool HasActiveEvent => instance != null && instance.activeOwner != null;
    public static string ActiveEventId => HasActiveEvent ? instance.activeEventId : "none";
    [SerializeField] private string activeEventId = "";

    public static StoryEventCoordinator GetOrCreate()
    {
        if (instance != null) return instance;
        var host = new GameObject("Story Event Coordinator");
        instance = host.AddComponent<StoryEventCoordinator>();
        return instance;
    }

    public bool TryBegin(string eventId, Object owner, Selectable defaultSelectable, GameObject modalRoot)
    {
        if (string.IsNullOrWhiteSpace(eventId) || owner == null || activeOwner != null) return false;
        activeOwner = owner;
        activeEventId = eventId;
        SporeUiCoordinator.Instance.PushStoryModal(owner, defaultSelectable, modalRoot);
        return true;
    }

    public void Finish(Object owner)
    {
        if (owner == null || activeOwner != owner) return;
        SporeUiCoordinator.Instance.PopModal(owner);
        ClearActive();
    }

    void Update()
    {
        if (activeOwner == null && !string.IsNullOrEmpty(activeEventId)) ClearActive();
    }

    void OnDestroy()
    {
        if (activeOwner != null) SporeUiCoordinator.Instance.PopModal(activeOwner, false);
        ClearActive();
        if (instance == this) instance = null;
    }

    void ClearActive()
    {
        activeOwner = null;
        activeEventId = "";
    }
}
