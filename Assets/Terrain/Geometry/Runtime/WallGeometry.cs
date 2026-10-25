using System;
using System.Collections.Generic;

namespace SporeGobbo.Geometry
{
    /// <summary>Read-only view of existing terrain. Implementations own bounds; geometry clamps only at true world edges.</summary>
    public interface IOccupancySource
    {
        int Width { get; }
        int Height { get; }
        bool IsSolid(int column, int row);
    }

    public interface ISmoothedOccupancy { bool TryField(double x,double y,out double value); }

    public readonly struct WallSample
    {
        public readonly Point2 Logical, SolidNormal;
        public readonly double BackWeight, FrontWeight;
        public WallSample(Point2 logical, Point2 solidNormal)
        {
            Logical=logical;SolidNormal=solidNormal;
            BackWeight=WallGeometry.SmoothStep(.08,.72,-solidNormal.Y);
            FrontWeight=WallGeometry.SmoothStep(.12,.72,solidNormal.Y);
        }
    }

    public readonly struct WallDisplayVertex
    {
        public readonly Point2 Top, Bottom, Lip;
        public WallDisplayVertex(Point2 top,Point2 bottom,Point2 lip) { Top=top;Bottom=bottom;Lip=lip; }
    }

    public readonly struct WallSettings
    {
        public readonly double HeightCells, LipWidthCells;
        public static WallSettings Reference => new WallSettings(2.15,.22);
        public WallSettings(double heightCells,double lipWidthCells)
        {
            if(!GeometryNumbers.Finite(heightCells)||heightCells<0)throw new ArgumentOutOfRangeException(nameof(heightCells));
            if(!GeometryNumbers.Finite(lipWidthCells)||lipWidthCells<0)throw new ArgumentOutOfRangeException(nameof(lipWidthCells));
            HeightCells=heightCells;LipWidthCells=lipWidthCells;
        }
    }

    public static class WallGeometry
    {
        public const int FilterRadius=2;
        public const double FilterSigma=1.15;
        public const double GradientEpsilon=.035;
        internal static double SmoothStep(double a,double b,double value)
        { double t=Math.Max(0,Math.Min(1,(value-a)/(b-a)));return t*t*(3-2*t); }
        /// <summary>Projection is evaluated after logical geometry/orientation, so changing display settings
        /// cannot regenerate topology or require occupancy queries. Reference depth extends in +display Y.</summary>
        public static WallDisplayVertex Project(WallSample sample,ICartesianProjection projection,WallSettings settings,WallHeightPolicy heightPolicy=WallHeightPolicy.ReferenceLocalScale)
        {
            if(projection==null)throw new ArgumentNullException(nameof(projection));
            Point2 top=projection.Project(sample.Logical);
            double dy=projection.ScaleY(sample.Logical.Y);
            double vx=-sample.SolidNormal.X*projection.ScaleX(sample.Logical.X),vy=-sample.SolidNormal.Y*dy;
            double length=Math.Sqrt(vx*vx+vy*vy);if(length==0)length=1;
            double lip=dy*settings.LipWidthCells*sample.FrontWeight;
            Point2 bottom;
            if(heightPolicy==WallHeightPolicy.ReferenceLocalScale)bottom=new Point2(top.X,top.Y+dy*settings.HeightCells*sample.BackWeight);
            else if(heightPolicy==WallHeightPolicy.ProjectLogicalExtrusion)bottom=projection.Project(new Point2(sample.Logical.X,sample.Logical.Y+settings.HeightCells*sample.BackWeight));
            else throw new ArgumentOutOfRangeException(nameof(heightPolicy));
            return new WallDisplayVertex(top,bottom,new Point2(top.X+vx/length*lip,top.Y+vy/length*lip));
        }
    }

