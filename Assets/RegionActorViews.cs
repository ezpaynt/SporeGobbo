using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;
/// <summary>Shared real-actor binding, clearance and animated projection; no spawn or campaign ownership.</summary>
public static class RegionActorViews
{
 public static Stage59ActorView Bind(IWorldRegion region,Camera camera,List<Stage59ActorView> views,GameObject actor,Func<bool> ready)
 {
  var body=actor.GetComponent<Rigidbody2D>();if(body==null)throw new InvalidOperationException("Actor lacks Rigidbody2D: "+actor.name);
  var binding=actor.GetComponent<ActorTerrainBinding>()??actor.AddComponent<ActorTerrainBinding>();binding.Bind(region);binding.SuppressCampaignBootstrap=false;binding.Ready=ready;binding.ReadRevision=()=>region.Revision;binding.FindRoute=(a,b)=>region.Route(a,b,TileMover.GetMapClearanceExtents(body,.22f));binding.PointerToWorld=s=>{if(region.Projection==null)return body.position;var p=camera.ScreenToWorldPoint(new Vector3(s.x,s.y,10));return region.World(region.Projection.Unproject(new Point2(p.x,-p.y)));};
  if(region is IRegionNavigation navigation)navigation.BindNavigation(binding);
  var dig=actor.GetComponent<BuddyDigAbility>();if(dig!=null)dig.BindTerrain(region);
  Physics2D.SyncTransforms();var extent=TileMover.GetMapClearanceExtents(body,.22f);Vector2 origin=body.position;
  bool Fits(Vector2 p)=>TileMover.CanOccupyBox(region,p,extent)&&!views.Any(v=>v!=null&&v.gameObject!=actor&&v.GetComponent<Rigidbody2D>() is Rigidbody2D other&&(other.position-p).sqrMagnitude<Mathf.Pow(extent.magnitude+TileMover.GetMapClearanceExtents(other,.22f).magnitude,2));
  if(!Fits(origin)){float best=float.PositiveInfinity;Vector2 selected=default;var cell=region.WorldToCell(origin);for(int y=-6;y<=6;y++)for(int x=-6;x<=6;x++){var p=region.CellToWorld(cell+new Vector2Int(x,y));float d=(p-origin).sqrMagnitude;if(d<best&&Fits(p)&&region.RayClear(origin,p,.02f)){best=d;selected=p;}}if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No clear local spawn for "+actor.name);body.position=selected;actor.transform.position=selected;}
  var view=actor.GetComponent<Stage59ActorView>()??actor.AddComponent<Stage59ActorView>();view.Context=region;view.Outline=false;view.Initialize();if(!views.Contains(view))views.Add(view);return view;
 }
 public static void Draw(List<Stage59ActorView> views){views.RemoveAll(v=>v==null);views.Sort((a,b)=>b.PhysicalFoot.y.CompareTo(a.PhysicalFoot.y));for(int i=0;i<views.Count;i++)views[i].Draw(20+i*2);}
 public static void BindVisual(IWorldRegion region,List<Stage59ActorView> views,GameObject visual)
 {if(visual==null||visual.GetComponent<Stage59ActorView>()!=null)return;var source=visual.GetComponentInChildren<SpriteRenderer>(true);if(source==null)return;foreach(var canvas in visual.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);if(visual.GetComponent<Collider2D>()==null){var c=visual.AddComponent<CircleCollider2D>();c.isTrigger=true;c.radius=.05f;}var view=visual.AddComponent<Stage59ActorView>();view.SourceOverride=source;view.Context=region;view.Outline=false;view.ShowGroundShadow=false;view.Initialize();views.Add(view);}
}
