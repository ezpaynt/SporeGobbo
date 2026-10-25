using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SporeGobbo.Geometry.Tests
{
    public sealed class GeometryFoundationTests
    {
        sealed class Occupancy : IOccupancySource
        {
            public int Width { get; }
            public int Height { get; }
            public readonly bool[,] Cells;
            public int Queries;
            public Occupancy(int width,int height){Width=width;Height=height;Cells=new bool[width,height];}
            public bool IsSolid(int x,int y){Queries++;return Cells[x,y];}
        }
        [Serializable] sealed class ProjectionFixture { public double[] column_widths_px; public double[] row_heights_px; }
        static string ReferencePath(string name)=>Path.Combine(Application.dataPath,"Terrain","Geometry","Tests","Reference~",name);
        static double Number(string value)=>double.Parse(value,CultureInfo.InvariantCulture);
        static void Near(Point2 actual,Point2 expected,double tolerance=1e-9)
        { Assert.That(actual.X,Is.EqualTo(expected.X).Within(tolerance));Assert.That(actual.Y,Is.EqualTo(expected.Y).Within(tolerance)); }
        static Occupancy Fixture()
        {
            string[] rows=File.ReadAllLines(ReferencePath("occupancy.txt"));var result=new Occupancy(rows[0].Length,rows.Length);
            for(int y=0;y<result.Height;y++)for(int x=0;x<result.Width;x++)result.Cells[x,y]=rows[y][x]=='1';return result;
        }
        static List<Point2> Points(int c,DiagonalPolicy policy)
        {
            var points=new List<Point2>();foreach(var t in ContourGeometry.Templates(c,policy))points.AddRange(ContourGeometry.Samples(t));return points;
        }
        static void SamePointSet(IEnumerable<Point2> a,IEnumerable<Point2> b)
        {
            var right=b.ToList();var left=a.ToList();Assert.That(left.Count,Is.EqualTo(right.Count));
            foreach(Point2 p in left)Assert.That(right.Any(q=>Math.Abs(q.X-p.X)<1e-12&&Math.Abs(q.Y-p.Y)<1e-12),Is.True);
        }

        [TestCaseSource(nameof(AllCases))]
        public void EveryCaseHasExpectedTopology(int c)
        {
            int expected=c==0||c==15?0:c==5||c==10?2:1;
            Assert.That(ContourGeometry.Classify((c&1)!=0,(c&2)!=0,(c&4)!=0,(c&8)!=0),Is.EqualTo(c));
            foreach(DiagonalPolicy policy in Enum.GetValues(typeof(DiagonalPolicy)))
                Assert.That(ContourGeometry.Templates(c,policy).Count,Is.EqualTo(expected));
        }
        static IEnumerable<int> AllCases=>Enumerable.Range(0,16);

        [Test]
        public void SaddlePoliciesAreExplicitAndComplementSwapsPolicy()
        {
            CollectionAssert.AreEqual(new[]{ContourTemplate.ArcTR,ContourTemplate.ArcBL},ContourGeometry.Templates(5));
            CollectionAssert.AreEqual(new[]{ContourTemplate.ArcTL,ContourTemplate.ArcBR},ContourGeometry.Templates(10));
            CollectionAssert.AreEqual(ContourGeometry.Templates(10),ContourGeometry.Templates(5,DiagonalPolicy.ConnectOpen));
            CollectionAssert.AreEqual(ContourGeometry.Templates(5),ContourGeometry.Templates(10,DiagonalPolicy.ConnectOpen));
            for(int c=0;c<16;c++)SamePointSet(Points(c,DiagonalPolicy.ConnectSolid),Points(15-c,DiagonalPolicy.ConnectOpen));
        }

        [TestCaseSource(nameof(AllCases))]
        public void RotationsAndReflectionsPreserveGeometry(int c)
        {
            int rotated=((c<<1)&15)|(c>>3);
            int reflected=((c&1)<<1)|((c&2)>>1)|((c&4)<<1)|((c&8)>>1);
            foreach(DiagonalPolicy p in Enum.GetValues(typeof(DiagonalPolicy)))
            {
                SamePointSet(Points(c,p).Select(ContourGeometry.RotateClockwise),Points(rotated,p));
                SamePointSet(Points(c,p).Select(ContourGeometry.ReflectX),Points(reflected,p));
                SamePointSet(Points(c,p).Select(ContourGeometry.ReflectY),Points(((c&1)<<3)|((c&2)<<1)|((c&4)>>1)|((c&8)>>3),p));
            }
        }

        [Test]
        public void EndpointsAreExactAnchorsWithEdgeCompatibleTangents()
        {
            var anchors=new[]{ContourGeometry.Top,ContourGeometry.Right,ContourGeometry.Bottom,ContourGeometry.Left};
            foreach(ContourTemplate t in Enum.GetValues(typeof(ContourTemplate)))
            {
                CubicCurve c=ContourGeometry.Curve(t);Assert.That(anchors,Does.Contain(c.Start));Assert.That(anchors,Does.Contain(c.End));
                Assert.That(ContourGeometry.Samples(t)[0],Is.EqualTo(c.Start));Assert.That(ContourGeometry.Samples(t)[10],Is.EqualTo(c.End));
                foreach(var pair in new[]{new[]{c.Start,c.Control1},new[]{c.End,c.Control2}})
                {
                    Point2 d=pair[1]-pair[0];Assert.That(d.X*d.X+d.Y*d.Y,Is.GreaterThan(0));
                    if(pair[0].X==0||pair[0].X==1)Assert.That(d.Y,Is.EqualTo(0));else Assert.That(d.X,Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void CoordinateAdapterMatchesMapDataCentersWithoutChangingScale()
        {
            var transform=new LogicalGridTransform(new Point2(-28.8,-18.6),.6,62);
            foreach(Point2 p in new[]{new Point2(0,0),new Point2(.5,.5),new Point2(50.25,39.75),new Point2(96,62),new Point2(-2,70)})
                Near(transform.ToLogical(transform.ToWorld(p)),p,1e-12);
            Near(transform.CellCenterToWorld(0,61),new Point2(-28.5,-18.3),1e-12);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new LogicalGridTransform(new Point2(0,0),0,62));
        }

        [Test]
        public void ReferenceProjectionAndNormalizedProjectionAreExplicitlyDifferent()
        {
            var f=JsonUtility.FromJson<ProjectionFixture>(File.ReadAllText(ReferencePath("grid_projection_exact.json")));
            var original=new StaticGridProjection(f.column_widths_px,f.row_heights_px);
            var normalized=StaticGridProjection.Normalize(f.column_widths_px,f.row_heights_px,11.25,10);
            Assert.That(original.Columns,Is.EqualTo(96));Assert.That(original.Rows,Is.EqualTo(62));
            Assert.That(original.ScaleX(48)/original.ScaleY(38),Is.EqualTo(1.125));
            Assert.That(normalized.ScaleX(48),Is.EqualTo(1));Assert.That(normalized.ScaleY(38),Is.EqualTo(1));
            Near(normalized.Project(new Point2(50.2,39.8)),new Point2(original.Project(new Point2(50.2,39.8)).X/11.25,original.Project(new Point2(50.2,39.8)).Y/10));
            Assert.That(original.ScaleY(51.999),Is.EqualTo(10));Assert.That(original.ScaleY(52.001),Is.EqualTo(11));
            var copy=new StaticGridProjection(new[]{2.0,3.0},new[]{4.0});Near(copy.Project(new Point2(-1,2)),new Point2(-2,8));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new StaticGridProjection(new[]{double.NaN},new[]{1.0}));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new StaticGridProjection(new[]{1.0},new[]{0.0}));
        }

        [Test]
        public void EveryCanonicalCurveSampleMatchesPythonReference()
        {
            int count=0;
            foreach(string line in File.ReadLines(ReferencePath("curve_golden.csv")).Skip(1))
            {
                string[] v=line.Split(',');var template=(ContourTemplate)int.Parse(v[0],CultureInfo.InvariantCulture);
                Near(ContourGeometry.Samples(template)[int.Parse(v[1],CultureInfo.InvariantCulture)],new Point2(Number(v[2]),Number(v[3])),1e-15);count++;
            }
            Assert.That(count,Is.EqualTo(66));
        }

        [Test]
        public void PythonGoldenNormalsAndStaticWallVerticesMatch()
        {
            var source=Fixture();var cache=new WallOrientationCache(source);
            var f=JsonUtility.FromJson<ProjectionFixture>(File.ReadAllText(ReferencePath("grid_projection_exact.json")));
            var projection=new StaticGridProjection(f.column_widths_px,f.row_heights_px);int count=0;double maximumError=0;
            foreach(string line in File.ReadLines(ReferencePath("wall_golden.csv")).Skip(1))
            {
                double[] v=line.Split(',').Select(Number).ToArray();WallSample sample=cache.Get(new Point2(v[0],v[1]));
                Near(sample.SolidNormal,new Point2(v[2],v[3]),1e-8);
                WallDisplayVertex display=WallGeometry.Project(sample,projection,WallSettings.Reference);
                Near(display.Top,new Point2(v[4],v[5]),1e-8);Near(display.Bottom,new Point2(v[6],v[7]),1e-8);Near(display.Lip,new Point2(v[8],v[9]),1e-8);count++;
                double[] actual={sample.SolidNormal.X,sample.SolidNormal.Y,display.Top.X,display.Top.Y,display.Bottom.X,display.Bottom.Y,display.Lip.X,display.Lip.Y};
                for(int i=0;i<actual.Length;i++)maximumError=Math.Max(maximumError,Math.Abs(actual[i]-v[i+2]));
            }
            Assert.That(count,Is.GreaterThanOrEqualTo(60));
            Debug.Log("Stage 1 Python parity: "+count+" normal/wall samples; maximum absolute component error="+maximumError.ToString("G17",CultureInfo.InvariantCulture));
        }

        [Test]
        public void SharedEndpointsHaveIdenticalTopBottomAndLipWithoutWelding()
        {
            var source=Fixture();var cache=new ContourTerrainCache(source);var projection=StaticGridProjection.Uniform(source.Width,source.Height);
            var shared=new Dictionary<Point2,WallDisplayVertex>();int matches=0,segments=0;
            for(int y=0;y<source.Height-1;y++)for(int x=0;x<source.Width-1;x++)foreach(var strip in cache.GetTile(x,y))
            {
                segments++;
                foreach(int i in new[]{0,10})
                {
                    WallSample sample=strip.Samples[i];var display=WallGeometry.Project(sample,projection,WallSettings.Reference);
                    if(shared.TryGetValue(sample.Logical,out WallDisplayVertex previous))
                    { Assert.That(display.Top,Is.EqualTo(previous.Top));Assert.That(display.Bottom,Is.EqualTo(previous.Bottom));Assert.That(display.Lip,Is.EqualTo(previous.Lip));matches++; }
                    else shared.Add(sample.Logical,display);
                }
            }
            Assert.That(segments,Is.EqualTo(585));Assert.That(matches,Is.GreaterThanOrEqualTo(581));
        }

        [TestCase(DiagonalPolicy.ConnectSolid)]
        [TestCase(DiagonalPolicy.ConnectOpen)]
        public void RandomizedIncrementalGeometryMatchesFreshRebuildIncludingWorldEdges(DiagonalPolicy policy)
        {
            var random=new System.Random(74191);var source=new Occupancy(13,11);
            for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)source.Cells[x,y]=random.Next(2)==1;
            var incremental=new ContourTerrainCache(source,policy);
            for(int stage=0;stage<18;stage++)
            {
                if(stage>0)
                {
                    int x=stage==1?0:stage==2?source.Width-1:random.Next(source.Width);
                    int y=stage==1?0:stage==2?source.Height-1:random.Next(source.Height);
                    source.Cells[x,y]=!source.Cells[x,y];incremental.InvalidateCell(x,y);
                }
                var full=new ContourTerrainCache(source,policy);
                for(int y=0;y<source.Height-1;y++)for(int x=0;x<source.Width-1;x++)
                {
                    var a=incremental.GetTile(x,y);var b=full.GetTile(x,y);Assert.That(a.Count,Is.EqualTo(b.Count));
                    for(int strip=0;strip<a.Count;strip++)
                    {
                        Assert.That(a[strip].CaseId,Is.EqualTo(b[strip].CaseId));Assert.That(a[strip].Template,Is.EqualTo(b[strip].Template));
                        for(int i=0;i<11;i++){Near(a[strip].Samples[i].Logical,b[strip].Samples[i].Logical);Near(a[strip].Samples[i].SolidNormal,b[strip].Samples[i].SolidNormal);}
                    }
                }
            }
        }

        [Test]
        public void SuppliedFourDigSequenceMatchesFullGeometryAtEveryStage()
        {
            var source=Fixture();var incremental=new ContourTerrainCache(source);
            var f=JsonUtility.FromJson<ProjectionFixture>(File.ReadAllText(ReferencePath("grid_projection_exact.json")));
            var projection=new StaticGridProjection(f.column_widths_px,f.row_heights_px);
            var digs=new[]{new DualTile(67,24),new DualTile(68,23),new DualTile(69,22),new DualTile(72,20)};
            for(int stage=0;stage<=digs.Length;stage++)
            {
                if(stage>0){var dig=digs[stage-1];Assert.That(source.Cells[dig.X,dig.Y],Is.True);source.Cells[dig.X,dig.Y]=false;incremental.InvalidateCell(dig.X,dig.Y);}
                var full=new ContourTerrainCache(source);
                for(int y=0;y<source.Height-1;y++)for(int x=0;x<source.Width-1;x++)
                {
                    var a=incremental.GetTile(x,y);var b=full.GetTile(x,y);Assert.That(a.Count,Is.EqualTo(b.Count));
                    for(int strip=0;strip<a.Count;strip++)for(int i=0;i<11;i++)
                    {
                        Assert.That(a[strip].Template,Is.EqualTo(b[strip].Template));
                        var av=WallGeometry.Project(a[strip].Samples[i],projection,WallSettings.Reference);
                        var bv=WallGeometry.Project(b[strip].Samples[i],projection,WallSettings.Reference);
                        Near(av.Top,bv.Top);Near(av.Bottom,bv.Bottom);Near(av.Lip,bv.Lip);
                    }
                }
            }
        }

        [Test]
        public void ReferenceHaloAndProjectionOnlyChangesDoNotQueryTerrain()
        {
            Assert.That(ContourTerrainCache.DirtyContourTiles(50,30,96,62).Count(),Is.EqualTo(4));
            Assert.That(ContourTerrainCache.DirtyWallTiles(50,30,96,62).Count(),Is.EqualTo(64));
            var source=Fixture();var cache=new WallOrientationCache(source);var point=new Point2(50,39);WallSample sample=cache.Get(point);
            int queries=source.Queries;int evaluations=cache.Evaluations;
            for(int i=0;i<20;i++){cache.Get(point);WallGeometry.Project(sample,StaticGridProjection.Uniform(96,62,1+i*.1),new WallSettings(i*.2,.22));}
            Assert.That(source.Queries,Is.EqualTo(queries));Assert.That(cache.Evaluations,Is.EqualTo(evaluations));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UniformAndSymmetricFieldsRemainFiniteAndDeterministic(bool solid)
        {
            var source=new Occupancy(8,8);for(int y=0;y<8;y++)for(int x=0;x<8;x++)source.Cells[x,y]=solid;
            var cache=new WallOrientationCache(source);WallSample sample=cache.Get(new Point2(3.5,3.5));Near(sample.SolidNormal,new Point2(0,-1));
            foreach(double scale in new[]{.001,1,1000000})
            {
                var v=WallGeometry.Project(sample,StaticGridProjection.Uniform(8,8,scale),WallSettings.Reference);
                Assert.That(GeometryNumbers.Finite(v.Bottom.X)&&GeometryNumbers.Finite(v.Bottom.Y)&&GeometryNumbers.Finite(v.Lip.X)&&GeometryNumbers.Finite(v.Lip.Y),Is.True);
            }
            cache.Clear();Assert.That(cache.Get(new Point2(3.5,3.5)).SolidNormal,Is.EqualTo(sample.SolidNormal));
        }
    }
}
