using System;
namespace SporeGobbo.Geometry
{
    /// <summary>Immutable camera-relative separable map. Positive smooth axis derivatives, integrated analytically.</summary>
    public sealed class FocusProjection : ICartesianProjection
    {
        public readonly Point2 Focus,Offset;
        public readonly double HalfWidth,HalfHeight,Distance,Side,Top,Foreground,SideDistance,TopDistance,ForegroundDistance,CurveBlend;
        public FocusProjection(Point2 focus,double width,double height,double distance,double side,double top,double foreground,Point2 offset,bool uniform=false,double sideDistance=0,double topDistance=0,double foregroundDistance=0,double curveBlend=0)
        {
            GeometryNumbers.Positive(width,nameof(width));GeometryNumbers.Positive(height,nameof(height));GeometryNumbers.Positive(distance,nameof(distance));
            GeometryNumbers.Positive(side,nameof(side));GeometryNumbers.Positive(top,nameof(top));GeometryNumbers.Positive(foreground,nameof(foreground));
            Focus=focus;Offset=offset;HalfWidth=width*.5;HalfHeight=height*.5;Distance=distance;
            if(double.IsNaN(curveBlend)||curveBlend<0||curveBlend>1)throw new ArgumentOutOfRangeException(nameof(curveBlend));CurveBlend=curveBlend;
            SideDistance=sideDistance==0?distance:sideDistance;TopDistance=topDistance==0?distance:topDistance;ForegroundDistance=foregroundDistance==0?distance*ForegroundDistanceRatio:foregroundDistance;
            GeometryNumbers.Positive(SideDistance,nameof(sideDistance));GeometryNumbers.Positive(TopDistance,nameof(topDistance));GeometryNumbers.Positive(ForegroundDistance,nameof(foregroundDistance));
            Side=uniform?1:side;Top=uniform?1:top;Foreground=uniform?1:foreground;
        }
        public const double SmoothQuadratic=3,SmoothCubic=-2,IntegralCubic=1,IntegralQuartic=-.5,ForegroundDistanceRatio=10.0/24;
        double Smooth(double t){double c=t*t*(SmoothQuadratic+SmoothCubic*t);return CurveBlend==0?c:c*(1-CurveBlend)+CurveBlend*t*t*t*(10+t*(-15+6*t));}
        double Axis(double value,double half,double negative,double positive,double distance)
        {
            double a=Math.Abs(value),edge=value<0?negative:positive,u=a-half;
            if(u<=0)return value;
            double t=Math.Min(u/distance,1),integral=distance*(IntegralCubic*t*t*t+IntegralQuartic*t*t*t*t);
            if(CurveBlend!=0)integral=integral*(1-CurveBlend)+CurveBlend*distance*(2.5*t*t*t*t-3*t*t*t*t*t+t*t*t*t*t*t);
            double mapped=a+(edge-1)*(integral+Math.Max(0,u-distance));
            return value<0?-mapped:mapped;
        }
        double Scale(double value,double half,double negative,double positive,double distance)
        {double t=Math.Max(0,Math.Min(1,(Math.Abs(value)-half)/distance));return 1+((value<0?negative:positive)-1)*Smooth(t);}
        double Inverse(double display,double half,double negative,double positive,double distance)
        {
            double sign=display<0?-1:1,a=Math.Abs(display),edge=display<0?negative:positive;
            if(a<=half)return display;
            double lo=half,hi=half+(a-half)/Math.Min(1,edge)+distance;
            for(int i=0;i<48;i++){double mid=(lo+hi)*.5;if(Math.Abs(Axis(sign*mid,half,negative,positive,distance))<a)lo=mid;else hi=mid;}
            return sign*(lo+hi)*.5;
        }
        public Point2 Project(Point2 p)=>new Point2(Axis(p.X-Focus.X,HalfWidth,Side,Side,SideDistance)+Offset.X,Axis(p.Y-Focus.Y,HalfHeight,Top,Foreground,p.Y>=Focus.Y?ForegroundDistance:TopDistance)+Offset.Y);
        public Point2 Unproject(Point2 p)=>new Point2(Inverse(p.X-Offset.X,HalfWidth,Side,Side,SideDistance)+Focus.X,Inverse(p.Y-Offset.Y,HalfHeight,Top,Foreground,p.Y>=Offset.Y?ForegroundDistance:TopDistance)+Focus.Y);
        public double ScaleX(double x)=>Scale(x-Focus.X,HalfWidth,Side,Side,SideDistance);
        public double ScaleY(double y)=>Scale(y-Focus.Y,HalfHeight,Top,Foreground,y>=Focus.Y?ForegroundDistance:TopDistance);
        public ProjectionBounds ConservativeBounds(ProjectionBounds logical,double height,double lip)
        {
            new WallSettings(height,lip);var a=Project(logical.Min);var b=Project(logical.Max);
            double max=Math.Max(1,Math.Max(Top,Foreground)),padding=lip*max;
            return new ProjectionBounds(new Point2(a.X-padding,a.Y-padding),new Point2(b.X+padding,b.Y+(height+lip)*max));
        }
    }
}
