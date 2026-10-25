using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry.Preview
{
 /// <summary>Intersection with the existing canonical cell-fill triangles. Presentation only; never writes terrain.</summary>
 public static class StructureTerrainClip
 {
  sealed class Mask:IOccupancySource{readonly StreamingWorld world;readonly DimensionalLandmark land;public Mask(StreamingWorld w,DimensionalLandmark l){world=w;land=l;}public int Width=>world.Width;public int Height=>world.Height;public bool IsSolid(int x,int y)=>!world.IsSolid(x,y)||land.Owns(x,y);}
  public static void Build(PreviewMeshData source,PreviewMeshData output,StreamingWorld world,DimensionalLandmark land)
  {
   output.Clear();var mask=new Mask(world,land);var tiles=new Dictionary<DualTile,PreviewMeshData>();var poly=new List<Point2>(10);var next=new List<Point2>(10);
   for(int i=0;i<source.Indices.Count;i+=3){var a=source.LogicalVertices[source.Indices[i]].Logical;var b=source.LogicalVertices[source.Indices[i+1]].Logical;var c=source.LogicalVertices[source.Indices[i+2]].Logical;
    int x0=(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X))-.5),x1=(int)Math.Floor(Math.Max(a.X,Math.Max(b.X,c.X))-.5);int y0=(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))-.5),y1=(int)Math.Floor(Math.Max(a.Y,Math.Max(b.Y,c.Y))-.5);
    bool entirelyOpen=true;for(int yy=y0;yy<=y1+1&&entirelyOpen;yy++)for(int xx=x0;xx<=x1+1;xx++)if(!mask.IsSolid(xx,yy)){entirelyOpen=false;break;}if(entirelyOpen){output.Triangle(a,b,c);continue;}
    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var key=new DualTile(x,y);if(!tiles.TryGetValue(key,out var tile)){tile=new PreviewMeshData("mask",PreviewLayer.Solid);PreviewMeshBuilder.EmitCellSolid(mask,x,y,tile);tiles.Add(key,tile);}for(int j=0;j<tile.Indices.Count;j+=3){poly.Clear();poly.Add(a);poly.Add(b);poly.Add(c);for(int e=0;e<3&&poly.Count>0;e++){var v=tile.LogicalVertices[tile.Indices[j+e]].Logical;var z=tile.LogicalVertices[tile.Indices[j+(e+1)%3]].Logical;next.Clear();for(int k=0;k<poly.Count;k++){var p=poly[k];var q=poly[(k+1)%poly.Count];double cp=PreviewMeshData.Cross(v,z,p),cq=PreviewMeshData.Cross(v,z,q);bool ip=cp>=-1e-9,iq=cq>=-1e-9;if(ip)next.Add(p);if(ip!=iq){double t=cp/(cp-cq);next.Add(p+(q-p)*t);}}var swap=poly;poly=next;next=swap;}for(int k=1;k+1<poly.Count;k++)output.Triangle(poly[0],poly[k],poly[k+1]);}}
   }
  }
 }
}
