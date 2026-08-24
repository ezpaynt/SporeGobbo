using UnityEngine;

/// <summary>
/// Narrow scene-terrain boundary used by player digging. Procedural generation remains owned by MapGenerator.
/// A future handcrafted Camp terrain can implement this without depending on Run generation.
/// </summary>
public interface IDiggableTerrain
{
    float CellSize { get; }
    void DigCircle(Vector2 worldPosition, float radius);
    bool IsBlocked(Vector2Int cell);
    bool IsDiggable(Vector2Int cell);
    Vector2Int WorldToCell(Vector2 worldPosition);
    Vector2 CellToWorld(Vector2Int cell);
}

public static class DiggableTerrainService
{
    public static IDiggableTerrain Active { get; private set; }

    public static void Register(IDiggableTerrain terrain)
    {
        if (terrain != null) Active = terrain;
    }

    public static void Unregister(IDiggableTerrain terrain)
    {
        if (ReferenceEquals(Active, terrain)) Active = null;
    }
}