    /// <summary>Per-terrain geometry cache, never a manager. Caller must invalidate after every occupancy
    /// edit, or discard the cache after wholesale terrain replacement. Not thread-safe.</summary>
    public sealed class WallOrientationCache
    {
        readonly IOccupancySource source;
        readonly Dictionary<Point2,WallSample> cache=new Dictionary<Point2,WallSample>();
        const int BucketSize=4;
        readonly Dictionary<DualTile,List<Point2>> buckets=new Dictionary<DualTile,List<Point2>>();
        readonly HashSet<DualTile> affectedBuckets=new HashSet<DualTile>();
        public int LastInvalidationVisitedSamples {get;private set;}
        public int LastInvalidationBucketCount=>affectedBuckets.Count;
        static readonly double[] weights=MakeWeights();
        static readonly double weightSum=Sum(weights);
        public static double FilterWeight(int x,int y)=>weights[(y+2)*5+x+2];
        public static double FilterWeightSum=>weightSum;
        public int CachedCount => cache.Count;
        public int Evaluations { get; private set; }
        public WallOrientationCache(IOccupancySource source)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));
            if(source.Width<2||source.Height<2)throw new ArgumentException("At least two occupancy samples per axis are required",nameof(source));
        }
        static double[] MakeWeights()
        {
            var result=new double[25];int i=0;
            for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)result[i++]=Math.Exp(-(x*x+y*y)/(2*WallGeometry.FilterSigma*WallGeometry.FilterSigma));
            return result;
        }
        static double Sum(double[] values) { double sum=0;foreach(double v in values)sum+=v;return sum; }
        double Sample(int x,int y) => source.IsSolid(Math.Max(0,Math.Min(source.Width-1,x)),Math.Max(0,Math.Min(source.Height-1,y)))?1:0;
        double Field(double x,double y)
        {
            double sx=x-.5,sy=y-.5;int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy);double tx=sx-ix,ty=sy-iy;
            double a=Sample(ix,iy),b=Sample(ix+1,iy),c=Sample(ix+1,iy+1),d=Sample(ix,iy+1);
            return a*(1-tx)*(1-ty)+b*tx*(1-ty)+c*tx*ty+d*(1-tx)*ty;
        }
        public WallSample Get(Point2 point)
        {
            if(!GeometryNumbers.Finite(point.X)||!GeometryNumbers.Finite(point.Y)||point.X<0||point.X>source.Width||point.Y<0||point.Y>source.Height)
                throw new ArgumentOutOfRangeException(nameof(point));
            if(cache.TryGetValue(point,out WallSample result))return result;
            using(GeometryTrace.Measure(GeometryPhase.WallFilter))
            {
            double gx=0,gy=0,eps=WallGeometry.GradientEpsilon;int i=0;
            if(source is ISmoothedOccupancy smoothed&&smoothed.TryField(point.X+eps,point.Y,out double xp)&&smoothed.TryField(point.X-eps,point.Y,out double xm)&&smoothed.TryField(point.X,point.Y+eps,out double yp)&&smoothed.TryField(point.X,point.Y-eps,out double ym))
            {gx=(xp-xm)/(2*eps)*weightSum;gy=(yp-ym)/(2*eps)*weightSum;}
            else for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
            {
                double w=weights[i++];
                gx+=w*(Field(point.X+x+eps,point.Y+y)-Field(point.X+x-eps,point.Y+y))/(2*eps);
                gy+=w*(Field(point.X+x,point.Y+y+eps)-Field(point.X+x,point.Y+y-eps))/(2*eps);
            }
            gx/=weightSum;gy/=weightSum;double n=Math.Sqrt(gx*gx+gy*gy);
            result=new WallSample(point,n>1e-8?new Point2(gx/n,gy/n):new Point2(0,-1));
            cache.Add(point,result);
            var bucket=Bucket(point);
            if(!buckets.TryGetValue(bucket,out var points)){points=new List<Point2>(32);buckets.Add(bucket,points);}
            points.Add(point);Evaluations++;return result;
            }
        }
        static DualTile Bucket(Point2 point)=>new DualTile((int)Math.Floor(point.X/BucketSize),(int)Math.Floor(point.Y/BucketSize));
        void SelectBuckets(int x,int y)
        {
            int x0=(int)Math.Floor((x+.5-4)/BucketSize),x1=(int)Math.Floor((x+.5+4)/BucketSize);
            int y0=(int)Math.Floor((y+.5-4)/BucketSize),y1=(int)Math.Floor((y+.5+4)/BucketSize);
            for(int yy=y0;yy<=y1;yy++)for(int xx=x0;xx<=x1;xx++)affectedBuckets.Add(new DualTile(xx,yy));
        }
        static bool DependsOn(Point2 point,int x,int y)=>Math.Abs(point.X-(x+.5))<=4&&Math.Abs(point.Y-(y+.5))<=4;
        void InvalidateSelected(IReadOnlyList<DualTile> cells,int singleX=0,int singleY=0)
        {
            LastInvalidationVisitedSamples=0;
            foreach(var bucket in affectedBuckets)
            {
                if(!buckets.TryGetValue(bucket,out var points))continue;
                for(int i=points.Count-1;i>=0;i--)
                {
                    var point=points[i];LastInvalidationVisitedSamples++;
                    bool remove=cells==null&&DependsOn(point,singleX,singleY);
                    // Indexed access avoids one boxed List enumerator per cached sample.
                    if(cells!=null)for(int c=0;c<cells.Count;c++)
                        if(DependsOn(point,cells[c].X,cells[c].Y)){remove=true;break;}
                    if(!remove)continue;
                    cache.Remove(point);int last=points.Count-1;points[i]=points[last];points.RemoveAt(last);
                }
            }
        }
        public void InvalidateCell(int x,int y)
        {
            using(GeometryTrace.Measure(GeometryPhase.WallInvalidation))
            {affectedBuckets.Clear();SelectBuckets(x,y);InvalidateSelected(null,x,y);}
        }
        public void InvalidateCells(IReadOnlyList<DualTile> cells)
        {
            if(cells==null)throw new ArgumentNullException(nameof(cells));
            using(GeometryTrace.Measure(GeometryPhase.WallInvalidation))
            {
                affectedBuckets.Clear();
                for(int i=0;i<cells.Count;i++)SelectBuckets(cells[i].X,cells[i].Y);
                InvalidateSelected(cells);
            }
        }
        public void Clear(){cache.Clear();buckets.Clear();affectedBuckets.Clear();LastInvalidationVisitedSamples=0;}
    }
}
