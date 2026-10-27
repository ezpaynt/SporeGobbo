using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;
namespace SporeGobbo.CampCandidate.Editor
{
 public static class Stage56DefinitionEvidence
 {
  [Serializable]public class Marker{public string id,name,gate,material;public double x,y,interactionX,interactionY;}
  [Serializable]public class Room{public string id;public int firstSlot,capacity;public double entranceX,entranceY;public List<Vector2> beds=new List<Vector2>();public List<Vector2Int> cells=new List<Vector2Int>();}
  [Serializable]public class Route{public string state,from,to;public int body,length;public bool reachable;}
  [Serializable]public class Data{public string id,fingerprint;public int width=192,height=192;public double cellSize=.6;public List<string> rows=new List<string>();public List<Marker> markers=new List<Marker>();public List<Room> rooms=new List<Room>();public List<Route> routes=new List<Route>();}
  public static void Export(string output){Directory.CreateDirectory(output);var d=AuthoredCamp.Create();var e=new Data{id=d.Id,fingerprint=d.Fingerprint()};for(int y=0;y<192;y++){var row=new char[192];for(int x=0;x<192;x++)row[x]=".#drbmsf"[(int)d.Category(x,y)];e.rows.Add(new string(row));}foreach(var m in d.Markers)e.markers.Add(new Marker{id=m.Id,name=m.Name,gate=m.Gate.ToString(),material=m.MaterialId,x=m.Anchor.X,y=m.Anchor.Y,interactionX=m.Interaction.X,interactionY=m.Interaction.Y});foreach(var room in d.Bedrooms){var r=new Room{id=room.Id,firstSlot=room.FirstSlot,capacity=room.Beds.Count,entranceX=room.Entrance.X,entranceY=room.Entrance.Y};foreach(var b in room.Beds)r.beds.Add(new Vector2((float)b.X,(float)b.Y));foreach(var c in room.Cells)r.cells.Add(new Vector2Int(c.X,c.Y));e.rooms.Add(r);}
   void Validate(CampSession s,string state){foreach(int size in new[]{1,3,5,9}){foreach(var m in d.Markers)if(s.Active(m.Gate)){int count=s.Route(d.Marker("fire").Interaction,m.Interaction,size).Count;e.routes.Add(new Route{state=state,from="fire",to=m.Id,body=size,length=Math.Max(0,count-1),reachable=count>0});}foreach(var r in d.Bedrooms)for(int i=0;i<r.Beds.Count;i++)if(r.FirstSlot+i<=s.Established){int count=s.Route(r.Beds[i],d.Marker("fire").Interaction,size).Count;e.routes.Add(new Route{state=state,from=r.Id+"/bed"+(r.FirstSlot+i),to="fire",body=size,length=Math.Max(0,count-1),reachable=count>0});}}}
   var session=new CampSession(d);Validate(session,"first-arrival");session.Discover();Validate(session,"ordinary-discovered");session.AddBuddy();Validate(session,"first-buddy");for(int i=0;i<10;i++){if(i>0)session.AddBuddy();if(!session.ConstructNext(out string why))throw new Exception("Candidate construction "+i+": "+why);Validate(session,"bed-"+(i+1));}session.BuddyDied();Validate(session,"first-death");session.Advance();Validate(session,"shop-inspection");session.Advance();Validate(session,"fight-after-shop-inspection");File.WriteAllText(Path.Combine(output,"candidate-definition.json"),JsonUtility.ToJson(e,true));
  }
 }
}
