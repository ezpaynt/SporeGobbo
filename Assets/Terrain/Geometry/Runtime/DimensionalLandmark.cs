using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry
{
 public interface IFrontierPresentation { bool SoftTile(int x,int y); }
 public enum BreakthroughStyle { Original,Hybrid,Soft }
 /// <summary>Immutable presentation skeleton. Ground is logical; Height is metadata, not a walkable level.</summary>
 public readonly struct StructureSection
 {
  public readonly Point2 Ground;public readonly double Height,Radius;readonly Point2? left,right;
  public StructureSection(Point2 ground,double height,double radius){if(height<0||radius<0||double.IsNaN(height)||double.IsNaN(radius))throw new ArgumentOutOfRangeException();Ground=ground;Height=height;Radius=radius;left=right=null;}
  public StructureSection(Point2 ground,double height,double radius,Point2 outer,Point2 inner):this(ground,height,radius){left=outer;right=inner;}
  public Point2 Left=>left??new Point2(Ground.X-Radius,Ground.Y-Height);
  public Point2 Right=>right??new Point2(Ground.X+Radius,Ground.Y-Height);
 }
 public sealed class StructurePart
 {
  public readonly string Id;public readonly StructureSection[] Sections;public readonly bool Overhead,ExposedTip;
  public StructurePart(string id,StructureSection[] sections,bool overhead=false,bool exposedTip=false){if(sections.Length<2)throw new ArgumentException();Id=id;Sections=sections;Overhead=overhead;ExposedTip=exposedTip;}
  public ProjectionBounds Bounds{get{double x0=double.MaxValue,y0=x0,x1=double.MinValue,y1=x1;foreach(var s in Sections){x0=Math.Min(x0,Math.Min(s.Left.X,s.Right.X));y0=Math.Min(y0,Math.Min(s.Left.Y,s.Right.Y));x1=Math.Max(x1,Math.Max(s.Left.X,s.Right.X));y1=Math.Max(y1,Math.Max(s.Left.Y,s.Right.Y));}return new ProjectionBounds(new Point2(x0,y0),new Point2(x1,y1));}}
 }
 public sealed class DimensionalLandmark
 {
  public readonly string Id;public readonly DualTile[] Footprint,Contacts;public readonly StructurePart[] Parts;
  public readonly ProjectionBounds Bounds;public readonly DualTile MinChunk,MaxChunk;
  public DimensionalLandmark(string id,DualTile[] footprint,DualTile[] contacts,StructurePart[] parts)
  {Id=id;Footprint=footprint;Contacts=contacts;Parts=parts;double x0=double.MaxValue,y0=x0,x1=double.MinValue,y1=x1;foreach(var p in parts){var b=p.Bounds;x0=Math.Min(x0,b.Min.X);y0=Math.Min(y0,b.Min.Y);x1=Math.Max(x1,b.Max.X);y1=Math.Max(y1,b.Max.Y);}foreach(var c in footprint){x0=Math.Min(x0,c.X);y0=Math.Min(y0,c.Y);x1=Math.Max(x1,c.X+1);y1=Math.Max(y1,c.Y+1);}Bounds=new ProjectionBounds(new Point2(x0,y0),new Point2(x1,y1));MinChunk=StreamingCoordinates.Owner((int)Math.Floor(x0),(int)Math.Floor(y0));MaxChunk=StreamingCoordinates.Owner((int)Math.Ceiling(x1),(int)Math.Ceiling(y1));}
  public bool Owns(int x,int y){foreach(var c in Footprint)if(c.X==x&&c.Y==y)return true;return false;}
 }
 public static class StructuralPrimitives
 {
  public static StructurePart Spike(string id,Point2 anchor,double width,double height)
  {var a=new StructureSection[25];for(int i=0;i<a.Length;i++){double t=i/24.0;double r=width*Math.Pow(1-t,.8);a[i]=new StructureSection(new Point2(anchor.X+Math.Sin(t*Math.PI)*.9,anchor.Y),height*t,r);}return new StructurePart(id,a,false,true);}
  // Both ends are exact shared ground sections. One strip, no independently capped rectangles.
  public static StructurePart Arch(string id,StructureSection a,StructureSection b,double rise)
  {var s=new StructureSection[33];for(int i=0;i<s.Length;i++){double t=i/32.0,u=1-t;var g=a.Ground*u+b.Ground*t;double r=a.Radius*u+b.Radius*t,h=a.Height*u+b.Height*t;double spread=r*(2*t-1);s[i]=new StructureSection(g,h+4*rise*t*u,r,new Point2(g.X+spread,g.Y-h-4*rise*t*u),new Point2(g.X-spread,g.Y-h-4*Math.Max(0,rise-2*r)*t*u));}return new StructurePart(id,s,true);}
  public static bool Intersects(ProjectionBounds a,ProjectionBounds b)=>a.Min.X<=b.Max.X&&a.Max.X>=b.Min.X&&a.Min.Y<=b.Max.Y&&a.Max.Y>=b.Min.Y;
  public static ProjectionBounds ProjectedBounds(StructurePart part,FocusProjection p){var b=part.Bounds;return new ProjectionBounds(p.Project(b.Min),p.Project(b.Max));}
 }
 /// <summary>Authored initial generation only. StreamingWorld remains the sole mutable physical authority.</summary>
 public sealed class LandmarkFixture:IStreamingTerrainDefinition
 {
  public readonly int Size,X,Y;public readonly List<DiscoveryRegion> Regions=new List<DiscoveryRegion>();public readonly List<int> InitialKnown=new List<int>();
  public readonly List<DimensionalLandmark> Landmarks=new List<DimensionalLandmark>();readonly HashSet<int> floor=new HashSet<int>();readonly HashSet<int> protectedCells=new HashSet<int>();
  public LandmarkFixture(int size=1024,double spikeHeight=24)
  {
   if(size<768)throw new ArgumentOutOfRangeException();Size=size;X=Y=size/2;
   Rect(floor,-90,20,105,28);Rect(floor,-85,-85,-77,85);Rect(floor,-85,-65,105,-57);Rect(floor,-85,65,105,73);
   // Roots are openly visible; spike chamber and long network remain physically open but concealed.
   Rect(floor,12,-22,70,28);foreach(var i in floor)InitialKnown.Add(i);
   var foot=new List<DualTile>();for(int y=-3;y<=3;y++)for(int x=-4;x<=4;x++)if(x*x/20.0+y*y/10.0<=1)foot.Add(new DualTile(X-45+x,Y-10+y));
   var anchor=P(-45,-10);var spike=StructuralPrimitives.Spike("spike/body",anchor,4,spikeHeight);
   Landmarks.Add(new DimensionalLandmark("stage53/spike",foot.ToArray(),new[]{new DualTile(X-45,Y-10)},new[]{spike}));
   var roots=new List<DualTile>();foreach(int cx in new[]{20,32,58})for(int y=-4;y<=4;y++)for(int x=-3;x<=3;x++)if(Math.Abs(x)<3||Math.Abs(y)<3)roots.Add(new DualTile(X+cx+x,Y+y));
   var c0=new StructureSection(P(20,0),0,3.5);var c1=new StructureSection(P(32,0),0,3.5);var c2=new StructureSection(P(58,0),0,3.5);
   Landmarks.Add(new DimensionalLandmark("stage53/roots",roots.ToArray(),new[]{new DualTile(X+20,Y),new DualTile(X+32,Y),new DualTile(X+58,Y)},new[]{StructuralPrimitives.Arch("roots/narrow",c0,c1,9),StructuralPrimitives.Arch("roots/wide",c1,c2,13)}));
   foreach(var l in Landmarks)foreach(var c in l.Footprint)protectedCells.Add(c.Y*Size+c.X);
   var room=new HashSet<int>();for(int y=-32;y<=10;y++)for(int x=-70;x<=-20;x++)if(Math.Pow((x+45)/25.0,2)+Math.Pow((y+10)/22.0,2)<=1)room.Add(Key(x,y));Rect(room,-46,10,-44,18);room.RemoveWhere(i=>Math.Pow((i%Size-X+45)/6.0,2)+Math.Pow((i/Size-Y+10)/5.0,2)<=1);Add("spike-pocket",room,DiscoveryPolicy.Gradual);
   var net=new HashSet<int>();Rect(net,83,-45,89,18);Rect(net,-5,-45,89,-39);Rect(net,-5,-45,1,-6);Rect(net,35,-45,41,-25);Rect(net,83,30,89,60);Rect(net,-5,54,89,60);Rect(net,-5,32,1,60);Add("network",net,DiscoveryPolicy.Bounded);
   // Real alternate shortcut seal, not a scripted door: x2..11,y22..26 between two known regions.
   for(int y=20;y<=28;y++)for(int x=3;x<=5;x++)floor.Remove(Key(x,y));
   InitialKnown.RemoveAll(i=>!floor.Contains(i)||Protected(i%Size,i/Size));
  }
  int Key(int x,int y)=>(Y+y)*Size+X+x;public Point2 P(int x,int y)=>new Point2(X+x+.5,Y+y+.5);
  void Rect(HashSet<int> cells,int x0,int y0,int x1,int y1){for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)cells.Add(Key(x,y));}
  void Add(string id,HashSet<int> cells,DiscoveryPolicy policy){cells.RemoveWhere(i=>Protected(i%Size,i/Size)||floor.Contains(i));floor.UnionWith(cells);if(policy==DiscoveryPolicy.Bounded){var chunks=new SortedDictionary<int,List<int>>();foreach(int i in cells){int k=(i/Size/16)*((Size+15)/16)+i%Size/16;if(!chunks.TryGetValue(k,out var list))chunks[k]=list=new List<int>();list.Add(i);}foreach(var kv in chunks)Regions.Add(new DiscoveryRegion("stage53/"+id+"/"+kv.Key,kv.Value,policy));}else Regions.Add(new DiscoveryRegion("stage53/"+id,cells,policy));}
  public bool Protected(int x,int y)=>protectedCells.Contains(y*Size+x);
  public bool TrySolid(int x,int y,out bool solid){solid=!floor.Contains(y*Size+x);return x>=X-100&&x<=X+115&&y>=Y-95&&y<=Y+95;}
 }
 public sealed class LandmarkPresentation:IOccupancySource,IFrontierPresentation
 {
  public readonly CaveDiscovery Discovery;public BreakthroughStyle Style;public int Width=>Discovery.World.Width;public int Height=>Discovery.World.Height;
  public LandmarkPresentation(CaveDiscovery d){Discovery=d;}
  public bool IsSolid(int x,int y)=>Discovery.World.IsSolid(x,y)||!Discovery.Shown(x,y);
  public bool SoftTile(int x,int y)
  {if(Style!=BreakthroughStyle.Soft)return false;bool hidden=false,shown=false;for(int j=0;j<2;j++)for(int i=0;i<2;i++){if(Discovery.World.IsSolid(x+i,y+j))return false;if(Discovery.Shown(x+i,y+j))shown=true;else hidden=true;}return hidden&&shown;}
 }
 public sealed class LandmarkExperiment:IDisposable
 {
  public readonly LandmarkFixture Fixture;public readonly StreamingWorld World;public readonly DiscoveryIndex Index;public readonly CaveDiscovery Discovery;public readonly LandmarkPresentation Presentation;
  public LandmarkExperiment(int size=1024,double height=24){Fixture=new LandmarkFixture(size,height);World=new StreamingWorld(size,size,47045,Fixture);Index=new DiscoveryIndex(World,Fixture.Regions);Discovery=new CaveDiscovery(World,Index);Discovery.SeedKnown(Fixture.InitialKnown);Presentation=new LandmarkPresentation(Discovery);}
  public void Dispose()=>Discovery.Dispose();
 }
}
