using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
/// <summary>Temporary attached wall mouth. CampRunPortal remains the departure authority.</summary>
[DefaultExecutionOrder(2600)]public sealed class CampDepartureMouth:MonoBehaviour
{
 SettlementCampRegion region;GameObject visual;Mesh mesh;Material material;
 readonly Vector3[] vertices=new Vector3[21];readonly Vector2[] shape=new Vector2[21];
 public Vector3 ProjectedPromptAnchor=>mesh!=null&&visual!=null?visual.transform.TransformPoint(new Vector3(mesh.bounds.center.x,mesh.bounds.max.y,0)):Vector3.zero;
 public void Initialize(SettlementCampRegion owner)
 {
  region=owner;visual=new GameObject("Attached cave departure mouth");visual.transform.SetParent(region.transform,false);mesh=new Mesh{name="Temporary cave mouth and threshold"};mesh.MarkDynamic();visual.AddComponent<MeshFilter>().sharedMesh=mesh;material=new Material(region.ActorShader);var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=58;
  var outer=new[]{new Vector2(-3,0),new Vector2(-3,2.4f),new Vector2(-2.4f,3.8f),new Vector2(-1,4.4f),new Vector2(1,4.4f),new Vector2(2.4f,3.8f),new Vector2(3,2.4f),new Vector2(3,0)};
  var inner=new[]{new Vector2(-2.4f,.25f),new Vector2(-2.4f,2.35f),new Vector2(-1.95f,3.2f),new Vector2(-.8f,3.7f),new Vector2(.8f,3.7f),new Vector2(1.95f,3.2f),new Vector2(2.4f,2.35f),new Vector2(2.4f,.25f)};
  var colors=new Color[21];var triangles=new System.Collections.Generic.List<int>();Color Linear(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
  for(int i=0;i<8;i++)
  {
   shape[i]=outer[i];shape[i+8]=inner[i];colors[i]=Linear(new Color(.19f,.20f,.16f));colors[i+8]=Linear(new Color(.012f,.018f,.014f));int j=(i+1)%8;
   triangles.AddRange(new[]{i,j,j+8,i,j+8,i+8});triangles.AddRange(new[]{16,i+8,j+8});
  }
  shape[16]=new Vector2(0,1.7f);colors[16]=Linear(new Color(.008f,.012f,.009f));shape[17]=new Vector2(-2.5f,0);shape[18]=new Vector2(2.5f,0);shape[19]=new Vector2(2.5f,.25f);shape[20]=new Vector2(-2.5f,.25f);
  for(int i=17;i<21;i++)colors[i]=Linear(new Color(.31f,.29f,.23f));triangles.AddRange(new[]{17,19,18,17,20,19});mesh.vertices=vertices;mesh.colors=colors;mesh.SetTriangles(triangles,0);
 }
 void LateUpdate()
 {
  if(region==null||region.Projection==null)return;var anchor=region.Definition.Marker("run.departure").Anchor;var contact=new Point2(anchor.X,anchor.Y+3.2);var scale=Stage4CameraPreview.ActorScale(region.Projection,contact,region.PresentationSettings.actorMode);
  for(int i=0;i<vertices.Length;i++)vertices[i]=region.Presentation.Project(new Point2(contact.X+shape[i].x,contact.Y))+Vector3.up*(shape[i].y*scale.y);mesh.vertices=vertices;mesh.RecalculateBounds();
 }
 void OnDestroy(){foreach(var o in new Object[]{mesh,material,visual})if(o!=null)Destroy(o);}
}
