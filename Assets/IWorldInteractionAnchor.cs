using UnityEngine;
/// <summary>Logical interaction anchor, independent of an art collider's dimensions.</summary>
public interface IWorldInteractionAnchor { Vector2 InteractionPoint { get; } }

/// <summary>Derived render-space caption anchor; never used for range, navigation or physics.</summary>
public interface IProjectedInteractionAnchor { Vector3 ProjectedPromptAnchor { get; } }
