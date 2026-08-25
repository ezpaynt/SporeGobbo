using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;

/// <summary>Reusable buddy-owned Dig action. Terrain remains responsible for permission and mutation.</summary>
public sealed class BuddyDigAbility : MonoBehaviour
{
    [Min(0.1f)] public float digRadius = 0.72f;
    [Min(0f)] public float digDuration = 0.18f;
    public event Action<TerrainDigResult> DigCompleted;

    BuddyDirectionalSprite directionalSprite;
    GobboVisualController visualController;
    IDiggableTerrain boundTerrain;

    public IDiggableTerrain ResolvedTerrain => boundTerrain ?? DiggableTerrainService.Active;
    public void BindTerrain(IDiggableTerrain terrain) => boundTerrain = terrain;

    void Awake()
    {
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null) visualController = GetComponentInChildren<GobboVisualController>();
    }

    public TerrainDigResult Dig(Vector2 target, TerrainDigAuthority authority = TerrainDigAuthority.Buddy,
        int residentialStage = 0, IReadOnlyCollection<Vector2Int> authorizedCells = null)
    {
        Face(target);
        visualController?.SetAnimationState(GobboAnimationState.Dig);
        IDiggableTerrain terrain = ResolvedTerrain;
        TerrainDigResult result = terrain != null
            ? terrain.DigCircle(target, digRadius, authority, residentialStage, authorizedCells)
            : new TerrainDigResult(0, 0, 0, TerrainDigFailureReason.MissingTerrainAuthority);
        DigCompleted?.Invoke(result);
        return result;
    }

    public IEnumerator DigRoutine(Vector2 target, TerrainDigAuthority authority, int residentialStage,
        IReadOnlyCollection<Vector2Int> authorizedCells, Action<TerrainDigResult> completed = null)
    {
        TerrainDigResult result = Dig(target, authority, residentialStage, authorizedCells);
        if (digDuration > 0f) yield return new WaitForSeconds(digDuration);
        visualController?.SetAnimationState(GobboAnimationState.Idle);
        completed?.Invoke(result);
    }

    void Face(Vector2 target)
    {
        Vector2 direction = target - (Vector2)transform.position;
        if (direction.sqrMagnitude <= 0.0001f) return;
        visualController?.SetDirection(direction.normalized);
        directionalSprite?.SetDirection(direction.normalized);
    }
}
