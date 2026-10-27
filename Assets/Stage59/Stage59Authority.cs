using System;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;

/// <summary>Production IDiggableTerrain adapter over the one existing CampSession authority.</summary>
public sealed class Stage59Authority:IDiggableTerrain,IDisposable
{
    public readonly CampSession Session;
    public readonly LogicalGridTransform Grid=new LogicalGridTransform(new Point2(0,0),.6,192);
    readonly Dictionary<DualTile,PolygonCollider2D> patches=new Dictionary<DualTile,PolygonCollider2D>();
    readonly HashSet<DualTile> dirty=new HashSet<DualTile>();
    readonly List<DualTile> cells=new List<DualTile>();
    public GameObject CollisionRoot {get;private set;}
    public double ColliderMs {get;private set;}
    public int PublishedRevision {get;private set;}
    public bool IsDisposed {get;private set;}
    public int Revision=>IsDisposed?-1:Session.Revision;
    public float CellSize=>.6f;
    public Func<bool> AllowEdits=()=>true;
    public Func<Vector2Int,bool> ScenarioDiggable;
    public Func<Vector2,float,TerrainDigAuthority,int,IReadOnlyCollection<Vector2Int>,TerrainDigResult> ScenarioDig;
    public Stage59Authority(CampSession session){Session=session;}
    public Vector2 ToWorld(Point2 p){var q=Grid.ToWorld(p);return new Vector2((float)q.X,(float)q.Y);}
    public Point2 ToLogical(Vector2 p)=>Grid.ToLogical(new Point2(p.x,p.y));
    public Vector2Int WorldToCell(Vector2 p){var q=ToLogical(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
    public Vector2 CellToWorld(Vector2Int c)=>ToWorld(new Point2(c.x+.5,c.y+.5));
    public bool IsBlocked(Vector2Int c)=>IsDisposed||Session.Terrain.IsSolid(c.x,c.y);
    public bool IsDiggable(Vector2Int c)=>!IsDisposed&&c.x>=0&&c.y>=0&&c.x<192&&c.y<192&&(ScenarioDiggable!=null?ScenarioDiggable(c):Session.Definition.Category(c.x,c.y)==CampCell.Expansion)&&IsBlocked(c);
    public void DigCircle(Vector2 p,float radius)=>DigCircle(p,radius,TerrainDigAuthority.Player,0,null);
    public TerrainDigResult DigCircle(Vector2 p,float radius,TerrainDigAuthority authority,int stage,IReadOnlyCollection<Vector2Int> authorized)
    {
        using(new ActorTerrainBinding.Scope("SG59.DigAuthority"))
        {
            if(!AllowEdits())return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
            if(ScenarioDig!=null)return ScenarioDig(p,radius,authority,stage,authorized);
            var center=WorldToCell(p);int range=Mathf.CeilToInt(radius/CellSize)+1,evaluated=0;cells.Clear();
            for(int y=center.y-range;y<=center.y+range;y++)for(int x=center.x-range;x<=center.x+range;x++)
            {
                var c=new Vector2Int(x,y);if((CellToWorld(c)-p).sqrMagnitude>radius*radius)continue;evaluated++;
                // The bedroom is ordinary future dirt. No residential or root-door bypass in this proof.
                if(IsDiggable(c))cells.Add(new DualTile(x,y));
            }
            int eligible=cells.Count,removed=Session.DigPlayer(cells);
            return new TerrainDigResult(evaluated,eligible,removed,removed>0?TerrainDigFailureReason.None:TerrainDigFailureReason.NoDirtRemoved);
        }
    }
    public List<Vector2> Route(Vector2 from,Vector2 to,Vector2 halfExtents)
    {
        int size=Math.Max(1,(int)Math.Ceiling(2*(Math.Max(halfExtents.x,halfExtents.y)+.15f)/CellSize));if(size%2==0)size++;
        if(IsDisposed||!TryAnchor(from,halfExtents,size,out var start)||!TryAnchor(to,halfExtents,size,out var goal))return new List<Vector2>();
        var logical=Session.Route(start,goal,size);var result=new List<Vector2>(logical.Count);
        Vector2 previous=from;
        foreach(var p in logical){var next=ToWorld(p);if(!TileMover.CanTraverseBox(this,previous,next,halfExtents)){result.Clear();return result;}result.Add(next);previous=next;}
        if(result.Count>0&&TileMover.CanTraverseBox(this,previous,to,halfExtents))result.Add(to);else result.Clear();return result;
    }
    bool TryAnchor(Vector2 physical,Vector2 halfExtents,int size,out Point2 anchor)
    {
        anchor=default;if(!TileMover.CanOccupyBox(this,physical,halfExtents))return false;var cell=WorldToCell(physical);float nearest=float.PositiveInfinity;bool found=false;
        for(int y=cell.y-2;y<=cell.y+2;y++)for(int x=cell.x-2;x<=cell.x+2;x++){var p=new Point2(x+.5,y+.5);var world=ToWorld(p);float distance=(world-physical).sqrMagnitude;if(distance>=nearest||!Session.Fits(p,size)||!TileMover.CanTraverseBox(this,physical,world,halfExtents))continue;anchor=p;nearest=distance;found=true;}return found;
    }
    public void CreateCollision(Transform parent)
    {
        CollisionRoot=new GameObject("Stage59 derived collision patches");CollisionRoot.transform.SetParent(parent,false);
        CollisionRoot.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
        for(int y=0;y<12;y++)for(int x=0;x<12;x++)Rebuild(new DualTile(x,y));
        PublishedRevision=Revision;Session.Committed+=OnCommit;
    }
    void OnCommit(CampBatch batch)
    {
        using(new ActorTerrainBinding.Scope("SG59.CollisionPublication"))
        {
            long t=System.Diagnostics.Stopwatch.GetTimestamp();dirty.Clear();foreach(var c in batch.Cells)dirty.Add(new DualTile(c.X/16,c.Y/16));
            foreach(var key in dirty)Rebuild(key);Physics2D.SyncTransforms();PublishedRevision=Revision;
            ColliderMs=SporeGobbo.Geometry.Preview.StreamingGeometry.Elapsed(t);
        }
    }
    void Rebuild(DualTile key)
    {
        if(!patches.TryGetValue(key,out var collider)){var go=new GameObject("Collision patch "+key.X+","+key.Y);go.transform.SetParent(CollisionRoot.transform,false);collider=go.AddComponent<PolygonCollider2D>();patches.Add(key,collider);}
        var paths=new List<Vector2[]>();
        for(int y=key.Y*16;y<key.Y*16+16;y++)for(int x=key.X*16;x<key.X*16+16;)
        {
            if(!Session.Terrain.IsSolid(x,y)){x++;continue;}int first=x;while(x<key.X*16+16&&Session.Terrain.IsSolid(x,y))x++;
            paths.Add(new[]{ToWorld(new Point2(first,y)),ToWorld(new Point2(first,y+1)),ToWorld(new Point2(x,y+1)),ToWorld(new Point2(x,y))});
        }
        collider.enabled=paths.Count>0;collider.pathCount=paths.Count;for(int i=0;i<paths.Count;i++)collider.SetPath(i,paths[i]);
    }
    public void Dispose(){if(CollisionRoot!=null)CollisionRoot.SetActive(false);IsDisposed=true;ScenarioDig=null;ScenarioDiggable=null;Session.Committed-=OnCommit;patches.Clear();if(CollisionRoot!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(CollisionRoot);else UnityEngine.Object.DestroyImmediate(CollisionRoot);}CollisionRoot=null;AllowEdits=()=>false;}
}
