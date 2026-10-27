using System;
using System.Collections.Generic;
using SporeGobbo.Geometry;
using SporeGobbo.CampLifecycle;
namespace SporeGobbo.CampCandidate
{
 /// <summary>Authored geography only. No campaign, simulation, camera or persistence ownership.</summary>
 public static class SettlementCampLayout
 {
  public const string LayoutId="camp.settlement.61c.v3";
  public const string RootId="camp.root.optional-pocket";
  static AuthoredCamp shared;
  // AuthoredCamp defensively copies its data and exposes read-only values. Never cache a live world here.
  public static AuthoredCamp Shared=>shared??(shared=Create());
  public static readonly Point2 RootFront=new Point2(129.5,106.5);
  public static List<DualTile> RootCells(){var r=new List<DualTile>();for(int y=100;y<=102;y++)for(int x=126;x<=132;x++)r.Add(new DualTile(x,y));return r;}
  public static AuthoredCamp Create()
  {
   var previous=Stage57Layout.Create();var a=new CampCell[192*192];
   for(int y=0;y<192;y++)for(int x=0;x<192;x++)a[y*192+x]=previous.Category(x,y);
   // Remove the old spread-out dorm and its two public approaches. This is ordinary shapeable earth.
   for(int y=54;y<=102;y++)for(int x=90;x<=142;x++)a[y*192+x]=CampCell.Expansion;
   var district=new HashSet<DualTile>();var spine=new HashSet<DualTile>();
   void Lobe(HashSet<DualTile> set,double cx,double cy,double rx,double ry)
   {for(int y=(int)(cy-ry-2);y<=cy+ry+2;y++)for(int x=(int)(cx-rx-2);x<=cx+rx+2;x++){double dx=(x-cx)/rx,dy=(y-cy)/ry,t=Math.Atan2(dy,dx);double edge=1+.07*Math.Sin(t*5)+.025*Math.Cos(t*3);if(dx*dx+dy*dy<edge*edge)set.Add(new DualTile(x,y));}}
   void Tube(HashSet<DualTile> set,int ax,int ay,int bx,int by,double radius)
   {int n=Math.Max(1,Math.Max(Math.Abs(ax-bx),Math.Abs(ay-by))*2);for(int i=0;i<=n;i++){double t=i/(double)n;Lobe(set,ax+(bx-ax)*t,ay+(by-ay)*t,radius,radius);}}
   Tube(spine,107,95,107,81,3.3);Tube(spine,104,90,111,90,2.7);Tube(spine,104,80,111,80,2.7);
   district.UnionWith(spine);var rooms=new List<CampBedroom>();int first=1;
   void Room(string id,int cx,int cy,int ex,int ey,int count,double rx,double ry)
   {
    var envelope=new HashSet<DualTile>();Lobe(envelope,cx,cy,rx,ry);Tube(envelope,cx,cy,ex,ey,2.1);district.UnionWith(envelope);
    var owned=new List<DualTile>(envelope);owned.Sort((p,q)=>p.Y!=q.Y?p.Y.CompareTo(q.Y):p.X.CompareTo(q.X));
    var beds=new List<Point2>();for(int j=0;j<count;j++)beds.Add(new Point2(cx-(count-1)+j*2+.5,cy+.5));
    rooms.Add(new CampBedroom(id,new Point2(ex+.5,ey+.5),owned,beds,first));first+=count;
   }
   Room("burrow.fern",99,91,104,90,3,5.6,4.0);
   Room("burrow.root",116,89,111,90,3,5.2,4.8);
   Room("burrow.lichen",100,80,104,80,2,5.1,4.0);
   Room("burrow.end",115,79,111,80,2,4.7,4.7);
   // A local structural skeleton protects this one authored discovery, not the whole upper Camp.
   foreach(var p in district)for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
    if(!district.Contains(new DualTile(p.X+dx,p.Y+dy)))a[(p.Y+dy)*192+p.X+dx]=CampCell.Permanent;
   foreach(var p in district)a[p.Y*192+p.X]=spine.Contains(p)?CampCell.Open:CampCell.Residential;
   for(int y=94;y<=103;y++)for(int x=104;x<=110;x++)a[y*192+x]=CampCell.Open;
   // Cozy Squad approach: open circulation, small protected station backing, no giant divider.
   var gathering=new HashSet<DualTile>();Lobe(gathering,98,103,9,5.2);Tube(gathering,97,108,107,103,3.6);
   foreach(var p in gathering)if(p.Y>=100||p.X<103)a[p.Y*192+p.X]=CampCell.Open;
   // Remove the inherited Fire pedestal, lower-left shop backing and central crescent.
   var publicFloor=new HashSet<DualTile>();Lobe(publicFloor,84,112,8,6);Lobe(publicFloor,91,98,3.5,3.2);
   Lobe(publicFloor,75,132,5,4);Lobe(publicFloor,56,106,4,4);
   // Break the obstructive lower crescent into shapeable lobes with one comfortable existing-loop connection.
   Tube(publicFloor,89,120,90,129,3.1);Tube(publicFloor,90,129,96,136,3.1);
   publicFloor.Add(new DualTile(106,123));publicFloor.Add(new DualTile(107,107));publicFloor.Add(new DualTile(59,103));Tube(publicFloor,85,108,98,104,3.4);
   foreach(var p in publicFloor)a[p.Y*192+p.X]=CampCell.Open;
   // A secondary east shoulder on the existing route toward the Pit, not the Intro approach.
   var shopFloor=new HashSet<DualTile>();Tube(shopFloor,111,122,123,128,3.5);Lobe(shopFloor,129,129,8,5);
   foreach(var p in shopFloor)a[p.Y*192+p.X]=CampCell.Open;
   var markers=new List<CampMarker>();
   foreach(var m in previous.Markers)
   {
    if(m.Id=="fire")markers.Add(new CampMarker("fire","Fire",new Point2(84.5,113.5),new Point2(84.5,116.5)));
    else if(m.Id=="storage")markers.Add(new CampMarker(m.Id,m.Name,new Point2(60.5,111.5),m.Interaction));
    else if(m.Id=="shop.reserved")markers.Add(new CampMarker(m.Id,m.Name,new Point2(132.5,124.5),new Point2(129.5,130.5),CampCell.Shop));
    else markers.Add(m);
   }
   // Optional root pocket has no reward or bed. Its protected shoulders prevent bypass.
   var pocket=new HashSet<DualTile>();Lobe(pocket,129,95,4.5,4.3);
   foreach(var p in pocket)for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
    if(!pocket.Contains(new DualTile(p.X+dx,p.Y+dy)))a[(p.Y+dy)*192+p.X+dx]=CampCell.Permanent;
   foreach(var p in pocket)a[p.Y*192+p.X]=CampCell.Open;
   for(int y=99;y<=107;y++)for(int x=126;x<=132;x++)a[y*192+x]=y>=100&&y<=102?CampCell.FirstBuddy:CampCell.Open;
   var optionalApproach=new HashSet<DualTile>();Tube(optionalApproach,110,108,129,107,3.3);
   foreach(var p in optionalApproach)if(p.Y>=104)a[p.Y*192+p.X]=CampCell.Open;
   markers.Add(new CampMarker(RootId,"Optional ancient roots",new Point2(129.5,101.5),RootFront,CampCell.FirstBuddy,"terrain.root"));
   foreach(var m in markers)if(m.Id!="arrival.first"&&m.Id!="fire"&&m.Id!="storage"&&m.Id!=RootId)
    for(int y=(int)m.Anchor.Y-3;y<=(int)m.Anchor.Y;y++)for(int x=(int)m.Anchor.X-3;x<=(int)m.Anchor.X+3;x++)a[y*192+x]=CampCell.Permanent;
   // Bones/Exit shortcut dirt remains ordinary; the station backings above are immutable.
   return new AuthoredCamp(a,markers,rooms,LayoutId);
  }
  public static CampResidentialCatalog Catalog(AuthoredCamp definition)
  {
   var potential=new RegionTerrainState(definition.Id,192,192,definition);var ordinary=new List<DualTile>();
   for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(definition.Category(x,y)==CampCell.Residential)ordinary.Add(new DualTile(x,y));
   potential.Commit(ordinary,0,c=>true);var rooms=new List<CampResidentialRoomDefinition>();int progression=0;
   foreach(var room in definition.Bedrooms)
   {
    var slots=new List<CampResidentialSlotDefinition>();int offset=0;
    foreach(var bed in room.Beds)
    {
     int id=room.FirstSlot+offset++;var path=potential.Route(room.Entrance,bed,3);
     if(path.Count==0)throw new InvalidOperationException("No potential clearance route for "+room.Id+" bed "+id);
     var targets=new List<(int x,int y)>();var footprint=new HashSet<(int x,int y)>();
     foreach(var p in path){int x=(int)p.X,y=(int)p.Y;targets.Add((x,y));for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(definition.Category(x+dx,y+dy)==CampCell.Residential)footprint.Add((x+dx,y+dy));}
     var ordered=new List<(int x,int y)>(footprint);ordered.Sort((p,q)=>p.y!=q.y?p.y.CompareTo(q.y):p.x.CompareTo(q.x));
     slots.Add(new CampResidentialSlotDefinition(id,id-1,((int)bed.X,(int)bed.Y),((int)bed.X,(int)bed.Y),((int)room.Entrance.X,(int)room.Entrance.Y),targets,ordered,new[]{((int)room.Entrance.X,(int)room.Entrance.Y)}));
    }
    rooms.Add(new CampResidentialRoomDefinition(room.Id,room.Id,++progression,new CampCellRect(92,72,32,26),new CampCellRect((int)room.Entrance.X-1,(int)room.Entrance.Y-1,3,3),slots));
   }
   return new CampResidentialCatalog(rooms);
  }
 }
}
