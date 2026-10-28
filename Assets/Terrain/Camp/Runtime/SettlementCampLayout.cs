using System;using System.Linq;using System.Collections.Generic;using SporeGobbo.Geometry;using SporeGobbo.CampLifecycle;
namespace SporeGobbo.CampCandidate
{
 /// <summary>One authored Camp, with preserved marker/bed identities. No campaign or live terrain authority.</summary>
 public static class SettlementCampLayout
 {
  public const string LayoutId="camp.settlement.64b.v1";
  public const string RootId=LegacyCamp61CLayout.RootId;
  public const int DormShiftX=-2,DormShiftY=-2;
  public static readonly Point2 RootFront=LegacyCamp61CLayout.RootFront;
  public static List<DualTile> RootCells()=>LegacyCamp61CLayout.RootCells();
  static AuthoredCamp shared;public static AuthoredCamp Shared=>shared??(shared=Create());
  public static AuthoredCamp Create()
  {
   var accepted=LegacyCamp61CLayout.Shared;var a=new CampCell[192*192];for(int y=0;y<192;y++)for(int x=0;x<192;x++)a[y*192+x]=accepted.Category(x,y);
   for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(a[y*192+x]==CampCell.FightPit)a[y*192+x]=CampCell.Expansion;
   // Move this small neighborhood, rather than replacing its ownership/construction model.
   for(int y=70;y<=100;y++)for(int x=89;x<=123;x++)if(a[y*192+x]!=CampCell.Open)a[y*192+x]=CampCell.Expansion;
   var district=new HashSet<DualTile>();var open=new HashSet<DualTile>();var rooms=new List<CampBedroom>();
   foreach(var old in accepted.Bedrooms)
   {
    var cells=new List<DualTile>();foreach(var c in old.Cells){var moved=new DualTile(c.X+DormShiftX,c.Y+DormShiftY);cells.Add(moved);district.Add(moved);if(accepted.Category(c.X,c.Y)==CampCell.Open)open.Add(moved);}
    rooms.Add(new CampBedroom(old.Id,old.Entrance+new Point2(DormShiftX,DormShiftY),cells,old.Beds.Select(p=>p+new Point2(DormShiftX,DormShiftY)).ToList(),old.FirstSlot));
   }
   for(int y=72;y<=99;y++)for(int x=92;x<=123;x++)if(accepted.Category(x,y)==CampCell.Open)open.Add(new DualTile(x+DormShiftX,y+DormShiftY));
   CampFightPitLayout.Tube(open,105,94,105,102,3.1);district.UnionWith(open);
   foreach(var c in district)for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
   {var q=new DualTile(c.X+dx,c.Y+dy);if(!district.Contains(q)&&accepted.Category(q.X,q.Y)!=CampCell.Open)a[q.Y*192+q.X]=CampCell.Permanent;}
   foreach(var c in district)a[c.Y*192+c.X]=open.Contains(c)?CampCell.Open:CampCell.Residential;
   // Generous irregular fighting floor plus fully walkable outer loop through the original root vestibule.
   var pit=new HashSet<DualTile>(CampFightPitLayout.FloorCells);var roots=new HashSet<DualTile>(RootCells());
   foreach(var c in pit)for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
   {var q=new DualTile(c.X+dx,c.Y+dy);if(!pit.Contains(q)&&!roots.Contains(q)&&accepted.Category(q.X,q.Y)!=CampCell.Open)a[q.Y*192+q.X]=CampCell.Permanent;}
   foreach(var c in pit)if(!roots.Contains(c))a[c.Y*192+c.X]=CampCell.Open;
   // Continuous bedroom/Pit structural separation; the public way back goes through the root entrance and Fire.
   for(int y=63;y<=99;y++)for(int x=122;x<=124;x++)if(accepted.Category(x,y)!=CampCell.Open)a[y*192+x]=CampCell.Permanent;
   foreach(var c in roots)a[c.Y*192+c.X]=CampCell.FirstBuddy;
   // A convenient back-wall approach between Bones and departure, retaining both immutable station backs.
   var back=new HashSet<DualTile>();CampFightPitLayout.Tube(back,69,72,77,70,3.4);CampFightPitLayout.Tube(back,77,70,87,67,3.4);foreach(var c in back)a[c.Y*192+c.X]=CampCell.Open;
   // Keep five-cell-wide existing station approaches before adding ordinary diggable shoulders.
   var protectedFloor=new HashSet<DualTile>();var oldTerrain=new RegionTerrainState("layout-validation",192,192,accepted);var fire=accepted.Marker("fire").Interaction;
   foreach(string id in new[]{"arrival.first","squad","bones","run.departure","shop.reserved",RootId})foreach(var p in oldTerrain.Route(fire,accepted.Marker(id).Interaction,3))for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)protectedFloor.Add(new DualTile((int)p.X+dx,(int)p.Y+dy));
   var dirt=new HashSet<DualTile>();CampFightPitLayout.Lobe(dirt,64,112,4,5);CampFightPitLayout.Lobe(dirt,74,101,4.5,3.5);CampFightPitLayout.Lobe(dirt,92,106,4,2.3);CampFightPitLayout.Lobe(dirt,88,123,9,2.6);CampFightPitLayout.Lobe(dirt,110,114,3.5,5);
   foreach(var c in dirt)if(a[c.Y*192+c.X]==CampCell.Open&&!protectedFloor.Contains(c)&&accepted.Markers.All(m=>Math.Pow(c.X-m.Interaction.X,2)+Math.Pow(c.Y-m.Interaction.Y,2)>16))a[c.Y*192+c.X]=CampCell.Expansion;
   var markers=new List<CampMarker>();foreach(var m in accepted.Markers)
   {
    if(m.Id=="fight.reserved"){markers.Add(new CampMarker(m.Id,"Fight Pit",new Point2(135.5,94.5),CampFightPitLayout.Entry,CampCell.Open));continue;}
    markers.Add(m);
    if(m.Id=="arrival.first"||m.Id==RootId)continue;
    // Preserve exact accepted station protection, including its small irregular shoulders.
    for(int y=(int)m.Anchor.Y-4;y<=(int)m.Anchor.Y+3;y++)for(int x=(int)m.Anchor.X-4;x<=(int)m.Anchor.X+4;x++)if(accepted.Category(x,y)==CampCell.Permanent)a[y*192+x]=CampCell.Permanent;
   }
   return new AuthoredCamp(a,markers,rooms,LayoutId);
  }
  public static CampResidentialCatalog Catalog(AuthoredCamp definition)
  {
   var potential=new RegionTerrainState(definition.Id,192,192,definition);var ordinary=new List<DualTile>();for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(definition.Category(x,y)==CampCell.Residential)ordinary.Add(new DualTile(x,y));potential.Commit(ordinary,0,c=>true);
   var rooms=new List<CampResidentialRoomDefinition>();int progression=0;
   foreach(var room in definition.Bedrooms)
   {
    var slots=new List<CampResidentialSlotDefinition>();int offset=0;
    foreach(var bed in room.Beds)
    {
     int id=room.FirstSlot+offset++;var path=potential.Route(room.Entrance,bed,3);if(path.Count==0)throw new InvalidOperationException("No potential clearance route for "+room.Id+" bed "+id);
     var targets=new List<(int x,int y)>();var footprint=new HashSet<(int x,int y)>();foreach(var p in path){int x=(int)p.X,y=(int)p.Y;targets.Add((x,y));for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(definition.Category(x+dx,y+dy)==CampCell.Residential)footprint.Add((x+dx,y+dy));}
     var ordered=new List<(int x,int y)>(footprint);ordered.Sort((p,q)=>p.y!=q.y?p.y.CompareTo(q.y):p.x.CompareTo(q.x));slots.Add(new CampResidentialSlotDefinition(id,id-1,((int)bed.X,(int)bed.Y),((int)bed.X,(int)bed.Y),((int)room.Entrance.X,(int)room.Entrance.Y),targets,ordered,new[]{((int)room.Entrance.X,(int)room.Entrance.Y)}));
    }
    int minX=room.Cells.Min(c=>c.X)-2,minY=room.Cells.Min(c=>c.Y)-2,maxX=room.Cells.Max(c=>c.X)+2,maxY=room.Cells.Max(c=>c.Y)+2;
    rooms.Add(new CampResidentialRoomDefinition(room.Id,room.Id,++progression,new CampCellRect(minX,minY,maxX-minX+1,maxY-minY+1),new CampCellRect((int)room.Entrance.X-1,(int)room.Entrance.Y-1,3,3),slots));
   }
   return new CampResidentialCatalog(rooms);
  }
 }
}
