using System;
using System.Collections.Generic;
using System.IO;
namespace SporeGobbo.Geometry
{
 public enum DiscoveryPolicy {Immediate,Gradual,Bounded}
 public enum TerrainClueKind {Tint,Disturbed,Crack,Trace}
 public readonly struct TerrainClue
 {public readonly Point2 Position;public readonly TerrainClueKind Kind;public TerrainClue(Point2 position,TerrainClueKind kind){Position=position;Kind=kind;}}
 public sealed class DiscoveryRegion
 {
  public readonly string Id;public readonly int[] Cells;public readonly DiscoveryPolicy Policy;public readonly double RevealSeconds;public readonly int LocalImmediate;
  public DiscoveryRegion(string id,IEnumerable<int> cells,DiscoveryPolicy policy,double revealSeconds=-1,int localImmediate=0){RevealSeconds=revealSeconds;LocalImmediate=localImmediate;Id=id;var sorted=new List<int>(cells);sorted.Sort();Cells=sorted.ToArray();Policy=policy;}
 }
 public sealed class DiscoveryFixture:IStreamingTerrainDefinition,IRefillableTerrainDefinition
 {
  public const string Identity="stage52.discovery-fixture.v1";public readonly int X,Y,Size;
  readonly HashSet<int> open=new HashSet<int>();public readonly List<DiscoveryRegion> Authored=new List<DiscoveryRegion>();
  public readonly List<int> InitialKnown=new List<int>();
  public readonly List<TerrainClue> Clues=new List<TerrainClue>();
  public readonly List<DualTile> Rubble=new List<DualTile>();public readonly Point2 Entry;public readonly Point2[] Approaches,Centers;
  public readonly string[] Names={"Tiny hollow","Medium chamber","Branching network","Two entrances","Cross-chunk chamber","Protected landmark","Refillable rubble","Network far entrance","False clue (ordinary dirt)"};
  public DiscoveryFixture(int size)
  {
   if(size<768)throw new ArgumentOutOfRangeException(nameof(size));Size=size;X=Y=size/2;Entry=P(-82,0);
   Rect(open,-92,-3,220,3);Rect(open,-86,-68,-80,68);
   // A physical barrier makes the long concealed route a genuine alternate connection.
   for(int y=-3;y<=3;y++)for(int x=99;x<=101;x++)open.Remove(Key(x,y));
   Add("tiny",DiscoveryPolicy.Immediate,Ellipse(-60,-20,4,3),-61,-18,-59,-5);
   Add("medium",DiscoveryPolicy.Gradual,Ellipse(-20,-36,13,9),-21,-28,-19,-5);
   var network=new HashSet<int>();Rect(network,30,-24,180,-20);Rect(network,34,-19,36,-5);Rect(network,178,-19,180,-5);Rect(network,68,-55,72,-20);Rect(network,123,-65,127,-20);network.UnionWith(Ellipse(70,-55,7,6));network.UnionWith(Ellipse(125,-65,10,8));
   // One connected network, multiple deterministic chunk-local units.
   AddPartitioned(network,"network",DiscoveryPolicy.Bounded);
   var two=Ellipse(-55,32,12,8);Rect(two,-65,5,-63,27);Rect(two,-47,5,-45,27);AddSet("two-entry",two,DiscoveryPolicy.Gradual);
   Add("cross-chunk",DiscoveryPolicy.Gradual,Ellipse(45,35,22,9),44,5,46,28);
   Add("landmark",DiscoveryPolicy.Gradual,Ellipse(110,35,13,9),109,5,111,28);
   Add("rubble",DiscoveryPolicy.Gradual,Ellipse(-5,35,9,6),-6,5,-4,30);
   for(int x=-6;x<=-4;x++)Rubble.Add(new DualTile(X+x,Y+4));
   foreach(int cell in open){int u=cell%Size-X,v=cell/Size-Y;if(Math.Abs(v)<=3||u>=-86&&u<=-80)InitialKnown.Add(cell);}
   InitialKnown.Sort();
   Approaches=new[]{P(-60,-2),P(-20,-2),P(35,-2),P(-64,2),P(45,2),P(110,2),P(-5,2),P(179,-2),P(15,2)};
   for(int i=0;i<Approaches.Length;i++){var p=Approaches[i];Clues.Add(new TerrainClue(new Point2(p.X,p.Y+(i<=2||i==7?-2:2)),(TerrainClueKind)(i%4)));}
   Centers=new[]{P(-60,-20),P(-20,-36),P(125,-65),P(-55,32),P(45,35),P(110,35),P(-5,35),P(179,-22),P(15,10)};
  }
  public Point2 P(int x,int y)=>new Point2(X+x+.5,Y+y+.5);
  int Key(int x,int y)=>(Y+y)*Size+X+x;
  void Rect(HashSet<int> cells,int x0,int y0,int x1,int y1){for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)cells.Add(Key(x,y));}
  HashSet<int> Ellipse(int x,int y,int rx,int ry){var cells=new HashSet<int>();for(int v=-ry;v<=ry;v++)for(int u=-rx;u<=rx;u++)if(u*u/(double)(rx*rx)+v*v/(double)(ry*ry)<=1)cells.Add(Key(x+u,y+v));return cells;}
  void Add(string id,DiscoveryPolicy policy,HashSet<int> cells,int x0,int y0,int x1,int y1){Rect(cells,x0,y0,x1,y1);AddSet(id,cells,policy);}
  void AddSet(string id,HashSet<int> cells,DiscoveryPolicy policy){cells.RemoveWhere(i=>Protected(i%Size,i/Size));open.UnionWith(cells);Authored.Add(new DiscoveryRegion("fixture/"+id,cells,policy));}
  void AddPartitioned(HashSet<int> cells,string prefix,DiscoveryPolicy policy)
  {
   open.UnionWith(cells);var groups=new SortedDictionary<int,HashSet<int>>();foreach(int i in cells){int k=(i/Size/16)*((Size+15)/16)+i%Size/16;if(!groups.TryGetValue(k,out var g))groups[k]=g=new HashSet<int>();g.Add(i);}
   foreach(var pair in groups){var left=new SortedSet<int>(pair.Value);while(left.Count>0){int seed=left.Min;var found=new List<int>();var q=new Queue<int>();q.Enqueue(seed);left.Remove(seed);while(q.Count>0){int i=q.Dequeue();found.Add(i);foreach(int n in new[]{i-1,i+1,i-Size,i+Size})if(left.Remove(n))q.Enqueue(n);}Authored.Add(new DiscoveryRegion("fixture/"+prefix+"/"+pair.Key+"/"+seed,found,policy));}}
  }
  public bool Protected(int x,int y){int u=x-X-110,v=y-Y-35;return u>=-9&&u<=9&&Math.Abs(v)<=1||u>=-8&&u<=8&&(u+8)%5==0&&Math.Abs(v)<=5;}
  public bool Refillable(int x,int y)=>y==Y+4&&x>=X-6&&x<=X-4;
  public bool TrySolid(int x,int y,out bool solid){solid=true;if(x<X-100||x>X+235||y<Y-90||y>Y+85)return false;solid=!open.Contains(y*Size+x);return true;}
 }
 public sealed class DiscoveryIndex
 {
  public readonly List<DiscoveryRegion> Regions=new List<DiscoveryRegion>();public readonly Dictionary<int,DiscoveryRegion> ByCell=new Dictionary<int,DiscoveryRegion>();
  public readonly Dictionary<DualTile,List<DiscoveryRegion>> ByChunk=new Dictionary<DualTile,List<DiscoveryRegion>>();public readonly Dictionary<string,DiscoveryRegion> ById=new Dictionary<string,DiscoveryRegion>();
  public readonly int Width,Height;public long InitialQueries;public int CellCount=>ByCell.Count;
  public DiscoveryIndex(StreamingWorld world,DiscoveryFixture fixture):this(world,fixture.Authored) {}
  public DiscoveryIndex(StreamingWorld world,IEnumerable<DiscoveryRegion> authored)
  {
   Width=world.Width;Height=world.Height;foreach(var r in authored)Register(r);
   // Once at generation, derive small connectivity units from the initial floor. Never repeated on excavation/camera motion.
   var left=new SortedSet<int>();var queue=new Queue<int>();var found=new List<int>();
   for(int cy=0;cy<Height;cy+=16)for(int cx=0;cx<Width;cx+=16)
   {
    left.Clear();for(int y=cy;y<Math.Min(cy+16,Height);y++)for(int x=cx;x<Math.Min(cx+16,Width);x++){InitialQueries++;int i=y*Width+x;if(!world.IsSolid(x,y)&&!ByCell.ContainsKey(i))left.Add(i);}
    while(left.Count>0){int seed=left.Min;queue.Clear();found.Clear();queue.Enqueue(seed);left.Remove(seed);while(queue.Count>0){int i=queue.Dequeue(),x=i%Width,y=i/Width;found.Add(i);Visit(x-1,y);Visit(x+1,y);Visit(x,y-1);Visit(x,y+1);}Register(new DiscoveryRegion("generated/"+world.Seed+"/"+cx/16+"/"+cy/16+"/"+seed,found,DiscoveryPolicy.Bounded));}
    void Visit(int x,int y){if(x<cx||x>=Math.Min(cx+16,Width)||y<cy||y>=Math.Min(cy+16,Height))return;int i=y*Width+x;if(left.Remove(i))queue.Enqueue(i);}
   }
  }
  public void Register(DiscoveryRegion region)
  {
   if(ById.ContainsKey(region.Id))throw new ArgumentException("Duplicate region ID");Regions.Add(region);ById.Add(region.Id,region);var chunks=new HashSet<DualTile>();
   foreach(int i in region.Cells){if(i<0||i>=Width*Height||ByCell.ContainsKey(i))throw new ArgumentException("Overlapping/out-of-range discovery metadata");ByCell.Add(i,region);chunks.Add(StreamingCoordinates.Owner(i%Width,i/Width));}
   foreach(var chunk in chunks){if(!ByChunk.TryGetValue(chunk,out var list))ByChunk.Add(chunk,list=new List<DiscoveryRegion>());list.Add(region);}
  }
 }
 /// <summary>Memory and visual gates only. Never writes physical occupancy.</summary>
 public sealed class CaveDiscovery:IDisposable
 {
  public readonly StreamingWorld World;public readonly DiscoveryIndex Index;
  public readonly HashSet<int> Remembered=new HashSet<int>(),Visual=new HashSet<int>();public readonly HashSet<string> DiscoveredRegions=new HashSet<string>();
  public readonly List<DualTile> Released=new List<DualTile>();readonly HashSet<int> changes=new HashSet<int>(),visited=new HashSet<int>(),accessible=new HashSet<int>();
  readonly Queue<Node> queue=new Queue<Node>();readonly List<int> seeds=new List<int>(),group=new List<int>();readonly List<int> distances=new List<int>();
  readonly SortedSet<Release> releases=new SortedSet<Release>();public int RevealDistance=16,MaxWaveCells=256,MaxVisits=4096,Grouping=3,MaxReleases=128;public double Duration=.8,Clock;public int PolicyOverride=-1;
  // Zero preserves the Stage 5.2 schedule byte-for-byte; Stage 5.3 opts in.
  public int ImmediateDistance; public bool FastChambers; int GroupLimit=>FastChambers?4096:1024;
  public int LastVisits,LastChanged,LastRemembered,Notifications,DiscoveryEvents;public int PendingReleases=>releases.Count;
  public long MemoryEstimate=>(Index.CellCount*12L)+(Remembered.Count+Visual.Count)*24L+releases.Count*32L;
  readonly struct Node{public readonly int Id,D;public Node(int id,int d){Id=id;D=d;}}
  readonly struct Release:IComparable<Release>{public readonly int Id;public readonly double At;public readonly bool Urgent;public Release(int id,double at,bool urgent=false){Id=id;At=at;Urgent=urgent;}public int CompareTo(Release b){int c=b.Urgent.CompareTo(Urgent);if(c!=0)return c;c=At.CompareTo(b.At);return c!=0?c:Id.CompareTo(b.Id);}}
  public CaveDiscovery(StreamingWorld world,DiscoveryIndex index){World=world;Index=index;World.Changed+=OnChanged;}
  void OnChanged(TerrainCellChange change){Notifications++;if(!change.Solid)changes.Add(change.Y*World.Width+change.X);}
  public bool Known(int x,int y){int i=y*World.Width+x;return !Index.ByCell.ContainsKey(i)||Remembered.Contains(i);}
  public bool Shown(int x,int y){int i=y*World.Width+x;return !Index.ByCell.ContainsKey(i)||Visual.Contains(i);}
  bool Open(int i)=>i>=0&&i<World.Width*World.Height&&!World.IsSolid(i%World.Width,i/World.Width);
  void Neighbors(int i,int distance,Queue<Node> q,HashSet<int> seen,bool knownOnly=false)
  {int x=i%World.Width,y=i/World.Width;Visit(x-1,y);Visit(x+1,y);Visit(x,y-1);Visit(x,y+1);void Visit(int xx,int yy){if(xx<0||yy<0||xx>=World.Width||yy>=World.Height)return;int n=yy*World.Width+xx;if(seen.Count>=MaxVisits||seen.Contains(n)||!Open(n)||knownOnly&&!Known(xx,yy))return;seen.Add(n);q.Enqueue(new Node(n,distance));}}
  public void SeedKnown(IEnumerable<int> cells)
  {foreach(int cell in cells)if(Open(cell)&&Index.ByCell.TryGetValue(cell,out var region)){Remembered.Add(cell);Visual.Add(cell);if(DiscoveredRegions.Add(region.Id))DiscoveryEvents++;}}
  public int ProcessChanges(Point2 observer)
  {
   LastChanged=changes.Count;LastVisits=0;LastRemembered=0;if(changes.Count==0)return 0;
   int root=(int)Math.Floor(observer.Y)*World.Width+(int)Math.Floor(observer.X);accessible.Clear();queue.Clear();if(Open(root)){accessible.Add(root);queue.Enqueue(new Node(root,0));}
   while(queue.Count>0){var n=queue.Dequeue();LastVisits++;if(n.D<24)Neighbors(n.Id,n.D+1,queue,accessible,true);}
   seeds.Clear();foreach(int i in changes)if(accessible.Contains(i)){seeds.Add(i);}changes.Clear();int before=Remembered.Count;RevealSeeds(seeds);LastRemembered=Remembered.Count-before;return LastRemembered;
  }
  public int Explore(Point2 observer)
  {LastVisits=0;int before=Remembered.Count;int i=(int)Math.Floor(observer.Y)*World.Width+(int)Math.Floor(observer.X);if(Open(i))RevealWave(i);LastRemembered=Remembered.Count-before;return LastRemembered;}
  void RevealWave(int root){seeds.Clear();seeds.Add(root);RevealSeeds(seeds);}
  void RevealSeeds(List<int> roots)
  {
   queue.Clear();visited.Clear();foreach(int root in roots)if(visited.Add(root))queue.Enqueue(new Node(root,0));int added=0;
   while(queue.Count>0&&added<MaxWaveCells&&visited.Count<=MaxVisits)
   {
    var n=queue.Dequeue();LastVisits++;if(Index.ByCell.TryGetValue(n.Id,out var region)&&!Remembered.Contains(n.Id))
    {
     var policy=PolicyOverride<0?region.Policy:(DiscoveryPolicy)PolicyOverride;
     if(policy!=DiscoveryPolicy.Bounded&&region.Cells.Length<=GroupLimit){int count=RevealGroup(n.Id,region,policy);added+=count;}else {Remember(n.Id,region,n.D<=region.LocalImmediate?0:Seconds(region)*(n.D/(double)Math.Max(1,RevealDistance)),region.LocalImmediate>0&&n.D<=region.LocalImmediate);added++;}
    }
    if(ImmediateDistance>0&&n.D<=ImmediateDistance&&Remembered.Contains(n.Id))Accelerate(n.Id);
    if(n.D<RevealDistance)Neighbors(n.Id,n.D+1,queue,visited);
   }
  }
  int RevealGroup(int seed,DiscoveryRegion region,DiscoveryPolicy policy)
  {
   // Separate scratch from the outer wave. A metadata group still requires actual local floor connectivity.
   var localQueue=new Queue<Node>();var localSeen=new HashSet<int>();localQueue.Enqueue(new Node(seed,0));localSeen.Add(seed);group.Clear();distances.Clear();int max=0;
   while(localQueue.Count>0&&localSeen.Count<=GroupLimit){var n=localQueue.Dequeue();group.Add(n.Id);distances.Add(n.D);max=Math.Max(max,n.D);int x=n.Id%World.Width,y=n.Id/World.Width;Visit(x-1,y);Visit(x+1,y);Visit(x,y-1);Visit(x,y+1);void Visit(int xx,int yy){if(xx<0||yy<0||xx>=World.Width||yy>=World.Height)return;int i=yy*World.Width+xx;if(!localSeen.Contains(i)&&Index.ByCell.TryGetValue(i,out var r)&&ReferenceEquals(r,region)&&Open(i)){localSeen.Add(i);localQueue.Enqueue(new Node(i,n.D+1));}}}
   int count=0;for(int j=0;j<group.Count;j++){int d=distances[j]/Math.Max(1,Grouping)*Math.Max(1,Grouping);if(Remember(group[j],region,policy==DiscoveryPolicy.Immediate||ImmediateDistance>0&&distances[j]<=ImmediateDistance||region.LocalImmediate>0&&distances[j]<=region.LocalImmediate?0:Seconds(region)*d/Math.Max(1,max),region.LocalImmediate>0&&distances[j]<=region.LocalImmediate))count++;}return count;
  }
  double Seconds(DiscoveryRegion r){double seconds=r.RevealSeconds<0?Duration:r.RevealSeconds;return FastChambers&&r.Policy!=DiscoveryPolicy.Bounded?Math.Min(.12,seconds):seconds;}
  void Accelerate(int id){if(releases.Contains(new Release(id,Clock,true)))return;releases.RemoveWhere(r=>r.Id==id);if(!Visual.Contains(id))releases.Add(new Release(id,Clock));}
  bool Remember(int i,DiscoveryRegion region,double delay,bool urgent=false)
  {if(!Remembered.Add(i))return false;if(DiscoveredRegions.Add(region.Id))DiscoveryEvents++;releases.Add(new Release(i,Clock+Math.Max(0,delay),urgent));return true;}
  public int Tick(double dt)
  {Released.Clear();Clock+=Math.Max(0,dt);while(releases.Count>0&&(Released.Count<(FastChambers?Math.Max(256,MaxReleases):MaxReleases)||releases.Min.Urgent)){var item=releases.Min;if(item.At>Clock)break;releases.Remove(item);if(Visual.Add(item.Id))Released.Add(new DualTile(item.Id%World.Width,item.Id/World.Width));}return Released.Count;}
  public bool ContentAllowed(Point2 observer,Point2 actor,double senseRange=32)
  {
   int x=(int)Math.Floor(actor.X),y=(int)Math.Floor(actor.Y);if(!Known(x,y)||!Shown(x,y))return false;double dx=actor.X-observer.X,dy=actor.Y-observer.Y,d=Math.Sqrt(dx*dx+dy*dy);if(d>senseRange)return false;int n=Math.Max(1,(int)Math.Ceiling(d*5));for(int i=0;i<=n;i++)if(World.IsSolid((int)Math.Floor(observer.X+dx*i/n),(int)Math.Floor(observer.Y+dy*i/n)))return false;return true;
  }
  public void WriteMemory(BinaryWriter w)
  {
   var ids=new List<string>(DiscoveredRegions);ids.Sort(StringComparer.Ordinal);w.Write(ids.Count);foreach(string id in ids){var r=Index.ById[id];w.Write(id);var bits=new byte[(r.Cells.Length+7)/8];int count=0;for(int n=0;n<r.Cells.Length;n++)if(Remembered.Contains(r.Cells[n])){bits[n/8]|=(byte)(1<<(n%8));count++;}bool full=count==r.Cells.Length;w.Write(full);if(!full)w.Write(bits);}
  }
  public void ReadMemory(BinaryReader reader)
  {
   int count=reader.ReadInt32();if(count<0||count>Index.Regions.Count)throw new InvalidDataException("Invalid discovery count");for(int n=0;n<count;n++){string id=reader.ReadString();if(!Index.ById.TryGetValue(id,out var r)||!DiscoveredRegions.Add(id))throw new InvalidDataException("Unknown or duplicate discovery unit");bool full=reader.ReadBoolean();byte[] bits=full?null:reader.ReadBytes((r.Cells.Length+7)/8);if(!full&&bits.Length!=(r.Cells.Length+7)/8)throw new EndOfStreamException();for(int j=0;j<r.Cells.Length;j++)if(full||(bits[j/8]&(1<<(j%8)))!=0){Remembered.Add(r.Cells[j]);Visual.Add(r.Cells[j]);}}DiscoveryEvents=DiscoveredRegions.Count;changes.Clear();
  }
  public void Dispose(){World.Changed-=OnChanged;changes.Clear();releases.Clear();Released.Clear();}
 }
 public sealed class DiscoveredTerrainPresentation:IOccupancySource
 {
  public readonly CaveDiscovery Discovery;public int Width=>Discovery.World.Width;public int Height=>Discovery.World.Height;
  public DiscoveredTerrainPresentation(CaveDiscovery discovery){Discovery=discovery;}
  public bool IsSolid(int x,int y)=>Discovery.World.IsSolid(x,y)||!Discovery.Shown(x,y);
 }
 public sealed class DiscoveryExperiment:IDisposable
 {
  public readonly DiscoveryFixture Fixture;public readonly StreamingWorld World;public readonly DiscoveryIndex Index;public readonly double WorldMs,IndexMs;public readonly CaveDiscovery Discovery;public readonly DiscoveredTerrainPresentation Presentation;
  public DiscoveryExperiment(int size=1024,int seed=47045){long t=System.Diagnostics.Stopwatch.GetTimestamp();Fixture=new DiscoveryFixture(size);World=new StreamingWorld(size,size,seed,Fixture);WorldMs=(System.Diagnostics.Stopwatch.GetTimestamp()-t)*1000.0/System.Diagnostics.Stopwatch.Frequency;t=System.Diagnostics.Stopwatch.GetTimestamp();Index=new DiscoveryIndex(World,Fixture);IndexMs=(System.Diagnostics.Stopwatch.GetTimestamp()-t)*1000.0/System.Diagnostics.Stopwatch.Frequency;Discovery=new CaveDiscovery(World,Index);Presentation=new DiscoveredTerrainPresentation(Discovery);}
  public void Save(Stream stream)
  {using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){w.Write(0x53473532);w.Write(1);w.Write(DiscoveryFixture.Identity);w.Write(World.Width);w.Write(World.Seed);World.WriteDeltas(w);var temporary=new List<int>(World.TemporaryOpenCells);temporary.Sort();w.Write(temporary.Count);foreach(int i in temporary)w.Write(i);Discovery.WriteMemory(w);w.Write(Discovery.RevealDistance);w.Write(Discovery.Duration);w.Write(Discovery.Grouping);w.Write(Discovery.PolicyOverride);}}
  public static DiscoveryExperiment Load(Stream stream)
  {
   using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
   {if(r.ReadInt32()!=0x53473532||r.ReadInt32()!=1||r.ReadString()!=DiscoveryFixture.Identity)throw new InvalidDataException("Unknown Stage52 snapshot");var s=new DiscoveryExperiment(r.ReadInt32(),r.ReadInt32());try{s.World.ReadDeltas(r);int n=r.ReadInt32();if(n<0||n>s.Fixture.Rubble.Count)throw new InvalidDataException("Invalid rubble delta count");for(int i=0;i<n;i++){int cell=r.ReadInt32();if(cell<0||cell>=s.World.Width*s.World.Height||!s.World.IsRefillable(cell%s.World.Width,cell/s.World.Width)||!s.World.Excavate(cell%s.World.Width,cell/s.World.Width))throw new InvalidDataException("Invalid rubble delta");}s.Discovery.ReadMemory(r);int distance=r.ReadInt32();double duration=r.ReadDouble();int grouping=r.ReadInt32(),policy=r.ReadInt32();if(distance<1||distance>64||double.IsNaN(duration)||double.IsInfinity(duration)||duration<0||duration>10||grouping<1||grouping>16||policy< -1||policy>2)throw new InvalidDataException("Invalid reveal tuning");s.Discovery.RevealDistance=distance;s.Discovery.Duration=duration;s.Discovery.Grouping=grouping;s.Discovery.PolicyOverride=policy;return s;}catch{s.Dispose();throw;}}
  }
  public void Dispose()=>Discovery.Dispose();
 }
}
