using System;
using System.Collections.Generic;
using System.IO;
namespace SporeGobbo.Geometry
{
 // Authored fixture identity, never resident chunk data. Bounds are deliberately small and explicit.
 public sealed class EnvironmentRegion:IStreamingTerrainDefinition
 {
  public const string Identity="stage51.fossil-arch.v1";
  public readonly int X,Y;public readonly Point2 Entry,Chamber,Seal,CoverEntry,Remote;
  public EnvironmentRegion(int width,int height){X=width/2;Y=height/2;Entry=P(-30,0);Chamber=P(41,-18);Seal=P(32,-18);CoverEntry=P(-33,23);Remote=new Point2(X+256.5,Y+192.5);}
  public Point2 P(int x,int y)=>new Point2(X+x+.5,Y+y+.5);
  public bool Protected(int x,int y)
  {
   double u=x-X+6,v=y-Y+2;
   // Curved spine with six irregular ribs; crosses the X chunk boundary.
   bool spine=u>=-14&&u<=18&&Math.Abs(v-Math.Sin(u*.16)*2)<1.4;
   bool ribs=u>=-13&&u<=17&&((x-X+64)%6<=1)&&Math.Abs(v)<7-Math.Abs(u)*.12;
   return spine||ribs;
  }
  public bool TrySolid(int x,int y,out bool solid)
  {
   int u=x-X,v=y-Y;solid=true;
   if(u>=-48&&u<=58&&v>=-38&&v<=38)
   {
    bool hall=u>=-42&&u<=25&&v>=-18&&v<=14;
    bool shaft=u>=-42&&u<=-34&&v>=-18&&v<=27;
    bool cover=u>=-31&&u<=5&&v>=20&&v<=26;
    bool entrance=u>=-33&&u<=-32&&v>=22&&v<=24; // x=-32 remains a diggable seal
    bool corridor=u>=18&&u<=31&&v>=-19&&v<=-17;
    bool room=u>=34&&u<=49&&v>=-25&&v<=-12;
    bool throat=u==33&&v>=-19&&v<=-17;
    bool embeddedDirt=u>=2&&u<=11&&v>=-9&&v<=8;
    solid=embeddedDirt||!(hall||shaft||cover||entrance&&u==-33||corridor||room||throat);
    return true;
   }
   if(Math.Abs(x-(int)Remote.X)<=19&&Math.Abs(y-(int)Remote.Y)<=15){solid=false;return true;}
   return false;
  }
  public bool UnderCover(Point2 p)=>p.X>=X-30&&p.X<=X+5&&p.Y>=Y+19&&p.Y<=Y+27;
  public bool InRoom(Point2 p)=>p.X>=X+33&&p.X<=X+51&&p.Y>=Y-27&&p.Y<=Y-10;
 }
 [Flags] public enum TraversalAccess {None=0,Player=1,Buddy=2,Enemy=4}
 public sealed class EnvironmentLink
 {
  public string Id="stage51.paired-door.v1";public Point2 Source,Destination,Facing=new Point2(1,0);
  public bool Enabled=true,Bidirectional=true,Unlocked=true;public TraversalAccess Access=TraversalAccess.Player;
  public bool Resolve(Point2 at,TraversalAccess who,out Point2 arrival,out Point2 facing)
  {
   arrival=Destination;facing=Facing;
   if(!Enabled||!Unlocked||(Access&who)==0)return false;
   if(Distance(at,Source)<=2.5)return true;
   if(Bidirectional&&Distance(at,Destination)<=2.5){arrival=Source;facing=new Point2(-Facing.X,-Facing.Y);return true;}
   return false;
  }
  static double Distance(Point2 a,Point2 b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
 }
 public enum TransferPhase {Idle,Preparing,Committed,Cancelled}
 public sealed class EnvironmentTransfer
 {
  public TransferPhase Phase {get;private set;}public Point2 Arrival,Facing;public int Transfers {get;private set;}public int Ticket {get;private set;}
  public bool Begin(EnvironmentLink link,Point2 at,TraversalAccess access,Func<Point2,bool> clear)
  {if(Phase==TransferPhase.Preparing||!link.Resolve(at,access,out var arrival,out var facing)||!clear(arrival))return false;Arrival=arrival;Facing=facing;Ticket++;Phase=TransferPhase.Preparing;return true;}
  public bool Commit(int ticket,bool chunksReady,Func<Point2,bool> clear,Action<Point2,Point2> transfer)
  {if(ticket!=Ticket||Phase!=TransferPhase.Preparing||!chunksReady)return false;if(!clear(Arrival)){Cancel();return false;}Phase=TransferPhase.Committed;Transfers++;transfer(Arrival,Facing);return true;}
  public void Cancel(){if(Phase==TransferPhase.Preparing){Ticket++;Phase=TransferPhase.Cancelled;}}
 }
 public sealed class EnvironmentState
 {
  public readonly EnvironmentRegion Region;public StreamingWorld World {get;private set;}public readonly EnvironmentLink Link;
  public bool Discovered,CoverOpened;public int DiscoveryVisits;public double Reveal=1;public bool Gradual=true;
  readonly Queue<int> queue=new Queue<int>();readonly HashSet<int> seen=new HashSet<int>();
  public EnvironmentState(int size=1024,int seed=47045)
  {if(size<768)throw new ArgumentOutOfRangeException(nameof(size));Region=new EnvironmentRegion(size,size);World=new StreamingWorld(size,size,seed,Region);Link=new EnvironmentLink{Source=Region.P(-35,3),Destination=Region.Remote};}
  // Local, bounded flood only on excavation notifications; no dependency on mesh residency or projection.
  public bool OnExcavation()
  {
   CoverOpened=!World.IsSolid(Region.X-32,Region.Y+23);
   if(Discovered)return false;
   queue.Clear();seen.Clear();int x0=Region.X-48,y0=Region.Y-38,w=107,h=77;
   int sx=(int)Region.Entry.X-x0,sy=(int)Region.Entry.Y-y0;queue.Enqueue(sy*w+sx);seen.Add(sy*w+sx);DiscoveryVisits=0;
   while(queue.Count>0)
   {
    int i=queue.Dequeue(),x=i%w,y=i/w;DiscoveryVisits++;
    if(x+x0==(int)Region.Chamber.X&&y+y0==(int)Region.Chamber.Y){Discovered=true;Reveal=Gradual?0:1;return true;}
    Visit(x-1,y,x0,y0,w,h);Visit(x+1,y,x0,y0,w,h);Visit(x,y-1,x0,y0,w,h);Visit(x,y+1,x0,y0,w,h);
   }
   return false;
  }
  void Visit(int x,int y,int x0,int y0,int w,int h){if(x<0||y<0||x>=w||y>=h)return;int i=y*w+x;if(!seen.Contains(i)&&!World.IsSolid(x+x0,y+y0)){seen.Add(i);queue.Enqueue(i);}}
  public void Step(double dt){if(Discovered)Reveal=Math.Min(1,Reveal+Math.Max(0,dt)/.65);}
  public bool CurrentlyVisible(Point2 player)
  {
   if(!Discovered)return false;
   double dx=Region.Chamber.X-player.X,dy=Region.Chamber.Y-player.Y;int n=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)*4));
   for(int i=0;i<=n;i++)if(World.IsSolid((int)Math.Floor(player.X+dx*i/n),(int)Math.Floor(player.Y+dy*i/n)))return false;
   return true;
  }
  public static float CoverOpacity(Point2 cell,Point2 player,bool cutaway,float blend)
  {double dx=cell.X-player.X,dy=cell.Y-player.Y;return cutaway&&dx*dx+dy*dy<36?1-.85f*blend:1;}
  public void Save(Stream stream)
  {
   using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
   {w.Write(0x53473531);w.Write(1);w.Write(EnvironmentRegion.Identity);w.Write(World.Width);w.Write(World.Seed);World.WriteDeltas(w);w.Write(Discovered);w.Write(CoverOpened);w.Write(Link.Enabled);w.Write(Link.Unlocked);w.Write(Link.Bidirectional);w.Write((int)Link.Access);w.Write(Gradual);}
  }
  public static EnvironmentState Load(Stream stream)
  {
   using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
   {if(r.ReadInt32()!=0x53473531||r.ReadInt32()!=1||r.ReadString()!=EnvironmentRegion.Identity)throw new InvalidDataException("Unknown Stage51 fixture");var s=new EnvironmentState(r.ReadInt32(),r.ReadInt32());s.World.ReadDeltas(r);bool discovered=r.ReadBoolean(),covered=r.ReadBoolean();s.Link.Enabled=r.ReadBoolean();s.Link.Unlocked=r.ReadBoolean();s.Link.Bidirectional=r.ReadBoolean();int access=r.ReadInt32();if((access&~7)!=0)throw new InvalidDataException("Invalid access flags");s.Link.Access=(TraversalAccess)access;s.Gradual=r.ReadBoolean();s.OnExcavation();if(s.Discovered!=discovered||s.CoverOpened!=covered)throw new InvalidDataException("Discovery does not match fixture connectivity");s.Reveal=1;return s;}
  }
 }
}
