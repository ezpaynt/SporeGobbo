using System;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;
/// <summary>The region's existing derived native collision, never another occupancy map.</summary>
public interface IExecutableTerrainClearance { RegionTerrainPhysics TerrainPhysics {get;} }
public sealed partial class RegionTerrainPhysics
{
    // These rectangles are the very same paths passed to SetPath during collision publication.
    sealed class NativePatch {public PolygonCollider2D Collider;public Rect[] Rectangles;public Vector2 Padding;}
    BoxCollider2D clearanceProbe;Vector2 lastProbeHalf,probeSupport,maximumTerrainSupport;bool probeValid;
    static readonly Unity.Profiling.ProfilerMarker clearanceProfile=new Unity.Profiling.ProfilerMarker("Spore.Region.NativeClearance");
    public long ClearanceQueries{get;private set;}public long ClearanceRectangles{get;private set;}
    void InitializeClearance()
    {
        var go=new GameObject("Terrain clearance query (unsimulated)");go.transform.SetParent(root.transform,false);
        var body=go.AddComponent<Rigidbody2D>();body.simulated=false;body.gravityScale=0;body.position=Vector2.zero;go.transform.position=Vector3.zero;
        clearanceProbe=go.AddComponent<BoxCollider2D>();RefreshClearance(patches.Keys);
    }
    void RefreshClearance(IEnumerable<DualTile> keys)
    {
        foreach(var key in keys)
        {
            if(!patches.TryGetValue(key,out var p)||!p.Collider.enabled||p.Rectangles.Length==0)continue;
            Rect right=p.Rectangles[0],left=right,top=right,bottom=right;
            foreach(var r in p.Rectangles){if(r.xMax>right.xMax)right=r;if(r.xMin<left.xMin)left=r;if(r.yMax>top.yMax)top=r;if(r.yMin<bottom.yMin)bottom=r;}
            float cell=(float)grid.CellSize;
            float px=p.Collider.ClosestPoint(new Vector2(right.xMax+cell,right.center.y)).x-right.xMax;
            float nx=left.xMin-p.Collider.ClosestPoint(new Vector2(left.xMin-cell,left.center.y)).x;
            float py=p.Collider.ClosestPoint(new Vector2(top.center.x,top.yMax+cell)).y-top.yMax;
            float ny=bottom.yMin-p.Collider.ClosestPoint(new Vector2(bottom.center.x,bottom.yMin-cell)).y;
            p.Padding=new Vector2(Mathf.Max(0,Mathf.Max(px,nx)),Mathf.Max(0,Mathf.Max(py,ny)));
        }
        maximumTerrainSupport=Vector2.zero;foreach(var p in patches.Values)if(p.Collider.enabled)maximumTerrainSupport=Vector2.Max(maximumTerrainSupport,p.Padding);
    }
    Vector2 BodySupport(Vector2 half)
    {
        if(!probeValid||half!=lastProbeHalf)
        {
            clearanceProbe.size=half*2;float cell=(float)grid.CellSize;
            probeSupport=new Vector2(Mathf.Max(clearanceProbe.ClosestPoint(new Vector2(half.x+cell,0)).x,-clearanceProbe.ClosestPoint(new Vector2(-half.x-cell,0)).x),Mathf.Max(clearanceProbe.ClosestPoint(new Vector2(0,half.y+cell)).y,-clearanceProbe.ClosestPoint(new Vector2(0,-half.y-cell)).y));
            lastProbeHalf=half;probeValid=true;
        }
        return probeSupport;
    }
    public Vector2 TerrainPlanningExtents(Vector2 physicalHalf)=>BodySupport(physicalHalf)+maximumTerrainSupport;
    static Rect Expanded(Rect r,Vector2 e)=>Rect.MinMaxRect(r.xMin-e.x,r.yMin-e.y,r.xMax+e.x,r.yMax+e.y);
    static bool Inside(Rect r,Vector2 p)=>p.x>=r.xMin&&p.x<=r.xMax&&p.y>=r.yMin&&p.y<=r.yMax;
    static bool SweepAxis(float p,float d,float min,float max,ref float enter,ref float exit)
    {
        if(d==0)return p>=min&&p<=max;float a=(min-p)/d,b=(max-p)/d;enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));return enter<=exit;
    }
    static bool Hits(Rect r,Vector2 p,Vector2 delta)
    {float enter=0,exit=1;return SweepAxis(p.x,delta.x,r.xMin,r.xMax,ref enter,ref exit)&&SweepAxis(p.y,delta.y,r.yMin,r.yMax,ref enter,ref exit);}
    internal static bool NonDeepeningContact(Rect r,Vector2 p,Vector2 delta)
    {
        // For each convex published rectangle, an outward/tangential nearest support plane
        // guarantees non-increasing penetration for the ENTIRE straight connector, not one step.
        float left=p.x-r.xMin,right=r.xMax-p.x,bottom=p.y-r.yMin,top=r.yMax-p.y;
        float nearest=Mathf.Min(Mathf.Min(left,right),Mathf.Min(bottom,top));
        return !(left==nearest&&delta.x>0||right==nearest&&delta.x<0||bottom==nearest&&delta.y>0||top==nearest&&delta.y<0);
    }
    bool Query(Vector2 from,Vector2 to,Vector2 half,bool sweep)
    {
        using var profile=clearanceProfile.Auto();ClearanceQueries++;
        if(clearanceProbe==null||half.x<=0||half.y<=0||float.IsNaN(half.x)||float.IsNaN(half.y))return false;
        Vector2 support=BodySupport(half),range=support+maximumTerrainSupport;
        var lo=Logical(Vector2.Min(from,to)-range);var hi=Logical(Vector2.Max(from,to)+range);
        int minX=Mathf.Max(0,(int)Math.Floor(Math.Min(lo.X,hi.X)/16)),maxX=Mathf.Min((state.World.Width-1)/16,(int)Math.Floor(Math.Max(lo.X,hi.X)/16));
        int minY=Mathf.Max(0,(int)Math.Floor(Math.Min(lo.Y,hi.Y)/16)),maxY=Mathf.Min((state.World.Height-1)/16,(int)Math.Floor(Math.Max(lo.Y,hi.Y)/16));
        var delta=to-from;
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
        {
            if(!patches.TryGetValue(new DualTile(x,y),out var patch)||!patch.Collider.enabled)continue;
            foreach(var core in patch.Rectangles)
            {
                ClearanceRectangles++;var r=Expanded(core,support+patch.Padding);
                if(!sweep){if(Inside(r,from))return false;}
                else if(Inside(r,from)){if(!NonDeepeningContact(r,from,delta))return false;}
                else if(Hits(r,from,delta))return false;
            }
        }
        return true;
    }
    public bool CanOccupyTerrainBox(Vector2 p,Vector2 half)=>Query(p,p,half,false);
    public bool CanTraverseTerrainBox(Vector2 a,Vector2 b,Vector2 half)=>Query(a,b,half,true);
}
