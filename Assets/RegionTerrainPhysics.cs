using System;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;
/// <summary>Shared derived collision and clearance queries over the sole region occupancy. No campaign policy.</summary>
public sealed partial class RegionTerrainPhysics:IDisposable
{
 readonly RegionTerrainState state;readonly LogicalGridTransform grid;readonly GameObject root;
 readonly Dictionary<DualTile,NativePatch> patches=new Dictionary<DualTile,NativePatch>();
 RegionRouteQueue navigation;
 RegionMovementCoordinator movement;
 public void BindNavigation(ActorTerrainBinding binding,IDiggableTerrain terrain)
 {
  if(navigation==null){navigation=new RegionRouteQueue(state,terrain,Logical,World);movement=new RegionMovementCoordinator(terrain,terrain is SettlementCampRegion);var pump=root.AddComponent<RegionRoutePump>();pump.Queue=navigation;pump.Movement=movement;}
  binding.RequestRoute=(from,goal)=>{var status=navigation.RequestPath(binding,from,goal,TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),binding.MinimumBodyRadius),out var path);return new ActorRouteResult(status,path,navigation.RouteRevision(binding),navigation.RouteDistance(binding));};
  binding.AcceptCachedRoute=revision=>navigation.Accepts(binding,revision);
  binding.RequestProbe=(key,from,goal)=>{var status=navigation.RequestPath(binding,from,goal,TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),binding.MinimumBodyRadius),out var path,key);return new ActorRouteResult(status,path,navigation.RouteRevision(binding,key));};
  movement.Register(binding);
  binding.ChooseDestination=(key,center,size,contact)=>movement.Choose(binding,key,center,size,contact);
  binding.HasMovementDestination=()=>movement.HasDestination(binding);
  binding.CancelRoute=()=>{navigation.CancelAll(binding);movement.ReleaseDuty(binding);};
  binding.CancelMovementRoute=()=>navigation.Cancel(binding);
  binding.CancelRouteWork=()=>navigation.CancelAll(binding);
 }
 readonly HashSet<DualTile> dirty=new HashSet<DualTile>();
 public int PatchCount=>patches.Count;public int RejectedRoutes{get;private set;}
 public RegionTerrainPhysics(RegionTerrainState state,Transform parent,float cell=.6f){this.state=state;grid=new LogicalGridTransform(default,cell,state.World.Height);root=new GameObject("Derived region collision");root.transform.SetParent(parent,false);root.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;for(int y=0;y<(state.World.Height+15)/16;y++)for(int x=0;x<(state.World.Width+15)/16;x++)RebuildCollision(new DualTile(x,y));Physics2D.SyncTransforms();InitializeClearance();}
 public Vector2 World(Point2 p){var q=grid.ToWorld(p);return new Vector2((float)q.X,(float)q.Y);}
 public Point2 Logical(Vector2 p)=>grid.ToLogical(new Point2(p.x,p.y));
 public bool Owns(Collider2D c)=>c!=clearanceProbe&&c!=null&&root!=null&&c.transform.IsChildOf(root.transform);
 public void Publish(IReadOnlyList<DualTile> cells){using var profile=new Unity.Profiling.ProfilerMarker("Spore.Region.Collision").Auto();dirty.Clear();foreach(var c in cells)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(state.World.Contains(c.X+x,c.Y+y))dirty.Add(new DualTile((c.X+x)/16,(c.Y+y)/16));foreach(var k in dirty)RebuildCollision(k);Physics2D.SyncTransforms();RefreshClearance(dirty);}
 public List<Vector2> Route(IDiggableTerrain terrain,Vector2 start,Vector2 goal,Vector2 extents){using var profile=new Unity.Profiling.ProfilerMarker("Spore.Region.Navigation").Auto();int size=Math.Max(1,(int)Math.Ceiling((2*Math.Max(extents.x,extents.y)+.15)/terrain.CellSize));if(size%2==0)size++;Point2 Anchor(Vector2 p){var c=terrain.WorldToCell(p);return new Point2(c.x+.5,c.y+.5);}var path=state.Route(Anchor(start),Anchor(goal),size);var result=new List<Vector2>();var prior=start;foreach(var p in path){var next=World(p);if(!TileMover.CanTraverseBox(terrain,prior,next,extents)){RejectedRoutes++;return new List<Vector2>();}result.Add(next);prior=next;}if(result.Count>0&&TileMover.CanTraverseBox(terrain,prior,goal,extents))result.Add(goal);else result.Clear();return result;}
 void RebuildCollision(DualTile key)
 {
  // Only exposed solid boundary cells need physics shapes. Logical TileMover remains authoritative.
  var paths=new List<Vector2[]>();bool Exposed(int x,int y)=>state.World.IsSolid(x,y)&&(!state.World.IsSolid(x-1,y)||!state.World.IsSolid(x+1,y)||!state.World.IsSolid(x,y-1)||!state.World.IsSolid(x,y+1));
  for(int y=key.Y*16;y<(key.Y+1)*16;y++)for(int x=key.X*16;x<(key.X+1)*16;){if(!Exposed(x,y)){x++;continue;}int first=x;while(x<(key.X+1)*16&&Exposed(x,y))x++;paths.Add(new[]{World(new Point2(first,y)),World(new Point2(first,y+1)),World(new Point2(x,y+1)),World(new Point2(x,y))});}
  if(!patches.TryGetValue(key,out var patch)){if(paths.Count==0)return;var go=new GameObject("Terrain boundary patch "+key.X+","+key.Y);go.transform.SetParent(root.transform,false);patch=new NativePatch{Collider=go.AddComponent<PolygonCollider2D>()};patches.Add(key,patch);}
  var collider=patch.Collider;collider.enabled=paths.Count>0;collider.pathCount=paths.Count;patch.Rectangles=new Rect[paths.Count];
  for(int i=0;i<paths.Count;i++){collider.SetPath(i,paths[i]);var a=paths[i][0];var c=paths[i][2];patch.Rectangles[i]=Rect.MinMaxRect(Mathf.Min(a.x,c.x),Mathf.Min(a.y,c.y),Mathf.Max(a.x,c.x),Mathf.Max(a.y,c.y));}
 }
public void Dispose(){movement?.Dispose();navigation?.Dispose();if(root!=null)UnityEngine.Object.Destroy(root);patches.Clear();}
}
