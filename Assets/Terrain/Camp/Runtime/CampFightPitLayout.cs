using System;using System.Collections.Generic;using SporeGobbo.Geometry;
namespace SporeGobbo.CampCandidate
{
 /// <summary>Immutable authored Pit masks and anchors. No terrain, actors, match or save state.</summary>
 public static class CampFightPitLayout
 {
  public static readonly Point2 Center=new Point2(151.5,82.5),Entry=new Point2(135.5,95.5);
  static readonly HashSet<DualTile> floor=BuildFloor();
  public static bool ArenaCell(int x,int y){double dx=(x-151)/16.0,dy=(y-82)/12.0;return dx*dx+dy*dy<.87*.87;}
  public static bool InnerCell(int x,int y){double dx=(x-151)/16.0,dy=(y-82)/12.0;return dx*dx+dy*dy<1;}
  public static bool FloorCell(int x,int y)=>floor.Contains(new DualTile(x,y));
  public static IReadOnlyCollection<DualTile> FloorCells=>new List<DualTile>(floor).AsReadOnly();
  public static Point2 Spectator(double angle)=>new Point2(151.5+18.5*Math.Cos(angle),82.5+15*Math.Sin(angle));
  static HashSet<DualTile> BuildFloor()
  {
   var cells=new HashSet<DualTile>();Lobe(cells,151,82,23,20);Lobe(cells,129,95,4.5,4.3);Tube(cells,129,95,136,94,3.3);Tube(cells,136,94,140,96,3.3);return cells;
  }
  public static void Lobe(HashSet<DualTile> cells,double cx,double cy,double rx,double ry)
  {for(int y=(int)(cy-ry-2);y<=cy+ry+2;y++)for(int x=(int)(cx-rx-2);x<=cx+rx+2;x++){double dx=(x-cx)/rx,dy=(y-cy)/ry,t=Math.Atan2(dy,dx),edge=1+.045*Math.Sin(t*5)+.025*Math.Cos(t*3);if(dx*dx+dy*dy<edge*edge&&x>0&&y>0&&x<191&&y<191)cells.Add(new DualTile(x,y));}}
  public static void Tube(HashSet<DualTile> cells,int ax,int ay,int bx,int by,double radius)
  {int n=Math.Max(1,Math.Max(Math.Abs(ax-bx),Math.Abs(ay-by))*2);for(int i=0;i<=n;i++){double t=i/(double)n;Lobe(cells,ax+(bx-ax)*t,ay+(by-ay)*t,radius,radius);}}
 }
}
