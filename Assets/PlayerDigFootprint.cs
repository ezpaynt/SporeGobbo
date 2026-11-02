using System.Collections.Generic;
using UnityEngine;

/// <summary>Selection geometry only. Regions retain authorization, mutation and publication.</summary>
public readonly struct PlayerDigFootprint
{
    public readonly Vector2 Start, End, HalfExtents;
    public readonly float Radius;
    public readonly bool BodySweep;
    public PlayerDigFootprint(Vector2 start, Vector2 end, Vector2 halfExtents, float radius)
    { Start=start; End=end; HalfExtents=halfExtents; Radius=radius; BodySweep=true; }
    public PlayerDigFootprint(Vector2 center,float radius)
    { Start=End=center; HalfExtents=Vector2.zero; Radius=radius; BodySweep=false; }
    public bool Valid => Finite(Start)&&Finite(End)&&Finite(HalfExtents)&&
        HalfExtents.x>=0&&HalfExtents.y>=0&&float.IsFinite(Radius)&&Radius>0;
    static bool Finite(Vector2 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y);

    public List<Vector2Int> Cells(IDiggableTerrain terrain)
    {
        var result=new List<Vector2Int>();
        if(!Valid||terrain==null||terrain.CellSize<=0)return result;
        // Region transforms may invert an axis. Derive cell bounds from both world corners.
        var reach=new Vector2(Mathf.Max(Radius,HalfExtents.x),Mathf.Max(Radius,HalfExtents.y));
        var a=terrain.WorldToCell(Vector2.Min(Start,End)-reach);
        var b=terrain.WorldToCell(Vector2.Max(Start,End)+reach);
        int steps=BodySweep?Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(Start,End)/Mathf.Max(.1f,terrain.CellSize*.5f))):0;
        for(int y=Mathf.Min(a.y,b.y)-1;y<=Mathf.Max(a.y,b.y)+1;y++)
        for(int x=Mathf.Min(a.x,b.x)-1;x<=Mathf.Max(a.x,b.x)+1;x++)
        {
            var cell=new Vector2Int(x,y);var center=terrain.CellToWorld(cell);
            bool selected=BodySweep&&TileMover.BodySweepOverlapsCell(Start,End,HalfExtents,center,terrain.CellSize);
            // Retain the existing sampled-circle area stroke, adding only the required body corridor.
            for(int i=0;!selected&&i<=steps;i++)
                selected=(center-Vector2.Lerp(Start,End,steps==0?0:i/(float)steps)).sqrMagnitude<=Radius*Radius;
            if(selected)result.Add(cell);
        }
        return result;
    }
}

/// <summary>Optional player-stroke capability; legacy terrains and Buddy digging retain their contracts.</summary>
public interface IPlayerDigTerrain
{
    TerrainDigResult DigPlayerStroke(PlayerDigFootprint footprint);
}
