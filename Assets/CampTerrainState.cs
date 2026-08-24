using System;
using System.Collections.Generic;

[Serializable]
public sealed class CampCellCoordinate
{
    public int x;
    public int y;

    public CampCellCoordinate() { }

    public CampCellCoordinate(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public CampCellCoordinate Clone() => new CampCellCoordinate(x, y);
}

[Serializable]
public sealed class CampTerrainState
{
    public int layoutRevision = 0;
    public bool mainChamberRevealed = false;
    public List<CampCellCoordinate> clearedCellCoordinates = new List<CampCellCoordinate>();
    public List<string> unlockedExpansionRegionIds = new List<string>();

    public void Normalize()
    {
        layoutRevision = Math.Max(0, layoutRevision);
        clearedCellCoordinates ??= new List<CampCellCoordinate>();
        unlockedExpansionRegionIds ??= new List<string>();

        HashSet<long> seenCells = new HashSet<long>();
        for (int i = clearedCellCoordinates.Count - 1; i >= 0; i--)
        {
            CampCellCoordinate cell = clearedCellCoordinates[i];
            if (cell == null)
            {
                clearedCellCoordinates.RemoveAt(i);
                continue;
            }

            long key = ((long)cell.x << 32) ^ (uint)cell.y;
            if (!seenCells.Add(key)) clearedCellCoordinates.RemoveAt(i);
        }

        HashSet<string> seenRegions = new HashSet<string>(StringComparer.Ordinal);
        for (int i = unlockedExpansionRegionIds.Count - 1; i >= 0; i--)
        {
            string regionId = unlockedExpansionRegionIds[i]?.Trim();
            if (string.IsNullOrEmpty(regionId) || !seenRegions.Add(regionId))
                unlockedExpansionRegionIds.RemoveAt(i);
            else
                unlockedExpansionRegionIds[i] = regionId;
        }
    }

    public CampTerrainState Clone()
    {
        Normalize();
        CampTerrainState copy = new CampTerrainState
        {
            layoutRevision = layoutRevision,
            mainChamberRevealed = mainChamberRevealed,
            unlockedExpansionRegionIds = new List<string>(unlockedExpansionRegionIds)
        };
        foreach (CampCellCoordinate cell in clearedCellCoordinates) copy.clearedCellCoordinates.Add(cell.Clone());
        return copy;
    }
}