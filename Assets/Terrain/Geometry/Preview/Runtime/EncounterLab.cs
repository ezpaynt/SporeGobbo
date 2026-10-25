using System;
using System.Collections.Generic;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 public enum LabState{Idle,Alerted,Pursuing,Telegraphing,Searching,Disengaged}
 public enum LabRule{DistanceThreshold,LossOfContact}
 public enum LabAttack{ShortMelee,LongMelee,Projectile,WideArea}
 public enum LabVisibility{OnscreenVisible,OnscreenOccluded,OffscreenVisible,OffscreenOccluded}
 [Serializable] public sealed class LabTuning
 {
  public double detection=12,pursuit=18,shortSearch=5,longSearch=15,speed=3,windup=.9,cooldown=1.8,searchDistance=30;
  public double shortReach=2,longReach=5,projectileReach=18,areaReach=7;
  public LabRule rule=LabRule.LossOfContact;
  public double Reach(LabAttack attack)=>attack==LabAttack.ShortMelee?shortReach:attack==LabAttack.LongMelee?longReach:attack==LabAttack.Projectile?projectileReach:areaReach;
 }
 /// <summary>Bounded, reset-time shared clearance/distance-field experiment, never production navigation.</summary>
 public sealed class LabNavigation
 {
  public const int Radius=96;public readonly int X,Y,Width,Height;readonly int[] prefix,queue;readonly Dictionary<int,Tree> trees=new Dictionary<int,Tree>();
  public int TreeCount=>trees.Count;public int VisitedNodes;public long PayloadBytes=>(long)(prefix.Length+queue.Length)*4+(long)trees.Count*Width*Height*8;
  public readonly Point2 Root;public readonly IOccupancySource Source;
  sealed class Tree{public int[] parent,turns;public int winding=-1,mostTurns=-1;public int root;public double offset;}
  public LabNavigation(IOccupancySource source,Point2 root)
  {
   Source=source;Root=root;X=Math.Max(0,(int)Math.Floor(root.X)-Radius);Y=Math.Max(0,(int)Math.Floor(root.Y)-Radius);Width=Math.Min(source.Width-X,Radius*2+1);Height=Math.Min(source.Height-Y,Radius*2+1);prefix=new int[(Width+1)*(Height+1)];queue=new int[Width*Height];
   for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int i=(y+1)*(Width+1)+x+1;prefix[i]=(source.IsSolid(x+X,y+Y)?1:0)+prefix[i-1]+prefix[i-Width-1]-prefix[i-Width-2];}
  }
  public bool Fits(Point2 p,double size)
  {
   int a=(int)Math.Floor(p.X-size*.5+.00001)-X,b=(int)Math.Ceiling(p.X+size*.5-.00001)-X,c=(int)Math.Floor(p.Y-size*.5+.00001)-Y,d=(int)Math.Ceiling(p.Y+size*.5-.00001)-Y;
   if(size<=0||a<0||c<0||b>Width||d>Height)return false;int w=Width+1;return prefix[d*w+b]-prefix[c*w+b]-prefix[d*w+a]+prefix[c*w+a]==0;
  }
  Tree Get(int size)
  {
   if(trees.TryGetValue(size,out var tree))return tree;tree=new Tree{parent=new int[Width*Height],turns=new int[Width*Height],offset=size%2==0?0:.5,root=-1};for(int i=0;i<tree.parent.Length;i++)tree.parent[i]=-2;
   double best=double.MaxValue;for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){var p=new Point2(x+X+tree.offset,y+Y+tree.offset);double d=Distance(p,Root);if(d<best&&Fits(p,size)){tree.root=y*Width+x;best=d;}}
   trees.Add(size,tree);if(tree.root<0)return tree;int head=0,tail=0;queue[tail++]=tree.root;tree.parent[tree.root]=-1;
   while(head<tail){int i=queue[head++],x=i%Width,y=i/Width;Visit(x-1,y,i);Visit(x+1,y,i);Visit(x,y-1,i);Visit(x,y+1,i);}
   VisitedNodes+=tail;return tree;
   void Visit(int x,int y,int parent){if(x<0||y<0||x>=Width||y>=Height)return;int i=y*Width+x;if(tree.parent[i]!=-2||!Fits(new Point2(x+X+tree.offset,y+Y+tree.offset),size))return;tree.parent[i]=parent;tree.turns[i]=tree.turns[parent]+(tree.parent[parent]>=0&&tree.parent[parent]-parent!=parent-i?1:0);double d=Distance(new Point2(x+X+tree.offset,y+Y+tree.offset),Root);if(d>20&&d<75&&tree.turns[i]>tree.mostTurns){tree.mostTurns=tree.turns[i];tree.winding=i;}queue[tail++]=i;}
  }
  public bool Route(Point2 desired,int size,List<Point2> result)
  {
   result.Clear();var t=Get(size);if(t.root<0)return false;if(TraceToRoot(desired,size,result))return true;double best=double.MaxValue;int start=-1;
   for(int i=0;i<t.parent.Length;i++)if(t.parent[i]!=-2){var p=new Point2(i%Width+X+t.offset,i/Width+Y+t.offset);double d=Distance(p,desired);if(d<best){best=d;start=i;}}
   if(start<0)return false;for(int i=start;i>=0;i=t.parent[i])result.Add(new Point2(i%Width+X+t.offset,i/Width+Y+t.offset));return result.Count>0;
  }
  public void Winding(int size,List<Point2> result){var t=Get(size);int best=t.winding;if(best>=0)TraceToRoot(new Point2(best%Width+X+t.offset,best/Width+Y+t.offset),size,result);else Route(new Point2(Root.X-35,Root.Y-20),size,result);}
  public void Corner(int size,List<Point2> result)
  {
   var tree=Get(size);int chosen=-1;double best=double.MaxValue;var desired=new Point2(Root.X+22,Root.Y-22);for(int i=0;i<tree.parent.Length;i++){if(tree.parent[i]==-2)continue;var p=new Point2(i%Width+X+tree.offset,i/Width+Y+tree.offset);double dx=p.X-Root.X,dy=p.Y-Root.Y;if(dx<12||dx>35||dy> -12||dy< -35)continue;double distance=Distance(p,desired);if(distance<best&&!Contact(Source,Root,p)){best=distance;chosen=i;}}if(chosen>=0)TraceToRoot(new Point2(chosen%Width+X+tree.offset,chosen/Width+Y+tree.offset),size,result);else Route(desired,size,result);
  }
  public bool TraceToRoot(Point2 point,int size,List<Point2> result)
  {
   result.Clear();var t=Get(size);int x=(int)Math.Round(point.X-X-t.offset),y=(int)Math.Round(point.Y-Y-t.offset);if(x<0||y<0||x>=Width||y>=Height)return false;int index=y*Width+x;if(t.parent[index]==-2)return false;var first=new Point2(x+X+t.offset,y+Y+t.offset);int steps=Math.Max(1,(int)Math.Ceiling(Distance(point,first)/.08));for(int i=0;i<=steps;i++){double f=i/(double)steps;if(!Fits(new Point2(point.X+(first.X-point.X)*f,point.Y+(first.Y-point.Y)*f),size))return false;}if(Distance(point,first)>1e-8)result.Add(point);for(int j=index;j>=0;j=t.parent[j])result.Add(new Point2(j%Width+X+t.offset,j/Width+Y+t.offset));return true;
  }
  public static double Distance(Point2 a,Point2 b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
  public static bool Contact(IOccupancySource source,Point2 a,Point2 b)
  {
   double length=Distance(a,b);int steps=Math.Max(1,(int)Math.Ceiling(length/.2));for(int i=0;i<=steps;i++){double t=i/(double)steps;if(source.IsSolid((int)Math.Floor(a.X+(b.X-a.X)*t),(int)Math.Floor(a.Y+(b.Y-a.Y)*t)))return false;}return true;
  }
 }
 public sealed class LabActor
 {
  public int Id,Size=1,Kind;public string Name;public bool Persistent,Active,SpawnValid,Blocked;public LabAttack Attack;public LabState State;
  public Point2 Position,Previous,Home,AttackDirection=new Point2(1,0),LastKnown;public double Clock,Timer,Distance,AttackClock,Attacks,Accumulator,LogicalSpeed;
  public readonly List<Point2> Route=new List<Point2>();public int RouteIndex;public PreviewLogicalProjectile Shot; public Func<LabActor,Point2,bool> AttackReadable;
  public void Reset(int id,string name,List<Point2> route,int size,bool persistent,LabAttack attack)
  {
   Id=id;Name=name;Size=size;Persistent=persistent;Attack=attack;Route.Clear();Route.AddRange(route);RouteIndex=0;SpawnValid=Route.Count>0;Home=Position=Previous=SpawnValid?Route[0]:new Point2(0,0);LastKnown=SpawnValid?Route[Route.Count-1]:Home;State=LabState.Idle;Clock=Timer=AttackClock=Attacks=Accumulator=LogicalSpeed=0;Blocked=false;Active=false;Distance=0;AttackDirection=new Point2(1,0);Shot=null;
  }
  public void Step(double dt,Point2 player,LabTuning tuning,IOccupancySource source)
  {
   if(!SpawnValid||dt<=0)return;Distance=LabNavigation.Distance(Position,player);Active=Distance<=64||State!=LabState.Idle&&State!=LabState.Disengaged;
   if(!Active){Accumulator+=dt;if(Accumulator<.5)return;dt=Accumulator;Accumulator=0;}
   Previous=Position;Clock+=dt;AttackClock=Math.Max(0,AttackClock-dt);bool contact=LabNavigation.Contact(source,Position,player);double reach=tuning.Reach(Attack),limit=Persistent?tuning.longSearch:tuning.shortSearch;
   if(Shot!=null&&!Shot.Finished)Shot.Advance(dt,10,tuning.projectileReach,source);
   if(State==LabState.Idle||State==LabState.Disengaged){if(Distance<=tuning.detection&&contact){State=LabState.Alerted;Timer=.35;}else if(State==LabState.Idle)MoveRoute(dt,tuning.speed,source);}
   else if(State==LabState.Alerted){Timer-=dt;if(Timer<=0){State=LabState.Pursuing;Timer=limit;}}
   else if(State==LabState.Pursuing)
   {
    if(contact&&(tuning.rule==LabRule.LossOfContact||Distance<=tuning.pursuit)){Timer=limit;LastKnown=player;}else Timer=Math.Max(0,Timer-dt);
    bool lost=tuning.rule==LabRule.DistanceThreshold?Distance>tuning.pursuit:!contact;
    if(lost&&(!Persistent||Timer<=0)||Distance>tuning.searchDistance&&!Persistent){State=LabState.Searching;Timer=limit;}
    else if(Distance<=reach&&contact&&AttackClock<=0&&(AttackReadable==null||AttackReadable(this,player))){State=LabState.Telegraphing;Timer=tuning.windup;if(Distance>.0001)AttackDirection=new Point2((player.X-Position.X)/Distance,(player.Y-Position.Y)/Distance);}
    else {if(contact)MoveToward(player,dt,tuning.speed,source);else MoveRoute(dt,tuning.speed,source);}
   }
   else if(State==LabState.Telegraphing)
   {
    if(AttackReadable!=null&&!AttackReadable(this,player)){Timer=tuning.windup;return;}Timer-=dt;if(Timer<=0){Attacks++;if(Attack==LabAttack.Projectile){if(Shot!=null&&Shot.Origin.Equals(Position)&&Shot.Direction.Equals(AttackDirection))Shot.Reset();else Shot=new PreviewLogicalProjectile(Position,AttackDirection);}State=LabState.Pursuing;Timer=limit;AttackClock=tuning.cooldown;}
   }
   else if(State==LabState.Searching){Timer-=dt;if(LabNavigation.Contact(source,Position,LastKnown))MoveToward(LastKnown,dt,tuning.speed*.5,source);else MoveRoute(dt,tuning.speed*.5,source);if(contact&&Distance<=tuning.detection){State=LabState.Pursuing;Timer=limit;}else if(Timer<=0)State=LabState.Disengaged;}
   LogicalSpeed=LabNavigation.Distance(Previous,Position)/dt;
  }
  void MoveRoute(double dt,double speed,IOccupancySource source)
  {
   if(speed<=0)return;double remaining=dt*speed;int guard=0;while(RouteIndex<Route.Count&&remaining>1e-8&&guard++<128){double distance=LabNavigation.Distance(Position,Route[RouteIndex]);if(distance<.00001){RouteIndex++;continue;}double step=Math.Min(distance,remaining);var previous=Position;MoveToward(Route[RouteIndex],step/speed,speed,source);remaining-=step;if(LabNavigation.Distance(previous,Position)<step-.001)return;if(LabNavigation.Distance(Position,Route[RouteIndex])<.00001)RouteIndex++;}
  }
  void MoveToward(Point2 target,double dt,double speed,IOccupancySource source)
  {
   double d=LabNavigation.Distance(Position,target);if(d<.0001)return;double step=Math.Min(d,dt*speed);var desired=new Vector2((float)((target.X-Position.X)/d*step),(float)((target.Y-Position.Y)/d*step));var p=PreviewMotion.Move(source,new Vector2((float)Position.X,(float)Position.Y),desired,Size);Blocked=(p-new Vector2((float)Position.X,(float)Position.Y)).sqrMagnitude<.0000001f;Position=new Point2(p.x,p.y);
  }
 }
 public static class LabPresentation
 {
  public static LabVisibility Visibility(FocusProjection p,Point2 player,Point2 target,double width,double height,IOccupancySource source){var q=p.Project(target);bool onscreen=q.X>=-width*.5&&q.X<=width*.5&&q.Y>=-height*.5&&q.Y<=height*.5;bool contact=LabNavigation.Contact(source,player,target);return onscreen?(contact?LabVisibility.OnscreenVisible:LabVisibility.OnscreenOccluded):(contact?LabVisibility.OffscreenVisible:LabVisibility.OffscreenOccluded);}
  public static ProjectionBounds BillboardBounds(FocusProjection p,Point2 anchor,ProjectionBounds logical,int mode){var a=p.Project(anchor);var s=Scale(p,anchor,mode);return new ProjectionBounds(new Point2(a.X+(logical.Min.X-anchor.X)*s.x,a.Y+(logical.Min.Y-anchor.Y)*s.y),new Point2(a.X+(logical.Max.X-anchor.X)*s.x,a.Y+(logical.Max.Y-anchor.Y)*s.y));}
  public static bool Intersects(ProjectionBounds b,double width,double height)=>b.Max.X>=-width*.5&&b.Min.X<=width*.5&&b.Max.Y>=-height*.5&&b.Min.Y<=height*.5;
  public static Point2 Edge(FocusProjection p,Point2 target,double width,double height,double margin=1.5){var q=p.Project(target);double scale=Math.Min(1,Math.Min((width*.5-margin)/Math.Max(.00001,Math.Abs(q.X)),(height*.5-margin)/Math.Max(.00001,Math.Abs(q.Y))));return new Point2(q.X*scale,q.Y*scale);}
  public static Vector2 Scale(FocusProjection p,Point2 anchor,int mode)=>Stage4CameraPreview.ActorScale(p,anchor,mode);
 }
}
