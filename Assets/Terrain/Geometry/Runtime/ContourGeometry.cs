using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SporeGobbo.Geometry
{
    /// <summary>Double-precision logical/display point. Logical kit coordinates have Y increasing downwards.</summary>
    public readonly struct Point2 : IEquatable<Point2>
    {
        public readonly double X, Y;
        public Point2(double x, double y) { X = x; Y = y; }
        public bool Equals(Point2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Point2 other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public static Point2 operator +(Point2 a, Point2 b) => new Point2(a.X + b.X, a.Y + b.Y);
        public static Point2 operator -(Point2 a, Point2 b) => new Point2(a.X - b.X, a.Y - b.Y);
        public static Point2 operator *(Point2 a, double b) => new Point2(a.X * b, a.Y * b);
    }

    public enum DiagonalPolicy { ConnectSolid, ConnectOpen }
    public enum ContourTemplate { ArcTL, ArcTR, ArcBR, ArcBL, Horizontal, Vertical }

    public readonly struct CubicCurve
    {
        public readonly Point2 Start, Control1, Control2, End;
        public CubicCurve(Point2 start, Point2 control1, Point2 control2, Point2 end)
        { Start = start; Control1 = control1; Control2 = control2; End = end; }
        public Point2 Evaluate(double t)
        {
            if (!GeometryNumbers.Finite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
            if (t == 0) return Start;
            if (t == 1) return End;
            double u = 1 - t;
            return Start * (u * u * u) + Control1 * (3 * u * u * t) + Control2 * (3 * u * t * t) + End * (t * t * t);
        }
    }

    public static class GeometryNumbers
    {
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static void Positive(double value, string name)
        { if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
    }

    /// <summary>Canonical binary Marching Squares. Bits are TL=1, TR=2, BR=4, BL=8.
    /// A dual tile (x,y) spans occupancy centers (x+.5,y+.5) through (x+1.5,y+1.5).
    /// Complement swaps saddle policy; fixed connect-solid cases 5/10 are not identical complements.</summary>
    public static class ContourGeometry
    {
        public const double Kappa = 0.5522847498307936;
        public const int Subdivisions = 10;
        public static readonly Point2 Top = new Point2(.5, 0), Right = new Point2(1, .5),
            Bottom = new Point2(.5, 1), Left = new Point2(0, .5);
        static readonly CubicCurve[] curves = CreateCurves();
        static readonly ReadOnlyCollection<Point2>[] samples = CreateSamples();
        static readonly ReadOnlyCollection<ContourTemplate>[] cases = CreateCases();

        static CubicCurve[] CreateCurves()
        {
            double k = Kappa * .5;
            return new[] {
                new CubicCurve(Top,new Point2(.5,k),new Point2(k,.5),Left),
                new CubicCurve(Top,new Point2(.5,k),new Point2(1-k,.5),Right),
                new CubicCurve(Right,new Point2(1-k,.5),new Point2(.5,1-k),Bottom),
                new CubicCurve(Bottom,new Point2(.5,1-k),new Point2(k,.5),Left),
                new CubicCurve(Left,new Point2(1.0/3,.5),new Point2(2.0/3,.5),Right),
                new CubicCurve(Top,new Point2(.5,1.0/3),new Point2(.5,2.0/3),Bottom) };
        }
        static ReadOnlyCollection<Point2>[] CreateSamples()
        {
            var result = new ReadOnlyCollection<Point2>[curves.Length];
            for (int c = 0; c < curves.Length; c++)
            {
                var points = new Point2[Subdivisions + 1];
                for (int i = 0; i <= Subdivisions; i++) points[i] = curves[c].Evaluate(i / (double)Subdivisions);
                result[c] = Array.AsReadOnly(points);
            }
            return result;
        }
        static ReadOnlyCollection<ContourTemplate>[] CreateCases()
        {
            var tl = ContourTemplate.ArcTL; var tr = ContourTemplate.ArcTR;
            var br = ContourTemplate.ArcBR; var bl = ContourTemplate.ArcBL;
            var h = ContourTemplate.Horizontal; var v = ContourTemplate.Vertical;
            var raw = new[] { new ContourTemplate[0], new[]{tl},new[]{tr},new[]{h},new[]{br},new[]{tr,bl},new[]{v},new[]{bl},
                new[]{bl},new[]{v},new[]{tl,br},new[]{br},new[]{h},new[]{tr},new[]{tl},new ContourTemplate[0] };
            var result = new ReadOnlyCollection<ContourTemplate>[16];
            for (int i = 0; i < 16; i++) result[i] = Array.AsReadOnly(raw[i]);
            return result;
        }
        public static int Classify(bool tl, bool tr, bool br, bool bl) => (tl?1:0)|(tr?2:0)|(br?4:0)|(bl?8:0);
        public static IReadOnlyList<ContourTemplate> Templates(int caseId, DiagonalPolicy policy = DiagonalPolicy.ConnectSolid)
        {
            if (caseId < 0 || caseId > 15) throw new ArgumentOutOfRangeException(nameof(caseId));
            if (policy != DiagonalPolicy.ConnectSolid && policy != DiagonalPolicy.ConnectOpen) throw new ArgumentOutOfRangeException(nameof(policy));
            return cases[policy == DiagonalPolicy.ConnectOpen && (caseId == 5 || caseId == 10) ? 15-caseId : caseId];
        }
        public static CubicCurve Curve(ContourTemplate template)
        { if ((int)template < 0 || (int)template >= curves.Length) throw new ArgumentOutOfRangeException(nameof(template)); return curves[(int)template]; }
        public static IReadOnlyList<Point2> Samples(ContourTemplate template)
        { Curve(template); return samples[(int)template]; }
        public static Point2 GlobalSample(int tileX, int tileY, ContourTemplate template, int index)
            => Samples(template)[index] + new Point2(tileX + .5, tileY + .5);
        public static Point2 RotateClockwise(Point2 p) => new Point2(1-p.Y,p.X);
        public static Point2 ReflectX(Point2 p) => new Point2(1-p.X,p.Y);
        public static Point2 ReflectY(Point2 p) => new Point2(p.X,1-p.Y);
    }
}
