using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
/// <summary>Visual-only actor contract shared by regions and retained preview comparison fixtures.</summary>
public interface IProjectedActorContext
{
 Transform PresentationRoot{get;}
 Shader ActorShader{get;}
 FocusProjection Projection{get;}
 Stage4Settings PresentationSettings{get;}
 bool PresentationReady{get;}
 Point2 LogicalPosition(Vector2 physical);
 Vector3 Render(Vector2 physical);
}
public interface IRegionActorBinding{void BindActor(GameObject actor);}
public interface IRegionVisualBinding{void BindStaticVisual(GameObject visual,Transform logicalAnchor);}
