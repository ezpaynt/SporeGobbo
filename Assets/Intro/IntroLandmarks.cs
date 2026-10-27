using System;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Geometry.Preview;
/// <summary>Three attached authored forms over retained elevated geometry; terrain masks own physical structure.</summary>
[DefaultExecutionOrder(2450)]public sealed class IntroLandmarks:MonoBehaviour
{
 IntroRegion region;Material material;Stage55Palette palette;int revision=-1;
 sealed class Form{public ElevatedPath Path;public DimensionalLandmark Land;public PreviewMatter Matter;public ElevatedMesh Data=new ElevatedMesh();public Mesh Mesh;public MeshRenderer Renderer;public Point2 Contact;}
 readonly List<Form> forms=new List<Form>();
 public void Initialize(IntroRegion r,Stage55Palette p)
 {
  region=r;palette=p;material=new Material(r.landmarkShader);material.color=Color.white;
  Add(Shoulder("intro.stone-shoulder",new Point2(48.5,34.5),2.6,4.4),PreviewMatter.Stone,48,34,2,2);
  Add(ElevatedShapes.Path("intro.attached-root",new[]{new Point2(80.5,48.5),new Point2(81.5,47.5),new Point2(83.5,48.5)},new[]{0d,2.8,0},new[]{.7,1.3,.7}),PreviewMatter.Root,82,48,2,1);
  Add(Shoulder("intro.steady-stone",new Point2(131.5,19.5),3.2,3.8),PreviewMatter.Stone,131,19,3,2);Add(Shoulder("intro.threshold-upper",new Point2(147.5,20.5),1.4,2.8),PreviewMatter.Stone,147,20,1,2);Add(Shoulder("intro.threshold-lower",new Point2(147.5,30.5),1.4,2.2),PreviewMatter.Stone,147,30,1,2);Rebuild();
 }
 static ElevatedPath Shoulder(string id,Point2 p,double width,double height){double[] heights={0,.12,.36,.64,.86,1},radii={.9,1,.94,.82,.64,.58};var sections=new ElevatedSection[heights.Length];for(int i=0;i<sections.Length;i++)sections[i]=new ElevatedSection(new Point2(p.X+(i%2==0?-.12:.15),p.Y),height*heights[i],width*radii[i],new Point2(1,0),.25);return new ElevatedPath(id,sections);}
 void Add(ElevatedPath path,PreviewMatter matter,int x,int y,int rx,int ry){var cells=new List<DualTile>();for(int yy=y-ry;yy<=y+ry;yy++)for(int xx=x-rx;xx<=x+rx;xx++)if(region.Definition.Protected(xx,yy))cells.Add(new DualTile(xx,yy));var go=new GameObject(path.Id);go.transform.SetParent(transform,false);var mesh=new Mesh{name=path.Id};go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=18;forms.Add(new Form{Path=path,Matter=matter,Land=new DimensionalLandmark(path.Id,cells.ToArray(),cells.ToArray(),new StructurePart[0]),Mesh=mesh,Renderer=renderer,Contact=RegionLandmarkDepth.ContactAnchor(cells,path)});}
 void Rebuild(){foreach(var f in forms){f.Data.Build(f.Path,palette,f.Matter,region.State.World,f.Land,true);f.Data.Upload(f.Mesh,new Point2(IntroLayout.Width*.5,IntroLayout.Height*.5));}revision=region.Revision;}
 void LateUpdate(){if(region?.Projection==null)return;if(revision!=region.Revision)Rebuild();foreach(var f in forms){f.Renderer.enabled=region.PresentationReady;var b=f.Data.Bounds(region.Projection);f.Mesh.bounds=new Bounds(new Vector3((float)((b.Min.X+b.Max.X)*.5),(float)(-(b.Min.Y+b.Max.Y)*.5)),new Vector3((float)(b.Max.X-b.Min.X),(float)(b.Max.Y-b.Min.Y),.1f));f.Renderer.sortingOrder=RegionLandmarkDepth.Resolve(region.Views,region.World(f.Contact).y,RegionLandmarkDepth.Treatment.Grounded);}}
 public bool TryGetDepthAnchor(string id,out Vector2 world,out bool overhead,out int sortingOrder){foreach(var f in forms)if(f.Path.Id==id){world=region.World(f.Contact);overhead=false;sortingOrder=f.Renderer.sortingOrder;return true;}world=default;overhead=false;sortingOrder=0;return false;}
 void OnDestroy(){foreach(var f in forms)if(f.Mesh!=null)Destroy(f.Mesh);if(material!=null)Destroy(material);}
}
