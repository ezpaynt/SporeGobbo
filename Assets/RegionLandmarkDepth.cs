using System;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;

/// <summary>One ground-depth rule for current authored landmark batches. Not a general height-aware visibility mask.</summary>
public static class RegionLandmarkDepth
{
    public const int ActorFirstOrder=20,ActorStride=2,OverheadMinimumOrder=110;
    public enum Treatment { Grounded, OverheadSpan }

    /// <summary>Front/south contact of the actual protected footprint, not an arbitrary first path section or lifted vertex.</summary>
    public static Point2 ContactAnchor(IReadOnlyList<DualTile> footprint,ElevatedPath fallback)
    {
        if(footprint!=null&&footprint.Count>0)
        {
            int minX=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
            for(int i=0;i<footprint.Count;i++){var c=footprint[i];minX=Math.Min(minX,c.X);maxX=Math.Max(maxX,c.X);maxY=Math.Max(maxY,c.Y);}
            return new Point2((minX+maxX+1)*.5,maxY+1);
        }
        if(fallback==null||fallback.Sections==null||fallback.Sections.Length==0)throw new ArgumentException("Landmark has no ground contact");
        double min=double.PositiveInfinity,max=double.NegativeInfinity,south=double.NegativeInfinity;
        foreach(var s in fallback.Sections){min=Math.Min(min,Math.Min(s.Left.Ground.X,s.Right.Ground.X));max=Math.Max(max,Math.Max(s.Left.Ground.X,s.Right.Ground.X));south=Math.Max(south,Math.Max(s.Left.Ground.Y,s.Right.Ground.Y));}
        return new Point2((min+max)*.5,south);
    }
    public static int ActorOrder(int rank)
    {if(rank<0)throw new ArgumentOutOfRangeException(nameof(rank));return checked(ActorFirstOrder+ActorStride*rank);}
    public static int Order(int actorsNorth,int actorCount,Treatment treatment)
    {
        if(actorsNorth<0||actorCount<actorsNorth)throw new ArgumentOutOfRangeException(nameof(actorsNorth));
        if(treatment==Treatment.OverheadSpan)return Math.Max(OverheadMinimumOrder,ActorOrder(actorCount)+1);
        return checked(ActorFirstOrder-1+ActorStride*actorsNorth);
    }
    /// <summary>Equal-depth actors draw in front. X and visual body height do not move physical contact depth.</summary>
    public static bool IsNorth(float actorFootY,float contactY)=>actorFootY>contactY;
    public static int Resolve(IReadOnlyList<Stage59ActorView> actors,float contactY,Treatment treatment)
    {
        int north=0,count=0;
        for(int i=0;i<actors.Count;i++){var view=actors[i];if(view==null)continue;count++;if(IsNorth(view.PhysicalFoot.y,contactY))north++;}
        return Order(north,count,treatment);
    }
}
