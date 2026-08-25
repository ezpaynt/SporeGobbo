using UnityEngine;

public interface ICampInteractable
{
    string GetInteractPrompt();
    void Interact(GobboController player);
}

/// <summary>
/// Optional metadata for the shared world interaction authority. ICampInteractable is the
/// historical name of the common contract used by both camp and run content.
/// </summary>
public interface IWorldInteractionMetadata
{
    bool CanInteract(GobboController player);
    Vector2 GetInteractionPoint();
    int InteractionPriority { get; }
    float InteractionRange { get; }
}
