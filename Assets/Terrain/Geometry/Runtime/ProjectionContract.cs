using System;

namespace SporeGobbo.Geometry
{
    /// <summary>A single monotone, separable Cartesian mapping shared by all display consumers.
    /// Camera implementations must be continuous in focus position, have positive axis scales, and use
    /// the same square center scale on X and Y. This interface does not move cameras or gameplay actors.</summary>
    public interface ICartesianProjection
    {
        Point2 Project(Point2 logical);
        Point2 Unproject(Point2 display);
        double ScaleX(double logicalX);
        double ScaleY(double logicalY);
        ProjectionBounds ConservativeBounds(ProjectionBounds logical,double wallHeightCells,double lipWidthCells);
    }

    public readonly struct ProjectionBounds
    {
        public readonly Point2 Min,Max;
        public ProjectionBounds(Point2 min,Point2 max)
        {
            if(!GeometryNumbers.Finite(min.X)||!GeometryNumbers.Finite(min.Y)||!GeometryNumbers.Finite(max.X)||!GeometryNumbers.Finite(max.Y)||min.X>max.X||min.Y>max.Y)
                throw new ArgumentOutOfRangeException(nameof(min));
            Min=min;Max=max;
        }
        public bool Contains(Point2 point)=>point.X>=Min.X&&point.Y>=Min.Y&&point.X<=Max.X&&point.Y<=Max.Y;
    }

    /// <summary>Array-free uniform mapping used by the current preview. No camera-relative deformation.</summary>
    public sealed class UniformGridProjection : ICartesianProjection
    {
        public static readonly UniformGridProjection Unit=new UniformGridProjection(1);
        readonly double scale;
        public UniformGridProjection(double scale){GeometryNumbers.Positive(scale,nameof(scale));this.scale=scale;}
        public Point2 Project(Point2 logical)=>logical*scale;
        public Point2 Unproject(Point2 display)=>display*(1/scale);
        public double ScaleX(double logicalX)=>scale;
        public double ScaleY(double logicalY)=>scale;
        public ProjectionBounds ConservativeBounds(ProjectionBounds logical,double wallHeightCells,double lipWidthCells)
        {
            new WallSettings(wallHeightCells,lipWidthCells);
            double lip=lipWidthCells*scale;
            var min=Project(logical.Min);var max=Project(logical.Max);
            return new ProjectionBounds(new Point2(min.X-lip,min.Y-lip),new Point2(max.X+lip,max.Y+(wallHeightCells+lipWidthCells)*scale));
        }
    }

    /// <summary>Framing configuration only. Stage 3.5 never applies it to a camera.
    /// One pixel scale for both axes keeps central cells square; viewport aspect must be preserved.</summary>
    public readonly struct ProjectionFraming
    {
        public readonly int PixelWidth,PixelHeight;
        public readonly double PixelsPerDisplayUnit;
        public double DisplayWidth=>PixelWidth/PixelsPerDisplayUnit;
        public double DisplayHeight=>PixelHeight/PixelsPerDisplayUnit;
        public ProjectionFraming(int pixelWidth,int pixelHeight,double pixelsPerDisplayUnit)
        {
            if(pixelWidth<1||pixelHeight<1)throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            GeometryNumbers.Positive(pixelsPerDisplayUnit,nameof(pixelsPerDisplayUnit));
            PixelWidth=pixelWidth;PixelHeight=pixelHeight;PixelsPerDisplayUnit=pixelsPerDisplayUnit;
        }
    }

    public enum GeometryVertexRole : byte { Fill, Contour, WallTop, WallBottom, ForegroundLip, Grid, Actor }
    /// <summary>24-byte logical vertex record. Wall parameters are indexed separately, not repeated per triangle.
    /// Mesh ownership and indices remain independent of display mapping.</summary>
    public readonly struct LogicalMeshVertex
    {
        public readonly Point2 Logical;
        public readonly int WallSampleIndex;
        public readonly GeometryVertexRole Role;
        public LogicalMeshVertex(Point2 logical,GeometryVertexRole role,int wallSampleIndex=-1)
        {Logical=logical;Role=role;WallSampleIndex=wallSampleIndex;}
    }

    public enum WallHeightPolicy { ReferenceLocalScale, ProjectLogicalExtrusion }

    /// <summary>Starting experimental dimensions only; the camera renderer is deliberately not implemented.</summary>
    public readonly struct ProjectionLayout
    {
        public readonly int Columns,Rows,CenterColumns,CenterRows;
        public readonly double CenterScale;
        public static ProjectionLayout Reference=>new ProjectionLayout(96,62,48,28,1);
        public ProjectionLayout(int columns,int rows,int centerColumns,int centerRows,double centerScale=1)
        {
            if(columns<1||rows<1||centerColumns<1||centerRows<1||centerColumns>columns||centerRows>rows)throw new ArgumentOutOfRangeException(nameof(columns));
            GeometryNumbers.Positive(centerScale,nameof(centerScale));
            Columns=columns;Rows=rows;CenterColumns=centerColumns;CenterRows=centerRows;CenterScale=centerScale;
        }
    }
}
