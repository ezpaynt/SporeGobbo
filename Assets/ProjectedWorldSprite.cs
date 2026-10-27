using UnityEngine;
using SporeGobbo.Geometry.Preview;
/// <summary>Display child only; the parent activity/interaction anchor stays in logical physics space.</summary>
[DefaultExecutionOrder(2400)]public sealed class ProjectedWorldSprite:MonoBehaviour
{
 IProjectedActorContext context;Transform logicalAnchor;Vector3 scale;
 public void Initialize(IProjectedActorContext owner,Transform anchor){context=owner;logicalAnchor=anchor;scale=transform.lossyScale;}
 void LateUpdate(){if(context==null||logicalAnchor==null||context.Projection==null)return;var logical=context.LogicalPosition(logicalAnchor.position);var projected=context.Render(logicalAnchor.position);var factor=Stage4CameraPreview.ActorScale(context.Projection,logical,context.PresentationSettings.actorMode);transform.position=projected;transform.localScale=new Vector3(scale.x*factor.x/.6f,scale.y*factor.y/.6f,scale.z);}
}
