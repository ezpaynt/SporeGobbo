using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PersistentTestCaveTerrainState
{
    public string mapId = "";
    public int layoutVersion;
    public List<Vector2Int> clearedCells = new List<Vector2Int>();

    public void EnsureIdentity(string expectedMapId, int expectedVersion)
    {
        expectedMapId = string.IsNullOrWhiteSpace(expectedMapId) ? "persistent-test-cave" : expectedMapId.Trim();
        expectedVersion = Mathf.Max(1, expectedVersion);
        if (mapId != expectedMapId || layoutVersion != expectedVersion)
        {
            mapId = expectedMapId;
            layoutVersion = expectedVersion;
            clearedCells = new List<Vector2Int>();
        }
        Normalize();
    }

    public void Normalize()
    {
        clearedCells ??= new List<Vector2Int>();
        HashSet<Vector2Int> unique = new HashSet<Vector2Int>();
        for (int i = clearedCells.Count - 1; i >= 0; i--)
            if (!unique.Add(clearedCells[i])) clearedCells.RemoveAt(i);
        clearedCells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        layoutVersion = Mathf.Max(0, layoutVersion);
        mapId = mapId == null ? "" : mapId.Trim();
    }

    public bool RecordCleared(Vector2Int cell)
    {
        clearedCells ??= new List<Vector2Int>();
        if (clearedCells.Contains(cell)) return false;
        clearedCells.Add(cell);
        return true;
    }

    public PersistentTestCaveTerrainState Clone()
    {
        Normalize();
        return new PersistentTestCaveTerrainState
        {
            mapId = mapId,
            layoutVersion = layoutVersion,
            clearedCells = new List<Vector2Int>(clearedCells)
        };
    }
}
