using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Immutable, preview-only supplied fixture. Never registered as gameplay terrain.</summary>
    public sealed class PreviewFixture : IOccupancySource
    {
        readonly string[] rows;
        public int Width => rows[0].Length;
        public int Height => rows.Length;
        public PreviewFixture(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            rows = text.Replace("\r", "").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length < 2 || rows[0].Length < 2 || rows.Any(r => r.Length != rows[0].Length || r.Any(c => c != '0' && c != '1')))
                throw new ArgumentException("Expected a rectangular binary occupancy fixture.", nameof(text));
        }
        public bool IsSolid(int column, int row) => rows[row][column] == '1';
    }

    public enum PreviewLayer { Floor, Solid, Faces, Lips, Contours, Grid, Gobbo }

    /// <summary>One batched layer/patch, using Y-up render coordinates and clockwise triangles (-Z normals).
    /// Degenerate triangles are discarded explicitly, never hidden by double-sided rendering.</summary>
    public sealed class PreviewMeshData
    {
        public readonly string Name;
        public readonly PreviewLayer Layer;
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Indices = new List<int>();
        public readonly List<LogicalMeshVertex> LogicalVertices=new List<LogicalMeshVertex>();
        public readonly List<WallSample> WallSamples=new List<WallSample>();
        public Point2? VisualActorAnchor;
        public Texture PreviewTexture;
        public readonly List<Vector2> TextureCoordinates=new List<Vector2>();
        readonly List<Point2> polygonWork=new List<Point2>();
        GeometryVertexRole DefaultRole=>Layer==PreviewLayer.Contours?GeometryVertexRole.Contour:Layer==PreviewLayer.Grid?GeometryVertexRole.Grid:Layer==PreviewLayer.Gobbo?GeometryVertexRole.Actor:GeometryVertexRole.Fill;
        public void Clear(){Vertices.Clear();Indices.Clear();LogicalVertices.Clear();WallSamples.Clear();TextureCoordinates.Clear();VisualActorAnchor=null;PreviewTexture=null;}
        public int RegisterWallSamples(IReadOnlyList<WallSample> samples)
        {int first=WallSamples.Count;for(int i=0;i<samples.Count;i++)WallSamples.Add(samples[i]);return first;}
        public Point2 ResolveVertex(int index,ICartesianProjection projection,WallHeightPolicy policy=WallHeightPolicy.ReferenceLocalScale)
        {
            var vertex=LogicalVertices[index];
            if(vertex.WallSampleIndex<0)return projection.Project(vertex.Logical);
            var wall=WallGeometry.Project(WallSamples[vertex.WallSampleIndex],projection,WallSettings.Reference,policy);
            return vertex.Role==GeometryVertexRole.WallBottom?wall.Bottom:vertex.Role==GeometryVertexRole.ForegroundLip?wall.Lip:wall.Top;
        }
        public PreviewMeshData(string name, PreviewLayer layer) { Name = name; Layer = layer; }
        public void Triangle(Point2 a, Point2 b, Point2 c)
            => Triangle(a,b,c,new LogicalMeshVertex(a,DefaultRole),new LogicalMeshVertex(b,DefaultRole),new LogicalMeshVertex(c,DefaultRole));
        void Triangle(Point2 a,Point2 b,Point2 c,LogicalMeshVertex la,LogicalMeshVertex lb,LogicalMeshVertex lc)
        {
            double area = Cross(a, b, c);
            if (Math.Abs(area) < 1e-11) return;
            Vector3 va=RenderPoint(a),vb=RenderPoint(b),vc=RenderPoint(c);
            float renderedArea=Vector3.Cross(vb-va,vc-va).z;
            if(Math.Abs(renderedArea)<=1e-10) return;
            int start = Vertices.Count;
            Vertices.Add(va); Vertices.Add(vb); Vertices.Add(vc);
            LogicalVertices.Add(la);LogicalVertices.Add(lb);LogicalVertices.Add(lc);
            // Logical Y-down is reflected into render Y-up, so positive logical area faces the camera.
            Indices.Add(start); Indices.Add(start + (renderedArea < 0 ? 1 : 2)); Indices.Add(start + (renderedArea < 0 ? 2 : 1));
        }
        public void Quad(Point2 a, Point2 b, Point2 c, Point2 d) { Triangle(a,b,c); Triangle(a,c,d); }
        public void AttachedQuad(WallDisplayVertex a,WallDisplayVertex b,int ia,int ib,bool foreground)
        {
            var topA=new LogicalMeshVertex(WallSamples[ia].Logical,GeometryVertexRole.WallTop,ia);
            var topB=new LogicalMeshVertex(WallSamples[ib].Logical,GeometryVertexRole.WallTop,ib);
            var role=foreground?GeometryVertexRole.ForegroundLip:GeometryVertexRole.WallBottom;
            var endA=new LogicalMeshVertex(WallSamples[ia].Logical,role,ia);
            var endB=new LogicalMeshVertex(WallSamples[ib].Logical,role,ib);
            Point2 pa=foreground?a.Lip:a.Bottom,pb=foreground?b.Lip:b.Bottom;
            Triangle(a.Top,b.Top,pb,topA,topB,endB);Triangle(a.Top,pb,pa,topA,endB,endA);
        }
        public void Rectangle(double x0, double y0, double x1, double y1)
            => Quad(new Point2(x0,y0), new Point2(x1,y0), new Point2(x1,y1), new Point2(x0,y1));
        public static Vector3 RenderPoint(Point2 p) => new Vector3((float)p.X, (float)-p.Y, 0);
        public static double Cross(Point2 a, Point2 b, Point2 c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        public Mesh CreateMesh()
        {
            var mesh = new Mesh { name = Name, indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            ApplyTo(mesh);
            return mesh;
        }
        public void ApplyTo(Mesh mesh)
        {
            using(GeometryTrace.Measure(GeometryPhase.MeshUpload))
            {
            mesh.Clear();mesh.indexFormat=Vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(Vertices); mesh.SetTriangles(Indices,0,false); using(GeometryTrace.Measure(GeometryPhase.Bounds)){mesh.RecalculateBounds();}
            // Bounds center/extents are floats: outward padding avoids excluding an exact edge vertex
            // after center +/- extent rounding, without moving any geometry.
            Bounds bounds=mesh.bounds;bounds.Expand(.0001f);mesh.bounds=bounds;
        
            }
        }
        internal void Polygon(List<Point2> polygon)
        {
            using(GeometryTrace.Measure(GeometryPhase.Triangulation))
            {
            if (polygon.Count < 3) return;
            double area = 0;
            for (int i=0;i<polygon.Count;i++) { Point2 a=polygon[i],b=polygon[(i+1)%polygon.Count];area+=a.X*b.Y-b.X*a.Y; }
            var remaining = polygonWork;remaining.Clear();remaining.AddRange(polygon);
            if (area < 0) remaining.Reverse();
            while (remaining.Count > 3)
            {
                bool clipped = false;
                for (int i=0;i<remaining.Count;i++)
                {
                    Point2 a=remaining[(i+remaining.Count-1)%remaining.Count], b=remaining[i], c=remaining[(i+1)%remaining.Count];
                    double cross=Cross(a,b,c);
                    if (Math.Abs(cross)<1e-11) { remaining.RemoveAt(i);clipped=true;break; }
                    if (cross<0) continue;
                    bool contains=false;
                    for(int j=0;j<remaining.Count;j++)
                    {
                        if(j==i||j==(i+1)%remaining.Count||j==(i+remaining.Count-1)%remaining.Count)continue;
                        Point2 p=remaining[j];
                        if(Cross(a,b,p)>=-1e-11&&Cross(b,c,p)>=-1e-11&&Cross(c,a,p)>=-1e-11){contains=true;break;}
                    }
                    if(contains)continue;
                    Triangle(a,b,c);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new InvalidOperationException("Contour fill is not a simple triangulatable polygon.");
            }
            if(remaining.Count==3)Triangle(remaining[0],remaining[1],remaining[2]);
        
            }
        }
    }

    /// <summary>Uniform-grid renderer adapter. All curves and wall vertices come from Stage 1.
    /// No projection warp, terrain edits, physics, saves or MapGenerator dependencies.</summary>
    public sealed class PreviewMeshWorkspace
    {
        internal readonly Point2[] Left=new Point2[ContourGeometry.Subdivisions+1],Right=new Point2[ContourGeometry.Subdivisions+1];
        internal readonly Point2[] Top=new Point2[ContourGeometry.Subdivisions+1];
        internal readonly WallDisplayVertex[] Walls=new WallDisplayVertex[ContourGeometry.Subdivisions+1];
    }

    public static class PreviewMeshBuilder
    {
        public const int PatchSize = 16;
        public const double ContourWidth = .075;
        public static List<PreviewMeshData> Build(IOccupancySource source)
        {
            var cache = new ContourTerrainCache(source);
            var workspace=new PreviewMeshWorkspace();
            var output = new List<PreviewMeshData>();
            var floor = new PreviewMeshData("Floor",PreviewLayer.Floor);
            floor.Rectangle(0,0,source.Width,source.Height);output.Add(floor);
            for(int py=0;py<source.Height-1;py+=PatchSize)for(int px=0;px<source.Width-1;px+=PatchSize)
                foreach(var mesh in BuildPatch(source,cache,px,py,PatchSize,null,workspace))if(mesh.Indices.Count>0)output.Add(mesh);
            var border=new PreviewMeshData("WorldEdgeFill",PreviewLayer.Solid);
            for(int x=0;x<source.Width;x++)
            {
                if(source.IsSolid(x,0))border.Rectangle(x,0,x+1,.5);
                if(source.IsSolid(x,source.Height-1))border.Rectangle(x,source.Height-.5,x+1,source.Height);
            }
            for(int y=0;y<source.Height;y++)
            {
                if(source.IsSolid(0,y))border.Rectangle(0,y,.5,y+1);
                if(source.IsSolid(source.Width-1,y))border.Rectangle(source.Width-.5,y,source.Width,y+1);
            }
            if(border.Indices.Count>0)output.Add(border);
            var grid=new PreviewMeshData("Grid",PreviewLayer.Grid);
            for(int x=0;x<=source.Width;x++)grid.Rectangle(x-.006,0,x+.006,source.Height);
            for(int y=0;y<=source.Height;y++)grid.Rectangle(0,y-.006,source.Width,y+.006);
            output.Add(grid);
            var gobbo=new PreviewMeshData("GobboPlaceholder",PreviewLayer.Gobbo);
            // One logical-cell-wide upright silhouette, not a gameplay actor.
            const double gx=50,gy=39;
            var head=new List<Point2>();for(int i=0;i<20;i++){double a=2*Math.PI*i/20;head.Add(new Point2(gx+Math.Cos(a)*.48,gy+Math.Sin(a)*.48));}
            gobbo.Polygon(head);gobbo.Triangle(new Point2(gx-.35,gy-.2),new Point2(gx-.8,gy-.5),new Point2(gx-.4,gy+.1));
            gobbo.Triangle(new Point2(gx+.35,gy-.2),new Point2(gx+.8,gy-.5),new Point2(gx+.4,gy+.1));output.Add(gobbo);
            return output;
        }

        /// <summary>Rebuild a single owned dual-grid patch. Neighbor samples always come from the global source.</summary>
        public static PreviewMeshData[] BuildPatch(IOccupancySource source,ContourTerrainCache cache,int px,int py,int patchSize,PreviewMeshData[] reuse=null,PreviewMeshWorkspace workspace=null) => BuildRegion(source,cache,px,py,patchSize,reuse,workspace,false,0,int.MaxValue,true,true);
        public static PreviewMeshData[] BuildOptimizedRows(IOccupancySource source,ContourTerrainCache cache,int px,int py,int patchSize,int firstRow,int rows,PreviewMeshData[] reuse=null,PreviewMeshWorkspace workspace=null,bool clear=true,bool mergeSolid=true)
            => BuildRegion(source,cache,px,py,patchSize,reuse,workspace,true,firstRow,rows,clear,mergeSolid);
        static PreviewMeshData[] BuildRegion(IOccupancySource source,ContourTerrainCache cache,int px,int py,int patchSize,PreviewMeshData[] reuse,PreviewMeshWorkspace workspace,bool optimized,int firstRow,int rows,bool clear,bool mergeSolid)
        {
            using(GeometryTrace.Measure(GeometryPhase.MeshBuild))
            {
            if(patchSize<=0||px<0||py<0||px>=source.Width-1||py>=source.Height-1)throw new ArgumentOutOfRangeException(nameof(patchSize));
            var projection=UniformGridProjection.Unit;
            workspace=workspace??new PreviewMeshWorkspace();
            var meshes=reuse??new[]{new PreviewMeshData($"Solid_{px}_{py}",PreviewLayer.Solid),new PreviewMeshData($"Faces_{px}_{py}",PreviewLayer.Faces),new PreviewMeshData($"Lips_{px}_{py}",PreviewLayer.Lips),new PreviewMeshData($"Contours_{px}_{py}",PreviewLayer.Contours)};
            if(meshes.Length!=4)throw new ArgumentException("Four layer buffers are required",nameof(reuse));
            if(clear)foreach(var mesh in meshes)mesh.Clear();
            var solid=meshes[0];var faces=meshes[1];var lips=meshes[2];var contours=meshes[3];
                for(int y=py+firstRow;y<Math.Min(Math.Min(py+patchSize,source.Height-1),py+(long)firstRow+rows);y++)
                for(int x=px;x<Math.Min(px+patchSize,source.Width-1);x++)
                {
                    if(optimized)
                    {
                        int code=ContourGeometry.Classify(source.IsSolid(x,y),source.IsSolid(x+1,y),source.IsSolid(x+1,y+1),source.IsSolid(x,y+1));
                        if(code==0)continue;
                        if(code==15){int start=x;while(mergeSolid&&x+1<Math.Min(px+patchSize,source.Width-1)&&source.IsSolid(x+2,y)&&source.IsSolid(x+2,y+1))x++;solid.Rectangle(start+.5,y+.5,x+1.5,y+1.5);continue;}
                    }
                    var strips=cache.GetTile(x,y);
                    if(optimized)CanonicalFill.Emit(strips[0].CaseId,x,y,solid,cache.Policy);else FillSolid(source,x,y,strips,solid);
                    bool softFrontier=source is IFrontierPresentation frontier&&frontier.SoftTile(x,y);
                    foreach(var strip in strips)
                    {
                        if(softFrontier)continue;
                        var top=workspace.Top;var projected=workspace.Walls;
                        for(int i=0;i<strip.Samples.Count;i++)
                        {projected[i]=WallGeometry.Project(strip.Samples[i],projection,WallSettings.Reference);top[i]=projected[i].Top;}
                        int faceBase=-1,lipBase=-1;
                        for(int i=0;i<strip.Samples.Count-1;i++)
                        {
                            if(Math.Max(strip.Samples[i].BackWeight,strip.Samples[i+1].BackWeight)>1e-4)
                            {
                                if(faceBase<0)faceBase=faces.RegisterWallSamples(strip.Samples);
                                faces.AttachedQuad(projected[i],projected[i+1],faceBase+i,faceBase+i+1,false);
                            }
                            if(Math.Max(strip.Samples[i].FrontWeight,strip.Samples[i+1].FrontWeight)>1e-4)
                            {
                                if(lipBase<0)lipBase=lips.RegisterWallSamples(strip.Samples);
                                lips.AttachedQuad(projected[i],projected[i+1],lipBase+i,lipBase+i+1,true);
                            }
                        }
                        Stroke(top,ContourWidth,contours,optimized?workspace.Left:null,optimized?workspace.Right:null);
                    }
                }
            return meshes;
        
            }
        }

        static class CanonicalFill
        {
            static readonly Point2[][] triangles=Create();
            sealed class Cell:IOccupancySource
            {public int Code;public int Width=>2;public int Height=>2;public bool IsSolid(int x,int y)=>(Code&(y==0?(x==0?1:2):(x==0?8:4)))!=0;}
            static Point2[][] Create()
            {
                var result=new Point2[32][];var cell=new Cell();
                for(int code=0;code<32;code++){cell.Code=code%16;var cache=new ContourTerrainCache(cell,code<16?DiagonalPolicy.ConnectSolid:DiagonalPolicy.ConnectOpen);var data=new PreviewMeshData("canonical",PreviewLayer.Solid);FillSolid(cell,0,0,cache.GetTile(0,0),data);var points=new Point2[data.LogicalVertices.Count];for(int i=0;i<points.Length;i++)points[i]=data.LogicalVertices[i].Logical-new Point2(.5,.5);result[code]=points;}
                return result;
            }
            public static void Warm(){GC.KeepAlive(triangles);}
            public static void Emit(int code,int x,int y,PreviewMeshData mesh,DiagonalPolicy policy){var t=triangles[code+(policy==DiagonalPolicy.ConnectOpen?16:0)];var origin=new Point2(x+.5,y+.5);for(int i=0;i<t.Length;i+=3)mesh.Triangle(t[i]+origin,t[i+1]+origin,t[i+2]+origin);}
        }
        public static void EmitCellSolid(IOccupancySource source,int x,int y,PreviewMeshData output){int code=ContourGeometry.Classify(source.IsSolid(x,y),source.IsSolid(x+1,y),source.IsSolid(x+1,y+1),source.IsSolid(x,y+1));CanonicalFill.Emit(code,x,y,output,DiagonalPolicy.ConnectSolid);}
        public static void WarmOptimizedTemplates()=>CanonicalFill.Warm();
        static void Connect(Dictionary<Point2,List<Point2>> graph,Point2 a,Point2 b)
        {
            if(!graph.TryGetValue(a,out var aa)){aa=new List<Point2>(2);graph.Add(a,aa);}aa.Add(b);
            if(!graph.TryGetValue(b,out var bb)){bb=new List<Point2>(2);graph.Add(b,bb);}bb.Add(a);
        }
        static void FillSolid(IOccupancySource source,int x,int y,IReadOnlyList<ContourStrip> strips,PreviewMeshData mesh)
        {
            bool tl=source.IsSolid(x,y),tr=source.IsSolid(x+1,y),br=source.IsSolid(x+1,y+1),bl=source.IsSolid(x,y+1);
            if(tl&&tr&&br&&bl){mesh.Rectangle(x+.5,y+.5,x+1.5,y+1.5);return;}
            if(!tl&&!tr&&!br&&!bl)return;
            Point2[] corners={new Point2(x+.5,y+.5),new Point2(x+1.5,y+.5),new Point2(x+1.5,y+1.5),new Point2(x+.5,y+1.5)};
            bool[] solid={tl,tr,br,bl};
            var graph=new Dictionary<Point2,List<Point2>>();
            for(int i=0;i<4;i++)
            {
                int j=(i+1)%4;Point2 midpoint=(corners[i]+corners[j])*.5;
                if(solid[i]&&solid[j])Connect(graph,corners[i],corners[j]);
                else if(solid[i])Connect(graph,corners[i],midpoint);
                else if(solid[j])Connect(graph,midpoint,corners[j]);
            }
            foreach(var strip in strips)for(int i=0;i<strip.Samples.Count-1;i++)Connect(graph,strip.Samples[i].Logical,strip.Samples[i+1].Logical);
            var visited=new HashSet<Point2>();
            foreach(var start in graph.Keys.OrderBy(p=>p.Y).ThenBy(p=>p.X))
            {
                if(visited.Contains(start))continue;
                var polygon=new List<Point2>();Point2 current=start,previous=default;bool first=true;
                do
                {
                    if(graph[current].Count!=2)throw new InvalidOperationException("Contour fill endpoint degree must be two.");
                    if(!visited.Add(current))throw new InvalidOperationException("Contour fill revisited a non-start vertex.");
                    polygon.Add(current);var neighbors=graph[current];Point2 next=first||!neighbors[0].Equals(previous)?neighbors[0]:neighbors[1];
                    previous=current;current=next;first=false;
                }while(!current.Equals(start));
                mesh.Polygon(polygon);
            }
        }
        static void Stroke(Point2[] points,double width,PreviewMeshData mesh,Point2[] left=null,Point2[] right=null)
        {
            // Shared anchor tangents make adjacent strip caps meet. This thin contour is diagnostic, not a seam cover.
            left=left??new Point2[points.Length];right=right??new Point2[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                Point2 before=points[Math.Max(0,i-1)],after=points[Math.Min(points.Length-1,i+1)];
                Point2 d=after-before;double length=Math.Sqrt(d.X*d.X+d.Y*d.Y);
                Point2 offset=new Point2(-d.Y/length*width*.5,d.X/length*width*.5);
                left[i]=points[i]+offset;right[i]=points[i]-offset;
            }
            for(int i=0;i<points.Length-1;i++)mesh.Quad(left[i],left[i+1],right[i+1],right[i]);
        }
    }
}
