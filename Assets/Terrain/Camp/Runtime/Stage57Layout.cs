using System;
using System.Collections.Generic;
using SporeGobbo.Geometry;
namespace SporeGobbo.CampCandidate
{
 public sealed class CampReservation
 {
  public readonly string Id,Intent;public readonly int Capacity;public readonly Point2 Center,Entrance;public readonly double RadiusX,RadiusY;
  public CampReservation(string id,string intent,int capacity,Point2 center,double rx,double ry,Point2 entrance){Id=id;Intent=intent;Capacity=capacity;Center=center;RadiusX=rx;RadiusY=ry;Entrance=entrance;}
  public bool Contains(int x,int y)=>Math.Pow((x-Center.X)/RadiusX,2)+Math.Pow((y-Center.Y)/RadiusY,2)<1;
 }
 public static class Stage57Layout
 {
  public static readonly CampReservation[] Reservations={
   new CampReservation("west.ring","Later branching loop neighborhood",16,new Point2(39,84),20,20,new Point2(58.5,94.5)),
   new CampReservation("lower.ring","Later social sleeping ring",14,new Point2(42,137),18,18,new Point2(60.5,132.5)),
   new CampReservation("pit.quarter","Cramped later Pit neighborhood",10,new Point2(165,127),15,15,new Point2(150.5,132.5)),
   new CampReservation("pet.pocket","Future pet sleeping pocket only",0,new Point2(70,108),4,3,new Point2(74.5,110.5)),
   new CampReservation("hunter.room","Revisable trophy-wall room; no unlock assigned",0,new Point2(42,56),10,8,new Point2(52.5,61.5))};
  public static AuthoredCamp Create(int shopOption=0)
  {
   var a=new CampCell[192*192];for(int i=0;i<a.Length;i++)a[i]=CampCell.Permanent;
   Point2 P(double x,double y)=>new Point2(x,y);
   void L(double x,double y,double rx,double ry,CampCell k){for(int v=Math.Max(1,(int)(y-ry-2));v<Math.Min(191,y+ry+2);v++)for(int u=Math.Max(1,(int)(x-rx-2));u<Math.Min(191,x+rx+2);u++){double dx=(u-x)/rx,dy=(v-y)/ry,t=Math.Atan2(dy,dx),edge=1+.06*Math.Sin(t*5)+.03*Math.Cos(t*3);if(dx*dx+dy*dy<edge*edge)a[v*192+u]=k;}}
   void T(CampCell k,double radius,params Point2[] ps){for(int i=1;i<ps.Length;i++){var p=ps[i-1];var q=ps[i];double n=Math.Ceiling(Math.Sqrt(Math.Pow(q.X-p.X,2)+Math.Pow(q.Y-p.Y,2))*2);for(int j=0;j<=n;j++){double t=j/Math.Max(1,n);L(p.X+(q.X-p.X)*t,p.Y+(q.Y-p.Y)*t,radius,radius,k);}}}
   // Ordinary earth envelopes are physical dirt, never forty generated sleeping rooms.
   foreach(var e in Reservations)L(e.Center.X,e.Center.Y,e.RadiusX,e.RadiusY,CampCell.Expansion);
   L(98,79,48,29,CampCell.Expansion);L(105,128,45,21,CampCell.Expansion);
   // Social chamber and lumpy public return loop.
   L(83,111,20,14,CampCell.Open);L(70,110,13,10,CampCell.Open);L(94,118,14,9,CampCell.Open);
   T(CampCell.Open,4,P(72,115),P(61,123),P(64,133),P(82,139),P(106,133),P(113,117),P(103,110));
   L(58,108,9,7,CampCell.Open);T(CampCell.Open,3.8,P(66,110),P(58,108),P(58,94));
   T(CampCell.Open,3.8,P(64,130),P(60,132));
   T(CampCell.Open,4,P(70,135),P(63,147),P(62,155));L(62,155,8,6,CampCell.Open);
   // Upper landmarks: two convenient niches, one ordinary-dirt shortcut zone.
   T(CampCell.Open,4,P(84,101),P(84,88),P(90,77),P(88,65));L(88,63,8,7,CampCell.Open);
   T(CampCell.Open,3.8,P(84,87),P(75,80),P(68,73));L(67,70,8,7,CampCell.Open);
   T(CampCell.Open,3.8,P(58,94),P(55,79),P(53,63));L(43,57,10,8,CampCell.Open);
   // Squad is a living room, with two public approaches into the dorm spine.
   T(CampCell.Open,4,P(97,109),P(97,105),P(102,101));L(98,101,10,7,CampCell.Open);
   T(CampCell.Open,3.6,P(102,101),P(111,99),P(114,89),P(115,76));
   T(CampCell.Open,3.6,P(88,86),P(88,77),P(105,75),P(115,76));
   T(CampCell.Open,3.6,P(105,96),P(112,96));
   // Shop A sits on the lower secondary route; B competes with the west social lobe.
   var shop=shopOption==0?P(75,132):P(55,119);L(shop.X,shop.Y+3,9,6,CampCell.Open);
   T(CampCell.Open,3.8,shopOption==0?P(82,139):P(61,123),P(shop.X+2,shop.Y+5));
   T(CampCell.Open,4,P(90,139),P(108,145),P(119,143));
   T(CampCell.FightPit,4,P(119,143),P(131,145),P(140,145));L(142,145,17,12,CampCell.FightPit);
   T(CampCell.FightPit,3.5,P(144,140),P(150,132));
   // Four sleeping pockets remain separated by undug shoulders around a short shared spine.
   var rooms=new List<CampBedroom>();int slot=1;
   void Room(string id,int x,int y,int ex,int ey,int capacity){var before=(CampCell[])a.Clone();L(x,y,8,6,CampCell.Residential);T(CampCell.Residential,3,P(x,y),P(ex,ey));var owned=new List<DualTile>();for(int v=0;v<192;v++)for(int u=0;u<192;u++)if(a[v*192+u]==CampCell.Residential&&before[v*192+u]!=CampCell.Residential)owned.Add(new DualTile(u,v));var beds=new List<Point2>();for(int j=0;j<capacity;j++)beds.Add(P(x-(capacity-1)*2+j*4+.5,y+.5));rooms.Add(new CampBedroom(id,P(ex+.5,ey+.5),owned,beds,slot));slot+=capacity;}
   Room("dorm.fern",100,86,105,96,3);Room("dorm.root",128,87,116,90,3);Room("dorm.lichen",101,64,110,75,2);Room("dorm.end",128,65,117,75,2);
   foreach(var r in rooms)L(r.Entrance.X-.5,r.Entrance.Y-.5,3,3,CampCell.Open);
   var markers=new List<CampMarker>{new CampMarker("fire","Fire",P(84.5,111.5),P(84.5,117.5),CampCell.Open,"terrain.root"),new CampMarker("arrival.first","Intro approach",P(62.5,155.5),P(62.5,155.5)),new CampMarker("run.departure","Arrival / Exit",P(88.5,60.5),P(88.5,66.5)),new CampMarker("bones","Old Bones",P(67.5,66.5),P(68.5,72.5),CampCell.FirstDeath,"terrain.bone"),new CampMarker("squad","Squad living room",P(98.5,98.5),P(98.5,104.5),CampCell.FirstBuddy,"terrain.root"),new CampMarker("storage","Storage",P(56.5,106.5),P(60.5,111.5)),new CampMarker("shop.reserved","Chek's place / absent",P(shop.X+.5,shop.Y+.5),P(shop.X+2.5,shop.Y+5.5),CampCell.Shop),new CampMarker("fight.reserved","Future Fight Pit",P(144.5,139.5),P(140.5,147.5),CampCell.FightPit)};
   foreach(var m in markers)if(m.Id!="arrival.first"){for(int y=(int)m.Anchor.Y-3;y<=(int)m.Anchor.Y;y++)for(int x=(int)m.Anchor.X-3;x<=(int)m.Anchor.X+3;x++)a[y*192+x]=CampCell.Permanent;L(m.Anchor.X-.5,m.Anchor.Y-.5,1.6,1.6,CampCell.Permanent);}
   L(107,100,2,2,CampCell.Permanent);L(91,97,2,2,CampCell.Permanent);
   // Substantial angled Hunter backing, independent of any milestone.
   T(CampCell.Permanent,1.8,P(36,50),P(43,48),P(49,51));
   return new AuthoredCamp(a,markers,rooms,"camp.candidate.5.7.v1");
  }
 }
 /// Test-only pacing. Every edit and movement still passes through CampSession's ordinary constructor.
 public sealed class CampConstructionPreview
 {
  public readonly CampSession Session;public int Speed,BuildTarget=10;public bool FinishCurrent,BuildDorm;public string Status="";public int LastTicks,LastBatches;double accumulator;
  public CampConstructionPreview(CampSession session){Session=session;}
  public void Tick(double dt){LastTicks=LastBatches=0;if(dt<=0)return;long start=System.Diagnostics.Stopwatch.GetTimestamp();if(BuildDorm&&Session.Construction==null&&Session.Established<Math.Min(BuildTarget,Session.Definition.Capacity)){if(Session.Living<=Session.Established)Session.AddBuddy();if(!Session.BeginConstruction(out string why)){Status=why;BuildDorm=false;return;}}
   if(Session.Construction==null){FinishCurrent=false;if(Session.Established>=Math.Min(BuildTarget,Session.Definition.Capacity))BuildDorm=false;return;}
   bool accelerated=FinishCurrent||BuildDorm;if(!accelerated&&Speed==0){int revision=Session.Revision;Session.TickConstruction(Math.Max(0,Math.Min(.05,dt)));LastTicks=1;LastBatches=Session.Revision!=revision?1:0;Status=Session.ConstructionStatus;return;}accumulator=Math.Min(.8,accumulator+Math.Max(0,Math.Min(dt,.05))*(accelerated?24:Speed==2?16:Speed==1?4:1));
   while(accumulator>=.05&&LastTicks<8){int revision=Session.Revision;Session.TickConstruction(.05);accumulator-=.05;LastTicks++;if(Session.Revision!=revision)LastBatches++;if(Session.Construction==null){Status=Session.ConstructionStatus;FinishCurrent=false;if(!Status.StartsWith("Established"))BuildDorm=false;break;}if(LastBatches>=1||(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2)break;}
   if(Session.Construction!=null)Status=Session.ConstructionStatus;
  }
 }
}
