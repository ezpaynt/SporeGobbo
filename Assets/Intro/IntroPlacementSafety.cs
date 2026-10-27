using System;using System.Collections.Generic;using SporeGobbo.Geometry;
/// <summary>Pure bounded forward-clearance check. The sole tutorial plug remains legally excavatable.</summary>
public static class IntroPlacementSafety
{
 public static bool HasForwardRoute(StreamingWorld world,IntroLayout layout,Point2 actor,IReadOnlyList<DualTile> proposed,int size=3,bool allowPlug=true)
 {
  var fill=new HashSet<int>();foreach(var c in proposed)fill.Add(c.Y*world.Width+c.X);
  bool Fits(int x,int y){int h=size/2;for(int yy=y-h;yy<=y+h;yy++)for(int xx=x-h;xx<=x+h;xx++)if(!world.Contains(xx,yy)||fill.Contains(yy*world.Width+xx)||(world.IsSolid(xx,yy)&&!(allowPlug&&layout.IsPlug(xx,yy))))return false;return true;}
  int sx=(int)Math.Floor(actor.X),sy=(int)Math.Floor(actor.Y),goal=(int)layout.Exit.Y*world.Width+(int)layout.Exit.X;if(!Fits(sx,sy))return false;
  var seen=new HashSet<int>();var q=new Queue<int>();int start=sy*world.Width+sx;seen.Add(start);q.Enqueue(start);
  while(q.Count>0){int i=q.Dequeue();if(i==goal)return true;int x=i%world.Width,y=i/world.Width;for(int d=0;d<4;d++){int xx=x+(d==0?-1:d==1?1:0),yy=y+(d==2?-1:d==3?1:0);if(!world.Contains(xx,yy))continue;int n=yy*world.Width+xx;if(!seen.Contains(n)&&Fits(xx,yy)){seen.Add(n);q.Enqueue(n);}}}return false;
 }
}
