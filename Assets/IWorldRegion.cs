using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;
/// <summary>Actual scene-region reads used by run lifecycle and existing combat. Occupancy lives in RegionTerrainState.</summary>
public interface IWorldRegion:IDiggableTerrain,IRegionActorBinding,IProjectedActorContext
{ Vector2 World(Point2 logical);bool LogicalReady{get;}int Revision{get;}Vector2 Arrival{get;}List<Vector2> Route(Vector2 a,Vector2 b,Vector2 extents);bool RayClear(Vector2 a,Vector2 b,float radius=0);void BindVisual(GameObject visual); }
public interface IRegionNavigation { void BindNavigation(ActorTerrainBinding binding); }
public static class RegionQueries
{
 public static IWorldRegion Current=>DiggableTerrainService.Active as IWorldRegion;
 public static IProjectedActorContext Projected=>DiggableTerrainService.Active as IProjectedActorContext;
 public static bool CombatReady=>DiggableTerrainService.Active is IWorldRegion r?r.LogicalReady:DiggableTerrainService.Active is SettlementCampRegion camp&&camp.LogicalReady;
 public static bool ProjectileClear(IDiggableTerrain terrain,Vector2 a,Vector2 b,float radius)
 =>terrain!=null&&object.ReferenceEquals(terrain,DiggableTerrainService.Active)&&CombatReady&&TileMover.CanTraverseBox(terrain,a,b,Vector2.one*Mathf.Max(.005f,radius));
 public static bool TerrainCollider(Collider2D c)=>DiggableTerrainService.Active is ExpeditionRegion r?r.DerivedCollider(c):DiggableTerrainService.Active is SettlementCampRegion camp&&camp.IsDerivedTerrainCollider(c);
 public static bool Clear(GameObject actor,Vector2 p,float radius){var terrain=ActorTerrainBinding.Resolve(actor);if(terrain!=null)return EnemyDamage.Ready(actor)&&TileMover.CanOccupyBox(terrain,p,TileMover.GetMapClearanceExtents(actor.GetComponent<Rigidbody2D>(),radius));return MapGenerator.Instance!=null&&MapGenerator.Instance.IsWorldPositionClearForBody(p,radius);}
 public static void BindVisual(GameObject visual){if(Current!=null)Current.BindVisual(visual);else if(DiggableTerrainService.Active is IRegionVisualBinding binding)binding.BindStaticVisual(visual,visual.transform);}

 public static bool Sight(GameObject actor,Vector2 a,Vector2 b){var binding=actor.GetComponent<ActorTerrainBinding>();return binding!=null?binding.Sight(a,b):MapPathfinder.HasLineOfWalkableSight(a,b);}
 public static bool Vision(GameObject actor,Vector2 a,Vector2 b){var r=Current;return r!=null?r.LogicalReady&&r.RayClear(a,b):Sight(actor,a,b);}
 public static bool Probe(GameObject actor,Vector2 a,Vector2 b,out List<Vector2> path){var binding=actor.GetComponent<ActorTerrainBinding>();if(binding!=null)return binding.TryProbe(a,b,out path);return MapPathfinder.TryFindPath(a,b,out path);}
 public static bool Path(GameObject actor,Vector2 a,Vector2 b,out List<Vector2> path){var binding=actor.GetComponent<ActorTerrainBinding>();if(binding!=null)return binding.TryPath(a,b,out path);return MapPathfinder.TryFindPath(a,b,out path);}
 public static void Bind(GameObject actor){Current?.BindActor(actor);}
}
