using System;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Geometry.Preview;
/// <summary>Bounded authored elevated paths using retained geometry kernels; no physical truth in render meshes.</summary>
[DefaultExecutionOrder(2450)]public sealed class ExpeditionLandmarks:MonoBehaviour
{
 ExpeditionRegion region;Stage55Palette palette;Material material;
 sealed class Batch{public ElevatedPath Path;public DimensionalLandmark Land;public PreviewMatter Matter;public ElevatedMesh Data=new ElevatedMesh();public Mesh Mesh;public MeshRenderer Renderer;public bool Dirty=true;}
 readonly List<Batch> batches=new List<Batch>();int revision=-1;public int Triangles{get;private set;}public double UpdateMs{get;private set;}
 public void Initialize(ExpeditionRegion region,Stage55Palette palette)
 {
  this.region=region;this.palette=palette;if(region.landmarkShader==null)throw new InvalidOperationException("Missing retained elevation shader");material=new Material(region.landmarkShader);material.color=Color.white;
  Add(ElevatedShapes.Spike(new Point2(43.5,78.5),3.4,5,LandmarkProfile.BroadShoulder),"stone.shoulder",PreviewMatter.Stone);
  Add(ElevatedShapes.Spike(new Point2(108.5,58.5),2.7,6,LandmarkProfile.Segmented),"stone.pillar",PreviewMatter.Stone);
  Add(ElevatedShapes.Spike(new Point2(96.5,96.5),4.1,3.5,LandmarkProfile.Cluster),"stone.shelf",PreviewMatter.Stone);
  var root=ElevatedShapes.Path("root.arch",new[]{new Point2(72.5,65.5),new Point2(74,66),new Point2(78,66.5),new Point2(81,66),new Point2(83.5,63.5)},new[]{0d,4,7,4,0},new[]{2d,1.8,1.4,1.8,2});Add(root,"root.left",PreviewMatter.Root,"root.right");
  Add(ElevatedShapes.Path("root.fork",new[]{new Point2(78,66.5),new Point2(81,61),new Point2(83.5,63.5)},new[]{7d,2,0},new[]{1.4,1.2,2},shared:root.Sections[8]),"root.right",PreviewMatter.Root);
  Add(ElevatedShapes.Path("root.tail",new[]{new Point2(67,81),new Point2(69,83),new Point2(73,84),new Point2(75,82)},new[]{0d,1.6,.6,0},new[]{.4,1.5,1.2,.2}),"root.tail",PreviewMatter.Root);
  // Embedded arched fossil, with several actual ribs, not a renamed spike.
  Add(ElevatedShapes.Path("bone.spine",new[]{new Point2(100.5,103.5),new Point2(104,101),new Point2(109,101),new Point2(113.5,103.5)},new[]{0d,4,4,0},new[]{1.5,.8,.8,1.5}),"bone.left",PreviewMatter.Bone,"bone.right");
  for(int i=0;i<3;i++){double x=104+i*2;Add(ElevatedShapes.Path("bone.rib."+i,new[]{new Point2(x,103),new Point2(x,101),new Point2(x,99)},new[]{0d,4,0},new[]{.6,.55,.45}),"bone.left",PreviewMatter.Bone,"bone.right");}
  region.State.Committed+=Changed;Rebuild();
 }
 void Add(ElevatedPath path,string footprint,PreviewMatter matter,string second=null){var cells=new List<DualTile>(region.Definition.Footprints[footprint]);if(second!=null)cells.AddRange(region.Definition.Footprints[second]);var land=new DimensionalLandmark(path.Id,cells.ToArray(),cells.ToArray(),new StructurePart[0]);var go=new GameObject(path.Id);go.transform.SetParent(transform,false);var mesh=new Mesh{name=path.Id};go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=path.Overhead?40:18;batches.Add(new Batch{Path=path,Land=land,Matter=matter,Mesh=mesh,Renderer=renderer});}
 void Rebuild(){Triangles=0;foreach(var b in batches){if(b.Dirty){b.Data.Build(b.Path,palette,b.Matter,region.State.World,b.Land,true);b.Data.Upload(b.Mesh,new Point2(region.State.World.Width*.5,region.State.World.Height*.5));b.Dirty=false;}Triangles+=b.Data.Indices.Count/3;}revision=region.Revision;}
 void Changed(IReadOnlyList<DualTile> cells,int rev){foreach(var b in batches)foreach(var c in cells){foreach(var section in b.Path.Sections)if(Math.Abs(section.Left.Ground.X-c.X)<6&&Math.Abs(section.Left.Ground.Y-c.Y)<6||Math.Abs(section.Right.Ground.X-c.X)<6&&Math.Abs(section.Right.Ground.Y-c.Y)<6){b.Dirty=true;break;}if(b.Dirty)break;}}
 void LateUpdate(){using var profile=new Unity.Profiling.ProfilerMarker("Spore.Expedition.Landmarks").Auto();if(region?.Projection==null)return;long start=System.Diagnostics.Stopwatch.GetTimestamp();if(revision!=region.Revision)Rebuild();foreach(var b in batches){bool known=false;foreach(var c in b.Land.Footprint)if(region.Discovery.Shown(c.X,c.Y)){known=true;break;}b.Renderer.enabled=known&&region.PresentationReady;if(!b.Renderer.enabled)continue;var bounds=b.Data.Bounds(region.Projection);b.Mesh.bounds=new Bounds(new Vector3((float)((bounds.Min.X+bounds.Max.X)*.5),(float)(-(bounds.Min.Y+bounds.Max.Y)*.5)),new Vector3((float)(bounds.Max.X-bounds.Min.X),(float)(bounds.Max.Y-bounds.Min.Y),.1f));int north=0;var section=b.Path.Sections[0];float baseY=region.World((section.Left.Ground+section.Right.Ground)*.5).y;foreach(var view in region.PresentedViews)if(view!=null&&view.PhysicalFoot.y>baseY)north++;b.Renderer.sortingOrder=b.Path.Overhead?110:19+2*north;}UpdateMs=StreamingGeometry.Elapsed(start);}
 void OnDestroy(){if(region?.State!=null)region.State.Committed-=Changed;foreach(var b in batches){if(b.Mesh!=null)Destroy(b.Mesh);if(b.Renderer!=null)Destroy(b.Renderer.gameObject);}if(material!=null)Destroy(material);}
}
