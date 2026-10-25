using System;
namespace SporeGobbo.Geometry.Preview
{
 // Preview attack gate: terrain/discovery only. No camera, frustum or renderer input.
 public static class EncounterReadability
 {
  public static bool Allowed(CaveDiscovery d,LabActor actor,Point2 player,double reach)
  {
   if(!Shown(d,actor.Position)||!Shown(d,player)||!LabNavigation.Contact(d.World,actor.Position,player))return false;
   // Entire potentially actionable telegraph, not just its endpoint or actor's center.
   if(actor.Attack==LabAttack.WideArea){for(int y=(int)Math.Floor(actor.Position.Y-reach);y<=Math.Ceiling(actor.Position.Y+reach);y++)for(int x=(int)Math.Floor(actor.Position.X-reach);x<=Math.Ceiling(actor.Position.X+reach);x++)if(LabNavigation.Distance(actor.Position,new Point2(x+.5,y+.5))<=reach&&!d.World.IsSolid(x,y)&&!d.Shown(x,y))return false;}
   else{double dx=player.X-actor.Position.X,dy=player.Y-actor.Position.Y,len=Math.Sqrt(dx*dx+dy*dy);if(actor.State==LabState.Telegraphing){dx=actor.AttackDirection.X;dy=actor.AttackDirection.Y;len=Math.Sqrt(dx*dx+dy*dy);}if(len<1e-8)return true;for(double t=0;t<=reach;t+=.2){var p=new Point2(actor.Position.X+dx/len*t,actor.Position.Y+dy/len*t);if(d.World.IsSolid((int)p.X,(int)p.Y))break;for(int side=-1;side<=1;side++){var q=p+new Point2(-dy/len,dx/len)*(.15*side);if(!d.World.IsSolid((int)q.X,(int)q.Y)&&!Shown(d,q))return false;}}}
   return true;
  }
  static bool Shown(CaveDiscovery d,Point2 p)=>d.Known((int)Math.Floor(p.X),(int)Math.Floor(p.Y))&&d.Shown((int)Math.Floor(p.X),(int)Math.Floor(p.Y));
 }
}
