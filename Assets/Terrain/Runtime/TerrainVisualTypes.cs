using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[Flags]
public enum DirtExposureMask
{
    None = 0,
    Above = 1 << 0,
    Right = 1 << 1,
    Below = 1 << 2,
    Left = 1 << 3
}

public enum TerrainVisualCellKind
{
    OutOfBounds,
    Open,
    Dirt,
    RevealDirt,
    Stone,
    Root,
    PermanentRock
}

public interface ITerrainVisualSource
{
    BoundsInt VisualBounds { get; }
    int VisualSeed { get; }
    TerrainVisualCellKind GetVisualKind(Vector2Int cell);
    bool ShouldRenderFloor(Vector2Int cell);
    Color GetPresentationTint(Vector2Int cell);
    TileBase GetSpecialBlockedTile(Vector2Int cell);
}
