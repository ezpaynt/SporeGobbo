using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SporeGobbo.Geometry;
using SporeGobbo.CampLifecycle;
namespace SporeGobbo.CampCandidate
{
 public enum CampCell { Open, Permanent, Expansion, Residential, FirstBuddy, FirstDeath, Shop, FightPit }
 public sealed class CampMarker
 {
  public string Id{get;} public string Name{get;} public Point2 Anchor{get;} public Point2 Interaction{get;} public CampCell Gate{get;}public string MaterialId{get;}
  public CampMarker(string id,string name,Point2 anchor,Point2 interaction,CampCell gate=CampCell.Open,string material="terrain.stone"){Id=id;Name=name;Anchor=anchor;Interaction=interaction;Gate=gate;MaterialId=material;}
 }
 public sealed class CampBedroom
 {
  public string Id{get;}public Point2 Entrance{get;}public IReadOnlyList<DualTile> Cells{get;}public IReadOnlyList<Point2> Beds{get;}public int FirstSlot{get;}
  public CampBedroom(string id,Point2 entrance,List<DualTile> cells,List<Point2> beds,int first){Id=id;Entrance=entrance;Cells=new List<DualTile>(cells).AsReadOnly();Beds=new List<Point2>(beds).AsReadOnly();FirstSlot=first;}
 }
 /// Immutable full-coverage authored base; no mutable occupancy or camera state lives here.
 public sealed class AuthoredCamp:IAuthoredRegionDefinition
 {
  public const string RegionId="camp.candidate.5.6.v1";public const int Size=192;public const double CellSize=.6;
  readonly string identity;readonly CampCell[] cells;public IReadOnlyList<CampMarker> Markers{get;}public IReadOnlyList<CampBedroom> Bedrooms{get;}public IReadOnlyList<RegionLandmarkStamp> Landmarks{get;}
  public AuthoredCamp(CampCell[] cells,List<CampMarker> markers,List<CampBedroom> rooms,string identity=RegionId){this.identity=identity;if(cells.Length!=Size*Size)throw new ArgumentException();this.cells=(CampCell[])cells.Clone();Markers=new List<CampMarker>(markers).AsReadOnly();Bedrooms=new List<CampBedroom>(rooms).AsReadOnly();var stamps=new List<RegionLandmarkStamp>();foreach(var m in markers){var footprint=new List<DualTile>();for(int y=(int)m.Anchor.Y-2;y<=(int)m.Anchor.Y+2;y++)for(int x=(int)m.Anchor.X-2;x<=(int)m.Anchor.X+2;x++)if(Protected(x,y))footprint.Add(new DualTile(x,y));if(footprint.Count>0)stamps.Add(new RegionLandmarkStamp(identity+"/structure/"+m.Id,m.MaterialId,footprint,new ProjectionBounds(new Point2(m.Anchor.X-2,m.Anchor.Y-3),new Point2(m.Anchor.X+2,m.Anchor.Y+1))));}Landmarks=stamps.AsReadOnly();}
  public string Id=>identity;public int Width=>Size;public int Height=>Size;public double WorldUnitsPerCell=>CellSize;public Point2 WorldOrigin=>new Point2(0,0);
  public RegionCellInfo CellAt(int x,int y){var c=Category(x,y);string material=c==CampCell.Open?"terrain.floor":c==CampCell.Permanent?"terrain.stone":"terrain.earth";if(c==CampCell.Permanent)foreach(var l in Landmarks)foreach(var p in l.ProtectedCells)if(p.X==x&&p.Y==y)material=l.MaterialId;return new RegionCellInfo(c!=CampCell.Open,c==CampCell.Permanent,material,c.ToString());}
  public CampCell Category(int x,int y)=>x<0||y<0||x>=Size||y>=Size?CampCell.Permanent:cells[y*Size+x];
  public bool TrySolid(int x,int y,out bool solid){solid=Category(x,y)!=CampCell.Open;return x>=0&&y>=0&&x<Size&&y<Size;}
  public bool Protected(int x,int y)=>Category(x,y)==CampCell.Permanent;
  public CampMarker Marker(string id){foreach(var m in Markers)if(m.Id==id)return m;throw new ArgumentException(id);}
  public int Capacity{get{int n=0;foreach(var r in Bedrooms)n+=r.Beds.Count;return n;}}
  public CampBedroom RoomForSlot(int slot){foreach(var r in Bedrooms)if(slot>=r.FirstSlot&&slot<r.FirstSlot+r.Beds.Count)return r;return null;}
  public string Fingerprint(){ulong h=14695981039346656037UL;foreach(var c in cells){h^=(byte)c;h*=1099511628211UL;}foreach(var m in Markers)foreach(char c in m.Id+":"+m.Anchor.X+","+m.Anchor.Y){h^=c;h*=1099511628211UL;}return h.ToString("X16");}
  public static AuthoredCamp Create()
  {
   var a=new CampCell[Size*Size];for(int i=0;i<a.Length;i++)a[i]=CampCell.Permanent;
   void Ellipse(double x,double y,double rx,double ry,CampCell kind){for(int v=Math.Max(1,(int)(y-ry-2));v<Math.Min(Size-1,y+ry+2);v++)for(int u=Math.Max(1,(int)(x-rx-2));u<Math.Min(Size-1,x+rx+2);u++){double dx=(u-x)/rx,dy=(v-y)/ry,t=Math.Atan2(dy,dx),edge=1+.05*Math.Sin(t*5)+.035*Math.Cos(t*3);if(dx*dx+dy*dy<edge*edge)a[v*Size+u]=kind;}}
   void Tube(CampCell kind,double radius,params Point2[] points){for(int i=1;i<points.Length;i++){var p=points[i-1];var q=points[i];double d=Math.Sqrt(Math.Pow(p.X-q.X,2)+Math.Pow(p.Y-q.Y,2));for(int n=0;n<=Math.Ceiling(d*2);n++){double t=n/Math.Max(1,Math.Ceiling(d*2));Ellipse(p.X+(q.X-p.X)*t,p.Y+(q.Y-p.Y)*t,radius,radius,kind);}}}
   Point2 P(double x,double y)=>new Point2(x,y);
   // Authored lobe chains, with a public loop; no runtime route repair or random generation.
   Ellipse(88,92,21,15,CampCell.Open);Ellipse(75,89,13,11,CampCell.Open);Ellipse(97,101,14,11,CampCell.Open);
   Ellipse(60,91,11,8,CampCell.Open);
   Tube(CampCell.Open,4.5,P(76,94),P(65,99),P(58,108),P(64,119),P(82,126),P(104,119),P(114,103),P(101,95));
   Tube(CampCell.Open,4,P(82,126),P(92,135),P(94,145));Ellipse(94,144,9,7,CampCell.Open);
   Tube(CampCell.Open,4,P(89,82),P(89,75),P(89,67),P(97,58));Ellipse(97,58,9,7,CampCell.Open);
   Tube(CampCell.Open,3.8,P(60,111),P(51,114),P(49,126),P(50,139));
   Tube(CampCell.Open,3.8,P(108,116),P(120,118),P(124,128),P(126,140));
   // Optional diggable space has no critical route or residential ownership.
   Ellipse(76,108,7,5,CampCell.Expansion);Ellipse(103,133,7,5,CampCell.Expansion);
   Tube(CampCell.FirstBuddy,4,P(107,98),P(117,101),P(126,98));Ellipse(127,98,11,9,CampCell.FirstBuddy);
   // Convenient memorial niche is physically accessible before its interaction unlocks.
   Tube(CampCell.Open,4,P(89,75),P(81,70),P(77,65));Ellipse(76,62,7,7,CampCell.Open);
   // Ordinary earth between the two niches: only fill untouched structural mass.
   var beforeShortcut=(CampCell[])a.Clone();Ellipse(87,59,10,8,CampCell.Expansion);
   for(int i=0;i<a.Length;i++)if(beforeShortcut[i]!=CampCell.Permanent)a[i]=beforeShortcut[i];
   Tube(CampCell.Shop,4,P(128,94),P(134,80),P(139,68));Ellipse(141,67,12,9,CampCell.Shop);
   Tube(CampCell.FightPit,4,P(146,60),P(151,51),P(152,43));Ellipse(152,42,13,10,CampCell.FightPit);
   // Shapeable upper-central earth: preserve authored open passages and every reserved domain.
   // Rooms and station backings are authored afterwards and retain their own protection.
   var beforeCentral=(CampCell[])a.Clone();Ellipse(113,76,20,23,CampCell.Expansion);
   for(int i=0;i<a.Length;i++)if(beforeCentral[i]!=CampCell.Permanent)a[i]=beforeCentral[i];
   // Irregular shoulders define the cozy Squad nook without enclosing the central region.
   Ellipse(137,98,2.5,3.5,CampCell.Permanent);Ellipse(122,94,2,2.5,CampCell.Permanent);
   var rooms=new List<CampBedroom>();int slot=1;
   void Room(string id,int x,int y,int ex,int ey,int capacity){var before=(CampCell[])a.Clone();Ellipse(x,y,9,6.5,CampCell.Residential);Tube(CampCell.Residential,3,P(x,y),P(ex,ey));var owned=new List<DualTile>();for(int v=0;v<Size;v++)for(int u=0;u<Size;u++)if(a[v*Size+u]==CampCell.Residential&&before[v*Size+u]!=CampCell.Residential)owned.Add(new DualTile(u,v));var beds=new List<Point2>();for(int j=0;j<capacity;j++)beds.Add(P(x-4+j*4+.5,y+.5));rooms.Add(new CampBedroom(id,P(ex+.5,ey+.5),owned,beds,slot));slot+=capacity;}
   Room("west.fern-pocket",65,74,73,85,3);Room("east.fire-pocket",113,75,104,88,3);
   Room("east.root-pocket",141,111,125,118,3);Room("east.later-pocket",141,140,126,140,1);
   // Entrances are public staging points outside the excavation domain.
   foreach(var r in rooms)Ellipse(r.Entrance.X-.5,r.Entrance.Y-.5,3,3,CampCell.Open);
   var markers=new List<CampMarker>{new CampMarker("fire","Fire",P(88.5,91.5),P(88.5,97.5)),new CampMarker("arrival.first","First arrival",P(94.5,144.5),P(94.5,144.5)),new CampMarker("run.departure","Run departure / return",P(97.5,57.5),P(97.5,62.5)),new CampMarker("squad","Squad selection",P(128.5,96.5),P(124.5,102.5),CampCell.FirstBuddy),new CampMarker("bones","Old Bones",P(76.5,58.5),P(77.5,64.5),CampCell.FirstDeath,"terrain.bone"),new CampMarker("storage","Resource storage",P(58.5,90.5),P(63.5,92.5)),new CampMarker("shop.reserved","Shop  /  reserved",P(142.5,65.5),P(140.5,70.5),CampCell.Shop),new CampMarker("fight.reserved","Fight Pit  /  reserved",P(153.5,40.5),P(151.5,46.5),CampCell.FightPit)};
   foreach(var m in markers)if(m.Id!="arrival.first"){
    Ellipse(Math.Floor(m.Anchor.X),Math.Floor(m.Anchor.Y),1.6,1.6,CampCell.Permanent);
    // Protected backing belongs to the station, not to an approach/progression corridor.
    for(int y=(int)m.Anchor.Y-3;y<=(int)m.Anchor.Y;y++)for(int x=(int)m.Anchor.X-3;x<=(int)m.Anchor.X+3;x++)a[y*Size+x]=CampCell.Permanent;
   }
   return new AuthoredCamp(a,markers,rooms);
  }
 }
 public sealed class CampBatch
 {
  public readonly int Revision;public readonly string Reason;public readonly IReadOnlyList<DualTile> Cells;
  public CampBatch(int revision,string reason,List<DualTile> cells){Revision=revision;Reason=reason;Cells=cells.AsReadOnly();}
 }
 public sealed class PreviewResident
 {
  public readonly string Id;public int Slot;public bool Alive=true,InSquad;public Point2 Position;
  public PreviewResident(string id,Point2 position){Id=id;Position=position;}
 }
 /// Session-only state, deliberately no Unity object, GameState, save API or scene routing.
 public sealed class CampSession
 {
  public AuthoredCamp Definition{get;}readonly StreamingWorld world;readonly Func<int,int,bool> excavate;
  public IOccupancySource Terrain=>world;public int Revision{get;private set;}public int Established{get;private set;}public int Deaths{get;private set;}public int Returns{get;private set;}
  public int Mushrooms{get;private set;}public int Shinies{get;private set;}public bool Discovered{get;private set;}public bool ShopInspected{get;private set;}public bool FightInspected{get;private set;}public bool Recovered{get;private set;}
  readonly List<PreviewResident> residents=new List<PreviewResident>();readonly IReadOnlyList<PreviewResident> residentView;public IReadOnlyList<PreviewResident> Residents=>residentView;public event Action<CampBatch> Committed;
  public readonly List<Point2> ConstructorTrail=new List<Point2>();public int Living{get{int n=0;foreach(var r in residents)if(r.Alive)n++;return n;}}
  public bool FirstBuddy=>CampTerrainProgressionPolicy.IsFirstBuddyRecruited(Living,Established,Deaths);
  public bool FirstDeath=>CampTerrainProgressionPolicy.IsFirstDeath(Deaths);
  public readonly bool IsolatedResidential;
  public CampSession(AuthoredCamp definition,bool isolatedResidential=false){IsolatedResidential=isolatedResidential;residentView=residents.AsReadOnly();Definition=definition;world=new StreamingWorld(AuthoredCamp.Size,AuthoredCamp.Size,56056,definition);excavate=world.TakeExcavationControl();}
  // Geometry binding is intentionally explicit; all candidate code mutates only through this session.
  public StreamingWorld BindReadOnlyGeometry()=>world;
  public bool Active(CampCell gate)=>gate==CampCell.Open||gate==CampCell.FirstBuddy&&FirstBuddy||gate==CampCell.FirstDeath&&FirstDeath||gate==CampCell.Shop&&ShopInspected||gate==CampCell.FightPit&&FightInspected;
  int Commit(IEnumerable<DualTile> cells,string reason,Func<DualTile,bool> permitted){var accepted=new List<DualTile>();var seen=new HashSet<DualTile>();foreach(var c in cells)if(seen.Add(c)&&permitted(c)&&excavate(c.X,c.Y))accepted.Add(c);if(accepted.Count>0){Revision++;Committed?.Invoke(new CampBatch(Revision,reason,accepted));}return accepted.Count;}
  public int DigPlayer(IEnumerable<DualTile> cells)=>Commit(cells,"player dig",c=>{var k=Definition.Category(c.X,c.Y);return k==CampCell.Expansion&&CampSpatialPolicy.CanDig(CampDigCategory.NormalCampDiggable,TerrainDigAuthority.Player,false);});
  // Opt-in Stage60 edits share the exact Commit transaction used by every derived consumer.
  public int DigIsolatedResidence(IEnumerable<DualTile> cells,TerrainDigAuthority authority,IReadOnlyCollection<DualTile> authorization=null)
  {
   if(!IsolatedResidential)return 0;
   var domain=authorization==null?null:new HashSet<DualTile>(authorization);
   return Commit(cells,"isolated Camp excavation",c=>Definition.Category(c.X,c.Y)==CampCell.Expansion&&(authority==TerrainDigAuthority.Player||authority==TerrainDigAuthority.Buddy) || Definition.Category(c.X,c.Y)==CampCell.Residential &&
    (authority==TerrainDigAuthority.Player || authority==TerrainDigAuthority.ResidentialProgression&&domain!=null&&domain.Contains(c)));
  }
  public bool OpenIsolatedRoot()
  {
   if(!IsolatedResidential||Living<=0)return false;
   var cells=new List<DualTile>();for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(Definition.Category(x,y)==CampCell.FirstBuddy)cells.Add(new DualTile(x,y));
   int n=Commit(cells,"stage60.root.first-dorm / assisted breakthrough",c=>Definition.Category(c.X,c.Y)==CampCell.FirstBuddy);
   if(n>0)Discover();return n>0;
  }
  public bool RemoveIsolatedBuddy(string id)
  {if(!IsolatedResidential)return false;var r=residents.Find(p=>p.Id==id&&p.Alive);if(r==null)return false;r.Alive=false;r.Slot=0;Deaths++;Reconcile();return true;}
  public bool CompleteIsolatedBed(string workerId,int slot,IReadOnlyCollection<DualTile> required,Point2 rest)
  {
   if(!IsolatedResidential||slot!=Established+1||slot>Definition.Capacity)return false;
   var worker=residents.Find(r=>r.Id==workerId&&r.Alive&&r.Slot==0);if(worker==null)return false;
   foreach(var c in required)if(Terrain.IsSolid(c.X,c.Y))return false;
   if(!Fits(rest,3)||Route(rest,Definition.Marker("fire").Interaction,3).Count==0)return false;
   Established=slot;worker.Slot=slot;Reconcile();return worker.Slot==slot;
  }
  public void Discover(){Discovered=true;}
  public PreviewResident AddBuddy(){if(residents.Count>=64)throw new InvalidOperationException("Preview resident safety cap64");var p=Definition.Marker("fire").Interaction;var r=new PreviewResident("preview.buddy."+(residents.Count+1).ToString("D3"),new Point2(p.X+(residents.Count%4)*3,p.Y+4+(residents.Count/4)*3));if(!Fits(r.Position,3))r.Position=p;residents.Add(r);Reconcile();UnlockKnownAccess();return r;}
  public void FakeReturn(int buddies,int mushrooms,int shinies){if(buddies<0||buddies>8||mushrooms<0||mushrooms>99||shinies<0||shinies>99||residents.Count+buddies>64)throw new ArgumentException("Return accepts0â€“8 buddies and0â€“99 resources;64 resident safety cap");Discovered=true;Returns++;Recovered=false;Mushrooms+=mushrooms;Shinies+=shinies;for(int i=0;i<buddies;i++)AddBuddy();}
  public bool BuddyDied(){foreach(var r in residents)if(r.Alive){r.Alive=false;r.Slot=0;Deaths++;Reconcile();UnlockKnownAccess();return true;}return false;}
  void Reconcile(){var records=new List<ResidentialOccupantRecord>();foreach(var r in residents)records.Add(new ResidentialOccupantRecord(r.Id,r.Slot,r.Alive));var result=CampSpatialPolicy.ResolveResidentialOccupancy(records,Established,Definition.Capacity);foreach(var r in residents)r.Slot=result.Assignments[r.Id];}
  void UnlockKnownAccess(){if(IsolatedResidential)return;var cells=new List<DualTile>();for(int y=0;y<AuthoredCamp.Size;y++)for(int x=0;x<AuthoredCamp.Size;x++){var k=Definition.Category(x,y);if(k==CampCell.FirstBuddy&&FirstBuddy||k==CampCell.FirstDeath&&FirstDeath)cells.Add(new DualTile(x,y));}Commit(cells,"milestone access excavation (preview simulation)",c=>true);}
  public string Advance(){if(!Discovered){Discover();return "Ordinary discovered Camp";}if(Living==0){AddBuddy();return "First Buddy; Squad approach opened";}if(Established<Math.Min(Living,Definition.Capacity))return "Use Dig Bedrooms: build the next authorized bed";if(!FirstDeath)return "Use Buddy Died after adding a second Buddy to inspect memorial progression";if(!ShopInspected){ShopInspected=true;OpenReservation(CampCell.Shop);return "Shop reservation inspectable; no shop gameplay";}if(!FightInspected){FightInspected=true;OpenReservation(CampCell.FightPit);return "Fight Pit reservation inspectable after Shop; no fight gameplay";}return "All preview milestones reached; later growth uses Add Buddy / Dig Bedrooms";}
  void OpenReservation(CampCell kind){var cells=new List<DualTile>();for(int y=0;y<AuthoredCamp.Size;y++)for(int x=0;x<AuthoredCamp.Size;x++)if(Definition.Category(x,y)==kind)cells.Add(new DualTile(x,y));Commit(cells,"reserved facility inspection only",c=>true);}
  public void Recover(){Recovered=true;}
  readonly Dictionary<string,List<Point2>> wanderRoutes=new Dictionary<string,List<Point2>>();readonly Dictionary<string,int> wanderRevisions=new Dictionary<string,int>();readonly Dictionary<string,bool> toFire=new Dictionary<string,bool>();
  public void TickSocialMovement(double dt){if(!Recovered)return;foreach(var r in residents){if(!r.Alive||Construction!=null&&Construction.Worker==r)continue;if(!wanderRoutes.TryGetValue(r.Id,out var path)||!wanderRevisions.TryGetValue(r.Id,out int revision)||revision!=Revision||path.Count==0){bool fire=!toFire.TryGetValue(r.Id,out bool prior)||!prior;toFire[r.Id]=fire;Point2 target=Definition.Marker(fire?"fire":"storage").Interaction;if(!fire&&r.Slot>0){var room=Definition.RoomForSlot(r.Slot);target=room.Beds[r.Slot-room.FirstSlot];}path=Route(r.Position,target,3);wanderRoutes[r.Id]=path;wanderRevisions[r.Id]=Revision;}if(path.Count==0)continue;var goal=path[0];double dx=goal.X-r.Position.X,dy=goal.Y-r.Position.Y,d=Math.Sqrt(dx*dx+dy*dy);if(d<.05){path.RemoveAt(0);continue;}double step=Math.Min(d,Math.Max(0,dt)*3);r.Position=Move(r.Position,new Point2(dx/d*step,dy/d*step),3);}}

