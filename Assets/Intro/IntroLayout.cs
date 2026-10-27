using System;using System.Linq;using System.Collections.Generic;using SporeGobbo.Geometry;
/// <summary>One authored damaged passage. Physical floor exists before discovery; collapse is transient.</summary>
public sealed class IntroLayout:ICompleteTerrainDefinition,IBlockingTerrainDefinition
{
 public const int Width=160,Height=80,Seed=62003;public const string Identity="intro.authored.62c.v2";
 public readonly Point2 Birth=new Point2(14.5,54.5),Exit=new Point2(147.5,25.5),DigApproach=new Point2(95.5,47.5);
 public readonly Point2[] Route={new Point2(14.5,54.5),new Point2(28.5,57.5),new Point2(41.5,45.5),new Point2(53.5,34.5),new Point2(67.5,37.5),new Point2(78.5,49.5),new Point2(86.5,54.5),new Point2(94.5,47.5),new Point2(111.5,47.5),new Point2(117.5,31.5),new Point2(134.5,23.5),new Point2(147.5,25.5)};
 readonly HashSet<int> floor=new HashSet<int>(),protectedCells=new HashSet<int>(),placement=new HashSet<int>();
 public readonly List<int> Known=new List<int>();public readonly List<DiscoveryRegion> Concealed=new List<DiscoveryRegion>();
 public readonly List<DualTile> Plug=new List<DualTile>();public readonly List<List<DualTile>> CollapseMasks=new List<List<DualTile>>();
 public readonly Point2[] CollapseCenters={new Point2(21.5,55.5),new Point2(42.5,44.5),new Point2(64.5,36.5),new Point2(80.5,50.5),new Point2(91.5,50.5)};
 public readonly float[] Thresholds={35,57,78,94,113};
 public IntroLayout()
 {
  for(int i=1;i<Route.Length;i++)Tube(floor,Route[i-1],Route[i],i==6?2.6:i>=9?4.4:3.3+(i%3)*.45);
  Ellipse(floor,14,54,6,5);Ellipse(floor,44,43,6,5);Ellipse(floor,93,47,6,5);Ellipse(floor,145,25,7,5);
  // A continuous natural stone fault bounds the one dirt breach; no digging around a tutorial gate.
  for(int y=0;y<Height;y++)for(int x=97;x<=102;x++)if(y<43||y>51)protectedCells.Add(y*Width+x);
  for(int y=43;y<=51;y++)for(int x=99;x<=100;x++){floor.Remove(y*Width+x);Plug.Add(new DualTile(x,y));}
  Ellipse(protectedCells,48,34,2,2);Ellipse(protectedCells,82,48,2,1);Ellipse(protectedCells,131,19,3,2);Ellipse(protectedCells,147,20,1,2);Ellipse(protectedCells,147,30,1,2);
  var tiny=new HashSet<int>();Ellipse(tiny,61,28,3,2);tiny.ExceptWith(floor);floor.UnionWith(tiny);Concealed.Add(new DiscoveryRegion("intro.tiny",tiny,DiscoveryPolicy.Immediate));
  var snake=new HashSet<int>();Tube(snake,new Point2(74.5,55.5),new Point2(69.5,60.5),1.5);Tube(snake,new Point2(69.5,60.5),new Point2(63.5,58.5),1.5);snake.ExceptWith(floor);floor.UnionWith(snake);Concealed.Add(new DiscoveryRegion("intro.snake",snake,DiscoveryPolicy.Gradual,1.1));
  for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(x<6||y<6||x>=Width-6||y>=Height-6)protectedCells.Add(y*Width+x);
  floor.ExceptWith(protectedCells);var hidden=new HashSet<int>(Concealed.SelectMany(g=>g.Cells));foreach(int i in floor)if(!hidden.Contains(i))Known.Add(i);Known.Sort();
  foreach(var center in CollapseCenters){var cells=new List<DualTile>();for(int y=(int)center.Y-4;y<=(int)center.Y+4;y++)for(int x=(int)center.X-3;x<=(int)center.X+3;x++){double dx=x+.5-center.X,dy=y+.5-center.Y;int i=y*Width+x;if(dx*dx/10+dy*dy/18<1&&floor.Contains(i)&&!protectedCells.Contains(i)){cells.Add(new DualTile(x,y));placement.Add(i);}}CollapseMasks.Add(cells);}
 }
 static void Ellipse(HashSet<int> cells,int x,int y,int rx,int ry){for(int v=-ry;v<=ry;v++)for(int u=-rx;u<=rx;u++)if(u*u/(double)(rx*rx)+v*v/(double)(ry*ry)<=1)cells.Add((y+v)*Width+x+u);}
 static void Tube(HashSet<int> cells,Point2 a,Point2 b,double radius){double dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy;for(int y=(int)Math.Floor(Math.Min(a.Y,b.Y)-radius);y<=Math.Ceiling(Math.Max(a.Y,b.Y)+radius);y++)for(int x=(int)Math.Floor(Math.Min(a.X,b.X)-radius);x<=Math.Ceiling(Math.Max(a.X,b.X)+radius);x++){double t=Math.Max(0,Math.Min(1,((x+.5-a.X)*dx+(y+.5-a.Y)*dy)/l)),u=x+.5-a.X-t*dx,v=y+.5-a.Y-t*dy;if(u*u+v*v<=radius*radius)cells.Add(y*Width+x);}}
 public bool Protected(int x,int y)=>x<0||y<0||x>=Width||y>=Height||protectedCells.Contains(y*Width+x);
 public bool PlacementAllowed(int x,int y)=>placement.Contains(y*Width+x);
 public bool TrySolid(int x,int y,out bool solid){solid=!floor.Contains(y*Width+x);return true;}
 public bool IsPlug(int x,int y)=>x>=99&&x<=100&&y>=43&&y<=51;
 public string Fingerprint(){ulong h=14695981039346656037;for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){TrySolid(x,y,out bool s);h=(h^(ulong)(Protected(x,y)?2:s?1:0))*1099511628211;}return h.ToString("X16");}
}
