using UnityEngine;
using System.Collections.Generic;
using SporeGobbo.CampLifecycle;

public readonly struct TerrainDigResult
{
    public readonly int EvaluatedCells;
    public readonly int EligibleCells;
    public readonly int RemovedCells;
    public readonly TerrainDigFailureReason FailureReason;
    public bool Changed => RemovedCells > 0;
    public bool Succeeded => FailureReason == TerrainDigFailureReason.None;
    public TerrainDigResult(int evaluatedCells, int eligibleCells, int removedCells, TerrainDigFailureReason failureReason)
    {
        EvaluatedCells = evaluatedCells;
        EligibleCells = eligibleCells;
        RemovedCells = removedCells;
        FailureReason = failureReason;
    }
    public TerrainDigResult(int evaluatedCells, int removedCells)
        : this(evaluatedCells, removedCells, removedCells,
            removedCells > 0 ? TerrainDigFailureReason.None : TerrainDigFailureReason.NoDirtRemoved) { }
}

public enum TerrainDigFailureReason
{
    None,
    MissingTerrainAuthority,
    NoEvaluatedCells,
    AuthorizationRejected,
    NoDirtRemoved
}

/// <summary>
/// Narrow scene-terrain boundary used by player digging. Procedural generation remains owned by MapGenerator.
/// A future handcrafted Camp terrain can implement this without depending on Run generation.
/// </summary>
public interface IDiggableTerrain
{
    float CellSize { get; }
    void DigCircle(Vector2 worldPosition, float radius);
    TerrainDigResult DigCircle(Vector2 worldPosition, float radius, TerrainDigAuthority authority,
        int residentialStage, IReadOnlyCollection<Vector2Int> authorizedCells);
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
