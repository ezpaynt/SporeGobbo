using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry
{
 /// <summary>Single logical region owner. Geometry/navigation consume it; campaign facts never live here.</summary>
 public sealed class RegionTerrainState
 {
  public Guid InstanceId{get;}=Guid.NewGuid();public string RegionId{get;}public StreamingWorld World{get;}public int Revision{get;private set;}
  public event Action<IReadOnlyList<DualTile>,int> Committed;
  readonly Func<int,int,bool> excavate,place;readonly HashSet<string> placements=new HashSet<string>();readonly HashSet<int> filled=new HashSet<int>();bool placementLease;readonly int[] previous,seen,queue;int generation;
  readonly HashSet<int> deltas=new HashSet<int>();
  bool mutationControlTaken;
  public RegionTerrainState(string regionId,int width,int height,ICompleteTerrainDefinition definition,int seed=0)
  {if(string.IsNullOrWhiteSpace(regionId))throw new ArgumentException(nameof(regionId));RegionId=regionId;World=new StreamingWorld(width,height,seed,definition);excavate=World.TakeExcavationControl();place=World.TakePlacementControl();previous=new int[width*height];seen=new int[width*height];queue=new int[width*height];}
  public RegionTerrainState(string regionId,int width,int height,ICompleteTerrainDefinition definition,IReadOnlyList<DualTile> restored,int revision):this(regionId,width,height,definition)
  {
   if(restored==null||revision<0||(restored.Count==0)!=(revision==0))throw new ArgumentException("Invalid restored revision");
   if(restored.Count>0&&CommitCore(restored,0,c=>true)!=restored.Count)throw new ArgumentException("Invalid restored terrain delta");
   Revision=revision;
  }
  public int Commit(IEnumerable<DualTile> requested,int expectedRevision,Func<DualTile,bool> permitted)
  {if(mutationControlTaken)throw new InvalidOperationException("Use the owning region transaction service");return CommitCore(requested,expectedRevision,permitted);}
  public Func<IEnumerable<DualTile>,int,Func<DualTile,bool>,int> TakeMutationControl()
  {if(mutationControlTaken)throw new InvalidOperationException("Region mutation already has an owner");mutationControlTaken=true;return CommitCore;}
  int CommitCore(IEnumerable<DualTile> requested,int expectedRevision,Func<DualTile,bool> permitted)
  {
   if(expectedRevision!=Revision)return 0;if(requested==null||permitted==null)throw new ArgumentNullException();
   // Materialize and authorize before any mutation: malformed/protected batches fail as a whole.
   var accepted=new List<DualTile>();var unique=new HashSet<int>();
   foreach(var c in requested){if(!World.Contains(c.X,c.Y)||World.Locked(c.X,c.Y)||!permitted(c))return 0;int i=c.Y*World.Width+c.X;if(unique.Add(i)&&World.IsSolid(c.X,c.Y))accepted.Add(c);}
   foreach(var c in accepted){if(!excavate(c.X,c.Y))throw new InvalidOperationException("Single-owner terrain changed during commit");if(World.BaseSolid(c.X,c.Y))deltas.Add(c.Y*World.Width+c.X);else deltas.Remove(c.Y*World.Width+c.X);filled.Remove(c.Y*World.Width+c.X);}
   if(accepted.Count>0){Revision++;Committed?.Invoke(accepted.AsReadOnly(),Revision);}return accepted.Count;
  }
  public Func<TerrainPlacementRequest,Func<IReadOnlyList<DualTile>,bool>,TerrainPlacementResult> TakePlacementControl()
  {if(!mutationControlTaken||placementLease)throw new InvalidOperationException("Placement requires the single region owner");placementLease=true;return PlaceCore;}
  TerrainPlacementResult PlaceCore(TerrainPlacementRequest request,Func<IReadOnlyList<DualTile>,bool> safety)
  {
   if(request==null||safety==null)return new TerrainPlacementResult(0,"invalid request");
   if(request.Instance!=InstanceId||request.Revision!=Revision)return new TerrainPlacementResult(0,"stale region/revision");
   if(string.IsNullOrWhiteSpace(request.EventId)||placements.Contains(request.EventId))return new TerrainPlacementResult(0,"duplicate/invalid event");
   if(!(World.Definition is IBlockingTerrainDefinition domain)||request.Cells==null||request.Cells.Count==0||request.Cells.Count>256)return new TerrainPlacementResult(0,"invalid domain/batch");
   var cells=new List<DualTile>();var unique=new HashSet<int>();
   foreach(var c in request.Cells){if(!World.Contains(c.X,c.Y)||World.Locked(c.X,c.Y)||!domain.PlacementAllowed(c.X,c.Y))return new TerrainPlacementResult(0,"protected/outside authored mask");if(unique.Add(c.Y*World.Width+c.X)&&!World.IsSolid(c.X,c.Y))cells.Add(c);}
   if(cells.Count==0)return new TerrainPlacementResult(0,"no open cells");
   if(!safety(cells.AsReadOnly()))return new TerrainPlacementResult(0,"actor/escape safety");
   foreach(var c in cells){if(!place(c.X,c.Y))throw new InvalidOperationException("Single-owner placement changed during commit");int i=c.Y*World.Width+c.X;deltas.Remove(i);if(!World.BaseSolid(c.X,c.Y))filled.Add(i);}
   placements.Add(request.EventId);Revision++;Committed?.Invoke(cells.AsReadOnly(),Revision);return new TerrainPlacementResult(cells.Count,null);
  }
  public int[] ExportDeltas(){if(filled.Count>0)throw new InvalidOperationException("Transient solid placement cannot be serialized as excavation-only deltas");var r=new int[deltas.Count];deltas.CopyTo(r);Array.Sort(r);return r;}
  public bool Fits(Point2 p,int size)
  {if(size<1)throw new ArgumentOutOfRangeException(nameof(size));double h=size*.5-.001;for(int y=(int)Math.Floor(p.Y-h);y<=(int)Math.Floor(p.Y+h);y++)for(int x=(int)Math.Floor(p.X-h);x<=(int)Math.Floor(p.X+h);x++)if(World.IsSolid(x,y))return false;return true;}
  // Bounded session workspace, deterministic four-neighbor traversal. Queries are serialized by the owner.
  public List<Point2> Route(Point2 from,Point2 to,int size)
  {
   var result=new List<Point2>();if(!Fits(from,size)||!Fits(to,size))return result;
   int w=World.Width,start=(int)Math.Floor(from.Y)*w+(int)Math.Floor(from.X),goal=(int)Math.Floor(to.Y)*w+(int)Math.Floor(to.X);
   if(generation==int.MaxValue){Array.Clear(seen,0,seen.Length);generation=0;}int g=++generation,head=0,tail=1;queue[0]=start;seen[start]=g;previous[start]=-1;
   while(head<tail){int i=queue[head++];if(i==goal){for(int n=goal;n>=0;n=previous[n])result.Add(new Point2(n%w+.5,n/w+.5));result.Reverse();break;}int x=i%w,y=i/w;for(int d=0;d<4;d++){int nx=x+(d==0?-1:d==1?1:0),ny=y+(d==2?-1:d==3?1:0);if(!World.Contains(nx,ny))continue;int n=ny*w+nx;if(seen[n]==g)continue;seen[n]=g;if(!Fits(new Point2(nx+.5,ny+.5),size))continue;previous[n]=i;queue[tail++]=n;}}
   return result;
  }
 }
}
