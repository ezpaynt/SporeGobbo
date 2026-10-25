using System;

namespace SporeGobbo.Geometry
{
    /// <summary>Explicit adapter from the kit's Y-down cell-edge coordinates to a Y-up Unity world.
    /// No gameplay scale is owned or changed here. Supply the existing MapData origin/cell size.</summary>
    public readonly struct LogicalGridTransform
    {
        public readonly Point2 WorldOrigin;
        public readonly double CellSize;
        public readonly int Rows;
        public LogicalGridTransform(Point2 worldOrigin, double cellSize, int rows)
        {
            GeometryNumbers.Positive(cellSize,nameof(cellSize));
            if (!GeometryNumbers.Finite(worldOrigin.X) || !GeometryNumbers.Finite(worldOrigin.Y)) throw new ArgumentOutOfRangeException(nameof(worldOrigin));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            WorldOrigin=worldOrigin; CellSize=cellSize; Rows=rows;
        }
        public Point2 ToWorld(Point2 logical) => new Point2(WorldOrigin.X+logical.X*CellSize,WorldOrigin.Y+(Rows-logical.Y)*CellSize);
        public Point2 ToLogical(Point2 world) => new Point2((world.X-WorldOrigin.X)/CellSize,Rows-(world.Y-WorldOrigin.Y)/CellSize);
        public Point2 CellCenterToWorld(int column,int row) => ToWorld(new Point2(column+.5,row+.5));
    }

    /// <summary>Static, separable projection only. No camera following. Reference wall scaling deliberately
    /// retains piecewise-constant derivatives; temporal discontinuities are documented, not concealed.</summary>
    public sealed class StaticGridProjection : ICartesianProjection
    {
        readonly double[] widths, heights, xEdges, yEdges;
        public int Columns => widths.Length;
        public int Rows => heights.Length;
        public double Width => xEdges[xEdges.Length-1];
        public double Height => yEdges[yEdges.Length-1];
        public StaticGridProjection(double[] columnWidths,double[] rowHeights)
        {
            widths=Copy(columnWidths,nameof(columnWidths)); heights=Copy(rowHeights,nameof(rowHeights));
            xEdges=Edges(widths); yEdges=Edges(heights);
        }
        static double[] Copy(double[] values,string name)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("Axis must not be empty",name);
            var copy=(double[])values.Clone(); foreach(double v in copy) GeometryNumbers.Positive(v,name); return copy;
        }
        static double[] Edges(double[] sizes)
        {
            var edges=new double[sizes.Length+1];
            for(int i=0;i<sizes.Length;i++) { edges[i+1]=edges[i]+sizes[i]; if(!GeometryNumbers.Finite(edges[i+1])) throw new ArgumentOutOfRangeException(nameof(sizes)); }
            return edges;
        }
        static int Index(double v,int length)
        { if(!GeometryNumbers.Finite(v)) throw new ArgumentOutOfRangeException(nameof(v)); return v<=0?0:v>=length?length-1:(int)Math.Floor(v); }
        static double Axis(double v,double[] edges,double[] sizes)
        { int i=Index(v,sizes.Length); return edges[i]+(v-i)*sizes[i]; }
        public Point2 Project(Point2 p) => new Point2(Axis(p.X,xEdges,widths),Axis(p.Y,yEdges,heights));
        public double ScaleX(double x) => widths[Index(x,widths.Length)];
        public double ScaleY(double y) => heights[Index(y,heights.Length)];
        static double InverseAxis(double display,double[] edges,double[] sizes)
        {
            if(!GeometryNumbers.Finite(display))throw new ArgumentOutOfRangeException(nameof(display));
            int low=0,high=sizes.Length-1;
            while(low<high){int mid=(low+high+1)/2;if(edges[mid]<=display)low=mid;else high=mid-1;}
            return low+(display-edges[low])/sizes[low];
        }
        public Point2 Unproject(Point2 display)=>new Point2(InverseAxis(display.X,xEdges,widths),InverseAxis(display.Y,yEdges,heights));
        public ProjectionBounds ConservativeBounds(ProjectionBounds logical,double wallHeightCells,double lipWidthCells)
        {
            new WallSettings(wallHeightCells,lipWidthCells);
            double maximumYScale=0;
            int first=Index(logical.Min.Y,heights.Length),last=Index(logical.Max.Y,heights.Length);
            for(int i=first;i<=last;i++)maximumYScale=Math.Max(maximumYScale,heights[i]);
            var min=Project(logical.Min);var max=Project(logical.Max);
            double depthEnd=Math.Max(max.Y+wallHeightCells*maximumYScale,Project(new Point2(logical.Max.X,logical.Max.Y+wallHeightCells)).Y);
            double lip=lipWidthCells*maximumYScale;
            return new ProjectionBounds(new Point2(min.X-lip,min.Y-lip),new Point2(max.X+lip,depthEnd+lip));
        }
        public static StaticGridProjection Uniform(int columns,int rows,double squareScale=1)
        {
            if(columns<=0||rows<=0) throw new ArgumentOutOfRangeException(nameof(columns));
            GeometryNumbers.Positive(squareScale,nameof(squareScale));
            var w=new double[columns];var h=new double[rows];
            for(int i=0;i<w.Length;i++)w[i]=squareScale;for(int i=0;i<h.Length;i++)h[i]=squareScale;
            return new StaticGridProjection(w,h);
        }
        /// <summary>Normalize each axis by its declared central cell dimension. Relative peripheral ratios
        /// are preserved; the central cell is exactly squareScale by squareScale.</summary>
        public static StaticGridProjection Normalize(double[] widths,double[] heights,double centralWidth,double centralHeight,double squareScale=1)
        {
            GeometryNumbers.Positive(centralWidth,nameof(centralWidth));GeometryNumbers.Positive(centralHeight,nameof(centralHeight));GeometryNumbers.Positive(squareScale,nameof(squareScale));
            var w=Copy(widths,nameof(widths));var h=Copy(heights,nameof(heights));
            for(int i=0;i<w.Length;i++) w[i]=w[i]/centralWidth*squareScale;
            for(int i=0;i<h.Length;i++) h[i]=h[i]/centralHeight*squareScale;
            return new StaticGridProjection(w,h);
        }
    }
}
