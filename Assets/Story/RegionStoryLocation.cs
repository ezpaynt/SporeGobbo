using System.Collections.Generic;using UnityEngine;
/// <summary>Existing story presentation marker data, independent of generator profiles.</summary>
public sealed class RegionStoryLocation
{
 public string markerKey;public Vector2Int actorCell,focusCell,triggerCell,despawnCell;
 public Vector3 cameraOffset=new Vector3(0,0,-10);public float cameraZoomMultiplier=.82f,triggerRadius=2;
 public List<Vector2Int> departureWaypoints=new List<Vector2Int>();
 // Removable compatibility conversion for the preserved legacy authored test layout.
 public static RegionStoryLocation Legacy(PersistentTestCaveLayout.AuthoredEventLocation p)=>new RegionStoryLocation{markerKey=p.markerKey,actorCell=p.actorCell,focusCell=p.focusCell,triggerCell=p.triggerCell,despawnCell=p.despawnCell,cameraOffset=p.cameraOffset,cameraZoomMultiplier=p.cameraZoomMultiplier,triggerRadius=p.triggerRadius,departureWaypoints=p.departureWaypoints};
}
