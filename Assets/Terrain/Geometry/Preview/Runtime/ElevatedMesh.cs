using System;
using System.Collections.Generic;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 public sealed class ElevatedMesh
 {
  public readonly List<ElevatedVertex> Vertices=new List<ElevatedVertex>();public readonly List<int> Indices=new List<int>();public readonly List<Color> Colors=new List<Color>();
  double minX,minY,maxX,maxY,maxHeight;
  public void Clear(){Vertices.Clear();Indices.Clear();Colors.Clear();minX=minY=double.MaxValue;maxX=maxY=double.MinValue;maxHeight=0;}
  public ElevatedMesh(){Clear();}
  void Include(ElevatedVertex v){minX=Math.Min(minX,v.Ground.X);minY=Math.Min(minY,v.Ground.Y);maxX=Math.Max(maxX,v.Ground.X);maxY=Math.Max(maxY,v.Ground.Y);maxHeight=Math.Max(maxHeight,v.Height);}
  public void Triangle(ElevatedVertex a,ElevatedVertex b,ElevatedVertex c,Color color){var ab=new Vector3((float)(b.Ground.X-a.Ground.X),(float)(b.Ground.Y-a.Ground.Y),(float)(b.Height-a.Height));var ac=new Vector3((float)(c.Ground.X-a.Ground.X),(float)(c.Ground.Y-a.Ground.Y),(float)(c.Height-a.Height));if(Vector3.Cross(ab,ac).sqrMagnitude<1e-16f)return;int n=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Include(a);Include(b);Include(c);Indices.Add(n);Indices.Add(n+1);Indices.Add(n+2);Colors.Add(color);Colors.Add(color);Colors.Add(color);}
  // Monotonic terrain mapping plus a nonnegative height interval gives an O(1)
  // conservative bound. No CPU projection of the entire mesh during camera movement.
  public ProjectionBounds Bounds(FocusProjection map,bool legacy=false){if(Vertices.Count==0)return new ProjectionBounds(default,default);var a=map.Project(new Point2(minX,minY-(legacy?maxHeight:0)));var b=map.Project(new Point2(maxX,maxY));double scaleX=Math.Max(1,map.Side),scaleY=Math.Max(1,Math.Max(map.Top,map.Foreground));return new ProjectionBounds(new Point2(a.X,a.Y-(legacy?0:maxHeight*Math.Sqrt(scaleX*scaleY))),b);}
  public void Upload(Mesh mesh,Point2 origin=default){var p=new List<Vector3>(Vertices.Count);var h=new List<Vector2>(Vertices.Count);foreach(var v in Vertices){p.Add(new Vector3((float)(v.Ground.X-origin.X),(float)(v.Ground.Y-origin.Y)));h.Add(new Vector2((float)v.Height,0));}mesh.Clear();mesh.indexFormat=Vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;mesh.SetVertices(p);mesh.SetUVs(0,h);mesh.SetColors(Colors);mesh.SetTriangles(Indices,0,false);}
  public static Color VertexColor(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
  public void Build(ElevatedPath path,Stage55Palette palette,PreviewMatter matter,StreamingWorld world,DimensionalLandmark land,bool clipLow=true)
  {
   Clear();var clipMask=clipLow?new Mask(world,land):null;var clipCache=new Dictionary<DualTile,Point2[]>();string light=matter==PreviewMatter.Root?"RootLight":matter==PreviewMatter.Bone?"BoneLimestone":"StoneLight",basis=palette.Matter(matter),shade=matter==PreviewMatter.Root?"RootShadow":matter==PreviewMatter.Bone?"BoneShadow":"StoneShadow";
   var tones=new[]{light,basis,shade,matter==PreviewMatter.Stone?"StoneDeep":matter==PreviewMatter.Root?"InkBlack":"BoneShadow"};double[] bands={0,.24,.65,.9,1};
   for(int i=0;i<path.Sections.Length-1;i++){var a=path.Sections[i];var b=path.Sections[i+1];for(int band=0;band<4;band++){var al=ElevatedVertex.Lerp(a.Left,a.Right,bands[band]);var ar=ElevatedVertex.Lerp(a.Left,a.Right,bands[band+1]);var bl=ElevatedVertex.Lerp(b.Left,b.Right,bands[band]);var br=ElevatedVertex.Lerp(b.Left,b.Right,bands[band+1]);var color=VertexColor(palette.Color(tones[band]));Emit(al,bl,br,color);Emit(al,br,ar,color);}
   }
   void Emit(ElevatedVertex a,ElevatedVertex b,ElevatedVertex c,Color color){if(!clipLow||Math.Max(a.Height,Math.Max(b.Height,c.Height))>4){Triangle(a,b,c,color);return;}Clip(a,b,c,color,clipMask,clipCache);}
  }
  sealed class Mask:IOccupancySource{readonly StreamingWorld w;readonly HashSet<int> owned=new HashSet<int>();public int Width=>w.Width;public int Height=>w.Height;public Mask(StreamingWorld w,DimensionalLandmark l){this.w=w;foreach(var c in l.Footprint)owned.Add(c.Y*w.Width+c.X);}public bool IsSolid(int x,int y)=>!w.IsSolid(x,y)||owned.Contains(y*w.Width+x);}
  // Canonical MS triangles clip the ground cross-section; interpolated height stays attached.
  void Clip(ElevatedVertex a,ElevatedVertex b,ElevatedVertex c,Color color,Mask mask,Dictionary<DualTile,Point2[]> cache)
  {
   if(Math.Abs(PreviewMeshData.Cross(a.Ground,b.Ground,c.Ground))<1e-10){Triangle(a,b,c,color);return;}
   int x0=(int)Math.Floor(Math.Min(a.Ground.X,Math.Min(b.Ground.X,c.Ground.X)))-1,x1=(int)Math.Ceiling(Math.Max(a.Ground.X,Math.Max(b.Ground.X,c.Ground.X))),y0=(int)Math.Floor(Math.Min(a.Ground.Y,Math.Min(b.Ground.Y,c.Ground.Y)))-1,y1=(int)Math.Ceiling(Math.Max(a.Ground.Y,Math.Max(b.Ground.Y,c.Ground.Y)));
   for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var key=new DualTile(x,y);if(!cache.TryGetValue(key,out var triangles)){var tile=new PreviewMeshData("clip",PreviewLayer.Solid);PreviewMeshBuilder.EmitCellSolid(mask,x,y,tile);triangles=new Point2[tile.Indices.Count];for(int j=0;j<triangles.Length;j++)triangles[j]=tile.LogicalVertices[tile.Indices[j]].Logical;cache.Add(key,triangles);}for(int k=0;k<triangles.Length;k+=3){var poly=new List<ElevatedVertex>{a,b,c};var v0=triangles[k];var v1=triangles[k+1];var v2=triangles[k+2];double sign=Math.Sign(PreviewMeshData.Cross(v0,v1,v2));var edges=new[]{v0,v1,v2};for(int e=0;e<3&&poly.Count>0;e++){var next=new List<ElevatedVertex>();var left=edges[e];var right=edges[(e+1)%3];var last=poly[poly.Count-1];double dl=sign*PreviewMeshData.Cross(left,right,last.Ground);foreach(var cur in poly){double dc=sign*PreviewMeshData.Cross(left,right,cur.Ground);if((dl>=-1e-9)!=(dc>=-1e-9))next.Add(ElevatedVertex.Lerp(last,cur,Math.Max(0,Math.Min(1,dl/(dl-dc)))));if(dc>=-1e-9)next.Add(cur);last=cur;dl=dc;}poly=next;}for(int j=1;j+1<poly.Count;j++)if(Math.Abs(PreviewMeshData.Cross(poly[0].Ground,poly[j].Ground,poly[j+1].Ground))>1e-10)Triangle(poly[0],poly[j],poly[j+1],color);}}
  }
 }
}
