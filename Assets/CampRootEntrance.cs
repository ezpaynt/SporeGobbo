using UnityEngine;
using System;
using System.Collections.Generic;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
/// <summary>Optional authored root. Actual Camp routine and leased terrain transaction own assistance/opening.</summary>
[DefaultExecutionOrder(2700)]public sealed class CampRootEntrance:MonoBehaviour,ICampInteractable,IWorldInteractionMetadata,IProjectedInteractionAnchor
{
 SettlementCampRegion region;GameObject visual;Mesh mesh;Material material;
 Point2[] logical;Vector3[] projected;
 public Vector3 ProjectedPromptAnchor=>mesh!=null&&visual!=null?visual.transform.TransformPoint(new Vector3(mesh.bounds.center.x,mesh.bounds.max.y,0)):Vector3.zero;
 public Vector2 GetInteractionPoint()=>transform.position;
 public float InteractionRange=>1.8f;public int InteractionPriority=>6;
 public string GetInteractPrompt()=>"Break roots with Buddy";
 public bool CanInteract(GobboController player)=>player!=null&&CampSpatialTerrain.Active!=null&&!CampSpatialTerrain.Active.ResidentialEntranceOpen;
 public void Interact(GobboController player)
 {
  if(!CanInteract(player))return;
  if(CampSpatialTerrain.Active.TryOpenResidentialEntrance(player))return;
  CampStartRoutineManager.Instance?.RequestResidentialEntranceAssistance();
 }
 void Start()
 {
  region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();if(region==null)return;
  visual=new GameObject("Optional pocket attached root bands");visual.transform.SetParent(region.transform,false);mesh=new Mesh{name="Authored optional root bands"};mesh.MarkDynamic();visual.AddComponent<MeshFilter>().sharedMesh=mesh;material=new Material(region.ActorShader);var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=62;
  var points=new List<Point2>();var colors=new List<Color>();var indices=new List<int>();int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue;
  foreach(var c in SettlementCampLayout.RootCells()){minX=Math.Min(minX,c.X);maxX=Math.Max(maxX,c.X);minY=Math.Min(minY,c.Y);}
  void Vertex(double x,double y,Color color){points.Add(new Point2(x,y));colors.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);}
  void Ribbon(double y,double phase,double width,Color color)
  {
   for(int i=0;i<12;i++)
   {
    double a=i/12.0,b=(i+1)/12.0,x0=minX-.3+a*(maxX-minX+1.6),x1=minX-.3+b*(maxX-minX+1.6),y0=y+Math.Sin(a*Math.PI*2+phase)*.28,y1=y+Math.Sin(b*Math.PI*2+phase)*.28;int n=points.Count;
    Vertex(x0,y0-width*.5,color);Vertex(x1,y1-width*.5,color);Vertex(x1,y1+width*.5,color);Vertex(x0,y0+width*.5,color);
    indices.Add(n);indices.Add(n+2);indices.Add(n+1);indices.Add(n);indices.Add(n+3);indices.Add(n+2);
   }
  }
  for(int i=0;i<6;i++){Ribbon(minY+.2+i*.54,i*.9,.68,new Color(.14f,.106f,.106f));Ribbon(minY+.15+i*.54,i*.9,.43,new Color(.286f,.212f,.184f));Ribbon(minY+.05+i*.54,i*.9,.10,new Color(.376f,.29f,.239f));}
  logical=points.ToArray();projected=new Vector3[logical.Length];mesh.vertices=projected;mesh.SetColors(colors);mesh.SetTriangles(indices,0);
 }
 void LateUpdate()
 {
  if(region==null||region.Projection==null||visual==null)return;visual.SetActive(!region.ResidentialEntranceOpen);if(region.ResidentialEntranceOpen)return;
  for(int i=0;i<logical.Length;i++)projected[i]=region.Presentation.Project(logical[i]);mesh.vertices=projected;mesh.RecalculateBounds();
 }
 void OnDestroy(){foreach(var o in new UnityEngine.Object[]{mesh,material,visual})if(o!=null)Destroy(o);}
}
