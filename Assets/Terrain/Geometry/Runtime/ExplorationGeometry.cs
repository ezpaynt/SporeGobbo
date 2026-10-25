using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry
{
 public enum PreviewMatter{Dirt, Permanent,Root,Stone,Bone}
 public static class OrganicStructure
 {
  public static StructurePart Arch(string id,StructureSection a,StructureSection b,double rise,double skew,double thickness)
  {var s=new StructureSection[33];for(int i=0;i<33;i++){double t=i/32.0,u=1-t;var g=a.Ground*u+b.Ground*t;double r=a.Radius*u+b.Radius*t;double mound=4*t*u*(1+skew*(2*t-1));double thick=thickness*(.65+.35*Math.Sin(Math.PI*t));double h=a.Height*u+b.Height*t;double spread=r*(2*t-1);s[i]=new StructureSection(g,h+rise*mound,r,new Point2(g.X+spread,g.Y-h-rise*mound),new Point2(g.X-spread,g.Y-h-Math.Max(0,rise-thick)*mound));}return new StructurePart(id,s,true);}
  public static StructurePart Branch(string id,StructureSection start,Point2 end)
  {var s=new StructureSection[17];var center=(start.Left+start.Right)*.5;s[0]=start;for(int i=1;i<17;i++){double t=i/16.0;var c=center*(1-t)+end*t+new Point2(0,-4*Math.Sin(Math.PI*t));var axis=(start.Right-start.Left)*(.5*(1-t))+new Point2(start.Radius*.4,start.Radius*.7)*t;var offset=axis*((1-t)*(1-.25*Math.Sin(Math.PI*t)));s[i]=new StructureSection(c,0,start.Radius*(1-t),c-offset,c+offset);}return new StructurePart(id,s,true);}
 }
 public sealed class ExplorationFixture:IStreamingTerrainDefinition,IRefillableTerrainDefinition
 {
  public readonly int Size,X,Y;readonly HashSet<int> floor=new HashSet<int>(),locked=new HashSet<int>();public readonly List<int> InitialKnown=new List<int>();public readonly List<DiscoveryRegion> Regions=new List<DiscoveryRegion>();
  public readonly List<DimensionalLandmark> Landmarks=new List<DimensionalLandmark>();public readonly List<PreviewMatter> Materials=new List<PreviewMatter>();public readonly List<Point2> Tour=new List<Point2>();public readonly List<TerrainClue> Clues=new List<TerrainClue>();public readonly List<DualTile> Rubble=new List<DualTile>();
  public ExplorationFixture(int size=1024,double height=32,double width=7)
  {
   Size=size;X=Y=size/2;Ellipse(floor,0,10,20,15);foreach(int i in floor)InitialKnown.Add(i);
   // A coherent winding circuit: north routes, side loops and a lower return. Nothing outside the entry is pre-discovered.
   int[,] way={{0,10},{-15,-8},{-28,-28},{-12,-48},{18,-58},{42,-42},{65,-61},{52,-83},{22,-94},{-14,-83},{-45,-101},{-66,-78},{-56,-51},{-73,-28},{-60,-4},{-44,20},{-30,43},{0,57},{28,45},{58,24},{76,-1},{65,-30},{42,-42}};
   for(int i=0;i<way.GetLength(0);i++){var point=P(way[i,0],way[i,1]);Tour.Add(point);if(i>0)Corridor(floor,Tour[i-1],point,i%4==0?4:2);}
   Corridor(floor,P(58,24),P(0,10),3);Corridor(floor,P(-12,-48),P(-56,-51),2);Corridor(floor,P(18,-58),P(22,-94),2);Corridor(floor,P(-30,43),P(-44,20),3);
   Ellipse(floor,46,-8,28,20);Corridor(floor,P(42,-42),P(46,-8),4);Corridor(floor,P(58,24),P(46,-8),4);
   // Excavatable collar and protected irregular spike. Broad upper shoulder remains recognizable under compression.
   var foot=new List<DualTile>();for(int y=-5;y<=5;y++)for(int x=-7;x<=7;x++)if(x*x/50.0+y*y/26.0<1)foot.Add(new DualTile(X+18+x,Y-27+y));
   var spike=StructuralPrimitives.Spike("54/stone-spike",P(18,-27),width,height);Landmarks.Add(new DimensionalLandmark("54/spike",foot.ToArray(),new[]{new DualTile(X+18,Y-27)},new[]{spike}));Materials.Add(PreviewMatter.Stone);
   var chamber=new HashSet<int>();Ellipse(chamber,18,-27,20,18);Corridor(chamber,P(10,-8),P(12,-14),2);chamber.RemoveWhere(i=>Math.Pow((i%Size-X-18)/9.0,2)+Math.Pow((i/Size-Y+27)/7.0,2)<=1);Add("spike",chamber,DiscoveryPolicy.Gradual,.24);
   // Three irregular grounded roots, two unequal asymmetric spans, one exact attached branch.
   var roots=new List<DualTile>();foreach(var c in new[]{P(30,-6),P(43,-3),P(68,-9)})for(int y=-4;y<=4;y++)for(int x=-3;x<=3;x++)if(Math.Abs(x)<3||Math.Abs(y)<3)roots.Add(new DualTile((int)c.X+x,(int)c.Y+y));
   var a=new StructureSection(P(30,-6),0,3.5);var b=new StructureSection(P(43,-3),0,3.5);var c2=new StructureSection(P(68,-9),0,3.5);var first=OrganicStructure.Arch("54/root-a",a,b,10,.35,4);var second=OrganicStructure.Arch("54/root-b",b,c2,17,-.28,5);var branch=OrganicStructure.Branch("54/root-branch",second.Sections[12],P(76,-29));
   Landmarks.Add(new DimensionalLandmark("54/roots",roots.ToArray(),new[]{new DualTile(X+30,Y-6),new DualTile(X+43,Y-3),new DualTile(X+68,Y-9)},new[]{first,second,branch}));Materials.Add(PreviewMatter.Root);
   var bonefoot=new[]{new DualTile(X-49,Y-54),new DualTile(X-50,Y-54),new DualTile(X-48,Y-54)};Landmarks.Add(new DimensionalLandmark("54/bone",bonefoot,bonefoot,new[]{StructuralPrimitives.Spike("54/bone-body",P(-49,-54),2.5,8)}));Materials.Add(PreviewMatter.Bone);
   foreach(var l in Landmarks)foreach(var cell in l.Footprint)locked.Add(cell.Y*Size+cell.X);
   // Closed side pockets with explicit timing semantics. Real dirt seals remain part of ordinary excavation.
   Pocket("small",-31,4,4,3,DiscoveryPolicy.Immediate,0,0);Pocket("occupied",-45,-26,10,8,DiscoveryPolicy.Gradual,.08,12);Pocket("large",-77,12,17,12,DiscoveryPolicy.Gradual,.3,4);
   var network=new HashSet<int>();Corridor(network,P(89,-20),P(128,-54),2);Corridor(network,P(128,-54),P(105,-93),2);Corridor(network,P(128,-54),P(160,-47),2);Corridor(network,P(160,-47),P(149,-8),2);Ellipse(network,149,-8,9,6);Add("hidden-network",network,DiscoveryPolicy.Bounded,.24,4);
   Corridor(floor,P(76,-1),P(86,-17),2); // Dirt gap to hidden network is an intentional breakthrough.
   for(int y=28;y<=32;y++)for(int x=-46;x<=-29;x++)floor.Remove(Key(x,y));for(int x=-46;x<=-29;x++)Rubble.Add(new DualTile(X+x,Y+30));
   Clues.Add(new TerrainClue(P(-21,4),TerrainClueKind.Crack));Clues.Add(new TerrainClue(P(-37,-26),TerrainClueKind.Trace));Clues.Add(new TerrainClue(P(87,-18),TerrainClueKind.Disturbed));
   foreach(int i in locked)floor.Remove(i);InitialKnown.RemoveAll(i=>!floor.Contains(i));for(int k=0;k<Regions.Count;k++){var r=Regions[k];var cells=new List<int>();foreach(int i in r.Cells)if(!locked.Contains(i)&&floor.Contains(i))cells.Add(i);Regions[k]=new DiscoveryRegion(r.Id,cells,r.Policy,r.RevealSeconds,r.LocalImmediate);}
  }
  public Point2 P(int x,int y)=>new Point2(X+x+.5,Y+y+.5);int Key(int x,int y)=>(Y+y)*Size+X+x;
  void Ellipse(HashSet<int> s,int x,int y,int rx,int ry){for(int v=-ry;v<=ry;v++)for(int u=-rx;u<=rx;u++)if(u*u/(double)(rx*rx)+v*v/(double)(ry*ry)<=1)s.Add(Key(x+u,y+v));}
  void Corridor(HashSet<int> cells,Point2 a,Point2 b,int r){int n=(int)Math.Ceiling(Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y))*2);for(int k=0;k<=n;k++){double t=k/(double)Math.Max(1,n);int cx=(int)Math.Floor(a.X+(b.X-a.X)*t),cy=(int)Math.Floor(a.Y+(b.Y-a.Y)*t);for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<=r*r+1)cells.Add((cy+y)*Size+cx+x);}}
  void Pocket(string id,int x,int y,int rx,int ry,DiscoveryPolicy p,double seconds,int fast){var s=new HashSet<int>();Ellipse(s,x,y,rx,ry);Add(id,s,p,seconds,fast);}
  void Add(string id,HashSet<int> cells,DiscoveryPolicy policy,double seconds,int fast=0){cells.RemoveWhere(i=>floor.Contains(i)||locked.Contains(i));floor.UnionWith(cells);if(policy==DiscoveryPolicy.Bounded){var groups=new SortedDictionary<int,List<int>>();foreach(int i in cells){int k=(i/Size/16)*(Size/16)+i%Size/16;if(!groups.TryGetValue(k,out var l))groups[k]=l=new List<int>();l.Add(i);}foreach(var g in groups)Regions.Add(new DiscoveryRegion("54/"+id+"/"+g.Key,g.Value,policy,seconds,fast));}else Regions.Add(new DiscoveryRegion("54/"+id,cells,policy,seconds,fast));}
  public bool Protected(int x,int y)=>locked.Contains(y*Size+x);
  public bool Refillable(int x,int y){foreach(var c in Rubble)if(c.X==x&&c.Y==y)return true;return false;}
  public bool TrySolid(int x,int y,out bool solid){solid=!floor.Contains(y*Size+x);return x>=X-190&&x<=X+200&&y>=Y-170&&y<=Y+130;}
 }
 public sealed class ExplorationExperiment:IDisposable
 {
  public readonly ExplorationFixture Fixture;public readonly StreamingWorld World;public readonly DiscoveryIndex Index;public readonly CaveDiscovery Discovery;public readonly LandmarkPresentation Presentation;
  public ExplorationExperiment(int size=1024,double height=32,double width=7){Fixture=new ExplorationFixture(size,height,width);World=new StreamingWorld(size,size,47045,Fixture);Index=new DiscoveryIndex(World,Fixture.Regions);Discovery=new CaveDiscovery(World,Index){Duration=.3,MaxReleases=32,Grouping=2,ImmediateDistance=5};Discovery.SeedKnown(Fixture.InitialKnown);Presentation=new LandmarkPresentation(Discovery){Style=BreakthroughStyle.Soft};}
  public void Dispose()=>Discovery.Dispose();
 }
}
