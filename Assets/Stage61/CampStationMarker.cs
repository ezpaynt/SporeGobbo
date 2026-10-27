using UnityEngine;
/// <summary>Stable authored station binding. Existing station component owns its interaction.</summary>
public sealed class CampStationMarker:MonoBehaviour,IWorldInteractionAnchor,IProjectedInteractionAnchor
{
 public string markerId;
 SettlementCampRegion region;Stage59ActorView[] views;CampDepartureMouth mouth;
 public Vector2 InteractionPoint=>transform.position;
 public Vector2 PromptPoint {get{return region!=null?region.World(region.Definition.Marker(markerId).Anchor):InteractionPoint;}}
 public Vector3 ProjectedPromptAnchor{get{if(mouth!=null)return mouth.ProjectedPromptAnchor;Vector3 point=region!=null?region.Render(PromptPoint):Vector3.zero;if(views!=null)foreach(var view in views)if(view!=null&&view.IsPresented&&view.ProjectedHead.y>point.y)point=view.ProjectedHead;return point;}}
 void Start(){region=Object.FindAnyObjectByType<SettlementCampRegion>();if(region==null)throw new System.InvalidOperationException("Station has no active authored region");region.BindStation(gameObject,markerId);views=GetComponents<Stage59ActorView>();mouth=GetComponent<CampDepartureMouth>();}
}
