using System;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Authored polyline at constant logical speed, ping-pong at endpoints. No pathfinding.</summary>
    public sealed class PreviewLogicalRoute
    {
        public readonly Point2[] Points;public readonly double Length;
        public PreviewLogicalRoute(params Point2[] points)
        {if(points==null||points.Length<2)throw new ArgumentException("At least two route points required.");Points=(Point2[])points.Clone();for(int i=1;i<Points.Length;i++){double x=Points[i].X-Points[i-1].X,y=Points[i].Y-Points[i-1].Y;Length+=Math.Sqrt(x*x+y*y);}if(Length<=0)throw new ArgumentException("Route needs nonzero length.");}
        public Point2 AtDistance(double distance)
        {
            double d=Math.Max(0,distance)%(2*Length);if(d>Length)d=2*Length-d;
            for(int i=1;i<Points.Length;i++){var a=Points[i-1];var b=Points[i];double x=b.X-a.X,y=b.Y-a.Y,len=Math.Sqrt(x*x+y*y);if(d<=len)return new Point2(a.X+x*d/len,a.Y+y*d/len);d-=len;}return Points[Points.Length-1];
        }
    }
    public sealed class PreviewLogicalProjectile
    {
        public readonly Point2 Origin,Direction;public Point2 Position {get;private set;}
        public double Travelled {get;private set;}public bool Blocked {get;private set;}public bool Finished {get;private set;}
        public DualTile HitCell {get;private set;}
        public PreviewLogicalProjectile(Point2 origin,Point2 direction)
        {double length=Math.Sqrt(direction.X*direction.X+direction.Y*direction.Y);if(length==0)throw new ArgumentException("Direction cannot be zero.");Origin=origin;Direction=new Point2(direction.X/length,direction.Y/length);Reset();}
        public void Reset(){Position=Origin;Travelled=0;Blocked=Finished=false;}
        public void Advance(double delta,double speed,double range,IOccupancySource terrain)
        {
            if(Finished||delta<=0)return;double remaining=Math.Min(Math.Max(0,speed)*delta,Math.Max(0,range-Travelled));
            while(remaining>1e-9)
            {
                double step=Math.Min(.05,remaining);var next=new Point2(Position.X+Direction.X*step,Position.Y+Direction.Y*step);
                var cell=new DualTile((int)Math.Floor(next.X),(int)Math.Floor(next.Y));
                if(terrain!=null&&terrain.IsSolid(cell.X,cell.Y)){HitCell=cell;Blocked=Finished=true;return;}
                Position=next;Travelled+=step;remaining-=step;
            }
            if(Travelled>=range-1e-8)Finished=true;
        }
    }
}
