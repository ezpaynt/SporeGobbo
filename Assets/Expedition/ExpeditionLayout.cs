using System;using System.Collections.Generic;using System.Linq;using SporeGobbo.Geometry;
/// <summary>Complete immutable authored topology. No MapData mirror, generator profiles or campaign facts.</summary>
public sealed class ExpeditionLayout:ICompleteTerrainDefinition
{
 public const int Width=144,Height=128,Seed=62001;public const string Identity="expedition.test.62b.v1";
 readonly HashSet<int> floor=new HashSet<int>(),permanent=new HashSet<int>();
 public readonly Dictionary<string,Point2> Anchors=new Dictionary<string,Point2>{{"arrival",new Point2(20.5,100.5)},{"fork",new Point2(48.5,84.5)},{"weevil",new Point2(68.5,92.5)},{"nursery",new Point2(30.5,62.5)},{"blob",new Point2(113.5,64.5)},{"fossil",new Point2(104.5,98.5)},{"quiet",new Point2(60.5,30.5)},{"exit",new Point2(122.5,24.5)}};
 public readonly List<DiscoveryRegion> Concealed=new List<DiscoveryRegion>();public readonly List<int> InitialKnown=new List<int>();
 public readonly Dictionary<string,List<DualTile>> Footprints=new Dictionary<string,List<DualTile>>();
 public ExpeditionLayout()
 {
  Ellipse(floor,20,100,10,8);Ellipse(floor,48,84,10,8);Ellipse(floor,68,92,13,10);Ellipse(floor,30,62,9,8);Ellipse(floor,108,58,16,12);Ellipse(floor,104,98,14,10);Ellipse(floor,60,30,12,9);Ellipse(floor,122,24,8,7);
  Tube(floor,4,new[]{P(20,100),P(32,96),P(39,86),P(48,84)});Tube(floor,3.5,new[]{P(48,84),P(52,70),P(64,67),P(76,68),P(90,63),P(108,58)});
  Tube(floor,4,new[]{P(48,84),P(54,93),P(68,92),P(84,96),P(104,98),P(120,82),P(118,69),P(108,58)});
  Tube(floor,3,new[]{P(48,84),P(35,78),P(30,62)});Tube(floor,3.5,new[]{P(108,58),P(115,44),P(119,34),P(122,24)});Tube(floor,3,new[]{P(64,67),P(55,54),P(54,42),P(60,30)});
  Tube(floor,2.5,new[]{P(84,96),P(84,102),P(92,103),P(80,103)});Ellipse(floor,81,111,2,2);Ellipse(floor,86,111,2,2);Ellipse(floor,91,111,2,2);
  // Hidden floor is authored now, separated by real dirt seals. Only its presentation reveals later.
  Hidden("tiny",DiscoveryPolicy.Gradual,.85,g=>Ellipse(g,18,71,3,2));
  Hidden("snake",DiscoveryPolicy.Gradual,1.1,g=>Tube(g,1.8,new[]{P(88,88),P(89,78),P(92,73),P(98,75),P(99,83)}));
  floor.RemoveWhere(i=>i/Width>=106&&i%Width>=96&&i%Width<=121);
  Hidden("combat",DiscoveryPolicy.Immediate,0,g=>Ellipse(g,108,114,10,7));
  // Protected masks correspond to grounded bases, leaving actual floor under the root span.
  Stamp("stone.shoulder",43,78,3,2);Stamp("stone.pillar",108,58,2,3);Stamp("stone.shelf",96,96,4,2);
  Stamp("root.left",72,65,2,2);Stamp("root.right",83,63,2,2);Stamp("root.tail",70,83,3,2);
  Stamp("bone.left",100,103,2,2);Stamp("bone.right",113,103,2,2);
  Tube(floor,3.5,new[]{P(68,69),P(78,69),P(88,66)});
  for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(x<6||y<6||x>=Width-6||y>=Height-6)permanent.Add(y*Width+x);
  floor.ExceptWith(permanent);var hidden=new HashSet<int>(Concealed.SelectMany(r=>r.Cells));foreach(int i in floor)if(!hidden.Contains(i))InitialKnown.Add(i);InitialKnown.Sort();
 }
 static Point2 P(int x,int y)=>new Point2(x+.5,y+.5);
 void Hidden(string id,DiscoveryPolicy policy,double duration,Action<HashSet<int>> author){var g=new HashSet<int>();author(g);g.ExceptWith(floor);floor.UnionWith(g);Concealed.Add(new DiscoveryRegion(id,g,policy,duration));}
 void Stamp(string id,int x,int y,int rx,int ry){var cells=new HashSet<int>();Ellipse(cells,x,y,rx,ry);permanent.UnionWith(cells);Footprints[id]=cells.Select(i=>new DualTile(i%Width,i/Width)).OrderBy(c=>c.Y*Width+c.X).ToList();}
 static void Ellipse(HashSet<int> set,int x,int y,int rx,int ry){for(int yy=y-ry;yy<=y+ry;yy++)for(int xx=x-rx;xx<=x+rx;xx++)if(xx>=6&&yy>=6&&xx<Width-6&&yy<Height-6&&(xx-x)*(xx-x)/(double)(rx*rx)+(yy-y)*(yy-y)/(double)(ry*ry)<=1)set.Add(yy*Width+xx);}
 static void Tube(HashSet<int> set,double radius,Point2[] p){for(int n=1;n<p.Length;n++){var a=p[n-1];var b=p[n];double dx=b.X-a.X,dy=b.Y-a.Y,l=dx*dx+dy*dy;for(int y=(int)Math.Floor(Math.Min(a.Y,b.Y)-radius);y<=Math.Ceiling(Math.Max(a.Y,b.Y)+radius);y++)for(int x=(int)Math.Floor(Math.Min(a.X,b.X)-radius);x<=Math.Ceiling(Math.Max(a.X,b.X)+radius);x++){double t=Math.Max(0,Math.Min(1,((x+.5-a.X)*dx+(y+.5-a.Y)*dy)/l)),u=x+.5-a.X-t*dx,v=y+.5-a.Y-t*dy;if(u*u+v*v<=radius*radius&&x>=6&&y>=6&&x<Width-6&&y<Height-6)set.Add(y*Width+x);}}}
 public bool Protected(int x,int y)=>x<0||y<0||x>=Width||y>=Height||permanent.Contains(y*Width+x);
 public bool TrySolid(int x,int y,out bool solid){solid=!floor.Contains(y*Width+x);return true;}
 public int Resistance(int x,int y)=>y>=106&&y<=107&&x>=80&&x<=93?(x<84?1:x<89?2:3):1;
 public string Fingerprint(){ulong h=14695981039346656037;for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){TrySolid(x,y,out bool s);h=(h^(ulong)(Protected(x,y)?2:s?1:0))*1099511628211;}return h.ToString("X16");}
}