  // Session-owned, bounded navigation workspace. Queries are serial on the owning simulation thread.
  int[] routePrevious,routeSeen,routeQueue;int routeGeneration;
  public List<Point2> Route(Point2 from,Point2 to,int size)
  {using(GeometryTrace.Measure(GeometryPhase.Navigation)){
   var result=new List<Point2>();int w=AuthoredCamp.Size,start=(int)Math.Floor(from.Y)*w+(int)Math.Floor(from.X),goal=(int)Math.Floor(to.Y)*w+(int)Math.Floor(to.X);if(!Fits(from,size)||!Fits(to,size))return result;
   if(routePrevious==null){routePrevious=new int[w*w];routeSeen=new int[w*w];routeQueue=new int[w*w];}
   if(routeGeneration==int.MaxValue){Array.Clear(routeSeen,0,routeSeen.Length);routeGeneration=0;}int generation=++routeGeneration;
   int head=0,tail=1;routeQueue[0]=start;routeSeen[start]=generation;routePrevious[start]=-1;bool found=false;
   while(head<tail){int i=routeQueue[head++];if(i==goal){found=true;break;}int x=i%w,y=i/w;for(int direction=0;direction<4;direction++){int n=direction==0?i-1:direction==1?i+1:direction==2?i-w:i+w;int nx=n%w,ny=n/w;if(nx<0||ny<0||nx>=w||ny>=w||Math.Abs(nx-x)+Math.Abs(ny-y)!=1||routeSeen[n]==generation)continue;routeSeen[n]=generation;if(!Fits(new Point2(nx+.5,ny+.5),size))continue;routePrevious[n]=i;routeQueue[tail++]=n;}}
   if(!found)return result;for(int i=goal;i!=-1;i=routePrevious[i])result.Add(new Point2(i%w+.5,i/w+.5));result.Reverse();return result;
  }}
  public bool Fits(Point2 p,int size){double h=size*.5-.001;for(int y=(int)Math.Floor(p.Y-h);y<=(int)Math.Floor(p.Y+h);y++)for(int x=(int)Math.Floor(p.X-h);x<=(int)Math.Floor(p.X+h);x++)if(world.IsSolid(x,y))return false;return true;}
  public Point2 Move(Point2 from,Point2 delta,int size){double d=Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y);int n=Math.Max(1,(int)Math.Ceiling(d/.2));var step=delta*(1.0/n);for(int i=0;i<n;i++){var p=from+step;if(Fits(p,size))from=p;else{p=new Point2(from.X+step.X,from.Y);if(Fits(p,size))from=p;p=new Point2(from.X,from.Y+step.Y);if(Fits(p,size))from=p;}}return from;}
  public bool BeginConstruction(out string failure)
  {
   failure="";if(Construction!=null){failure="Construction already in progress";return false;}Reconcile();PreviewResident worker=null;foreach(var r in residents)if(r.Alive&&r.Slot==0){worker=r;break;}if(worker==null){failure="No living Buddy needs a bed";return false;}
   int slot=Established+1;var room=Definition.RoomForSlot(slot);if(room==null){failure="Ten-bed candidate capacity reached; fifty-bed master plan deferred";return false;}
   if(!CampArrivalPolicy.CanBeginReservedConstruction(slot,slot-1,Established)){failure="Sequential dependency refused";return false;}
   ConstructorTrail.Clear();var approach=Route(worker.Position,room.Entrance,3);if(approach.Count==0){failure="Constructor cannot reach entrance with3-cell clearance";return false;}ConstructorTrail.AddRange(approach);var stand=room.Entrance;
   var owned=new HashSet<DualTile>(room.Cells);var blocked=new HashSet<DualTile>();foreach(var c in room.Cells)if(world.IsSolid(c.X,c.Y))blocked.Add(c);
   // Finite simulated planning before any mutation: dig within1.2cell radius at<=2.7cell reach,
   // then advance only into full3cell clearance in the same authorization domain.
   var steps=new List<List<DualTile>>();var stands=new List<Point2>();var simulation=new HashSet<DualTile>();int guard=room.Cells.Count*8;
   bool FitSim(Point2 p){double h=1.499;for(int y=(int)Math.Floor(p.Y-h);y<=(int)Math.Floor(p.Y+h);y++)for(int x=(int)Math.Floor(p.X-h);x<=(int)Math.Floor(p.X+h);x++)if(world.IsSolid(x,y)&&!simulation.Contains(new DualTile(x,y)))return false;return true;}
   while(blocked.Count>0&&guard-->0){DualTile best=default;double score=double.MaxValue;bool found=false;foreach(var c in blocked){double dx=c.X+.5-stand.X,dy=c.Y+.5-stand.Y,d=dx*dx+dy*dy;if(d<=2.7*2.7&&(d<score||d==score&&(c.Y<best.Y||c.Y==best.Y&&c.X<best.X))){best=c;score=d;found=true;}}
    if(found){var cut=new List<DualTile>();foreach(var c in blocked)if(Math.Pow(c.X-best.X,2)+Math.Pow(c.Y-best.Y,2)<=1.2*1.2)cut.Add(c);foreach(var c in cut){blocked.Remove(c);simulation.Add(c);}steps.Add(cut);stands.Add(stand);continue;}
    // BFS over the currently opened room to a reachable new dig stand.
    var q=new Queue<Point2>();var seen=new HashSet<DualTile>();q.Enqueue(stand);seen.Add(new DualTile((int)stand.X,(int)stand.Y));bool moved=false;
    while(q.Count>0&&!moved){var p=q.Dequeue();foreach(var c in blocked)if(Math.Pow(c.X+.5-p.X,2)+Math.Pow(c.Y+.5-p.Y,2)<=2.7*2.7){stand=p;moved=true;break;}if(moved)break;foreach(var v in new[]{new Point2(1,0),new Point2(-1,0),new Point2(0,1),new Point2(0,-1)}){var n=p+v;var key=new DualTile((int)n.X,(int)n.Y);if(owned.Contains(key)&&seen.Add(key)&&FitSim(n))q.Enqueue(n);}}
    if(!moved){failure="Authorized dig cannot advance from entrance; candidate requires revision";return false;}
   }
   if(blocked.Count>0){failure="Construction planning guard exceeded";return false;}
   Construction=new ConstructionWork(worker,room,slot,steps,stands);return true;
  }
  public sealed class ConstructionWork
  {
   public readonly PreviewResident Worker;public readonly CampBedroom Room;public readonly int Slot;
   public readonly List<List<DualTile>> Cuts;public readonly List<Point2> Stands;
   internal int Index,Cursor;internal double Timer;internal List<Point2> Path;
   public ConstructionWork(PreviewResident worker,CampBedroom room,int slot,List<List<DualTile>> cuts,List<Point2> stands){Worker=worker;Room=room;Slot=slot;Cuts=cuts;Stands=stands;}
  }
  public ConstructionWork Construction{get;private set;}
  public string ConstructionStatus{get;private set;}="";
  public bool TickConstruction(double dt)
  {
   var c=Construction;if(c==null)return false;
   Point2 target=c.Index<c.Cuts.Count?c.Stands[c.Index]:c.Room.Beds[c.Slot-c.Room.FirstSlot];
   if(c.Path==null){c.Path=Route(c.Worker.Position,target,3);c.Cursor=0;if(c.Path.Count==0){ConstructionStatus="Constructor cannot reach next validated stand";Construction=null;return false;}}
   double budget=Math.Max(0,dt)*6;while(c.Cursor<c.Path.Count&&budget>0){var p=c.Worker.Position;var q=c.Path[c.Cursor];double dx=q.X-p.X,dy=q.Y-p.Y,d=Math.Sqrt(dx*dx+dy*dy);if(d<.001){c.Cursor++;continue;}double length=Math.Min(budget,d);var next=Move(p,new Point2(dx/d*length,dy/d*length),3);if(next.Equals(p)){ConstructionStatus="Constructor blocked while following validated path";Construction=null;return false;}c.Worker.Position=next;budget-=length;}
   if(c.Cursor<c.Path.Count)return true;
   if(c.Index<c.Cuts.Count){c.Timer+=Math.Max(0,dt);if(c.Timer<.18)return true;c.Timer=0;var owned=new HashSet<DualTile>(c.Room.Cells);Commit(c.Cuts[c.Index],"bedroom "+c.Room.Id,cell=>owned.Contains(cell)&&Definition.Category(cell.X,cell.Y)==CampCell.Residential);ConstructorTrail.Add(c.Worker.Position);c.Index++;c.Path=null;ConstructionStatus="Digging bed "+c.Slot+" / accepted action "+c.Index;return true;}
   bool valid=CampSpatialPolicy.CanCommitResidentialConstruction(c.Cuts.Count,c.Index,CountBlocked(c.Room.Cells),Route(c.Worker.Position,Definition.Marker("fire").Interaction,3).Count>0);
   if(valid){Established=c.Slot;Reconcile();ConstructionStatus="Established bed "+c.Slot+"; full footprint and Fire return validated";}else ConstructionStatus="Construction completion refused: footprint or return path";
   Construction=null;return valid;
  }
  public bool ConstructNext(out string failure){if(!BeginConstruction(out failure))return false;int guard=20000;while(Construction!=null&&guard-->0)TickConstruction(.1);failure=ConstructionStatus;return guard>0&&ConstructionStatus.StartsWith("Established");}
  int CountBlocked(IReadOnlyList<DualTile> cells){int n=0;foreach(var c in cells)if(world.IsSolid(c.X,c.Y))n++;return n;}
 }
}
