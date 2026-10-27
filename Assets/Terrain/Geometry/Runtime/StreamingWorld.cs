using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
namespace SporeGobbo.Geometry
{
    /// <summary>One authoritative bit per cell. Derived geometry never owns or copies terrain state.</summary>
    public interface IStreamingTerrainDefinition
    { bool TrySolid(int x,int y,out bool solid); bool Protected(int x,int y); }
    /// Complete authored definitions own every cell and protection rule; stress defaults never apply.
    public interface ICompleteTerrainDefinition : IStreamingTerrainDefinition {}
    public interface IBlockingTerrainDefinition { bool PlacementAllowed(int x,int y); }
    public interface IRefillableTerrainDefinition { bool Refillable(int x,int y); }
    public readonly struct TerrainCellChange
    {public readonly int X,Y,Revision;public readonly bool Solid;public TerrainCellChange(int x,int y,int revision,bool solid){X=x;Y=y;Revision=revision;Solid=solid;}}
    public sealed class StreamingWorld : IOccupancySource
    {
        readonly BitArray solid;
        public readonly IStreamingTerrainDefinition Definition;
        readonly HashSet<int> edits=new HashSet<int>();
        readonly HashSet<int> temporaryOpen=new HashSet<int>();
        public IReadOnlyCollection<int> TemporaryOpenCells=>temporaryOpen;
        public int Revision {get;private set;}public event Action<TerrainCellChange> Changed;
        public bool IsRefillable(int x,int y)=>Definition is IRefillableTerrainDefinition d&&d.Refillable(x,y);
        public int Width {get;} public int Height {get;} public int Seed {get;}
        public int EditCount=>edits.Count;
        public int OccupancyBytes=>(Width*Height+7)/8;
        public long Queries {get;private set;}
        public StreamingWorld(int width=1024,int height=1024,int seed=47045,IStreamingTerrainDefinition definition=null)
        {
            if(width<32||height<32||width>4096||height>4096)throw new ArgumentOutOfRangeException();
            Width=width;Height=height;Seed=seed;Definition=definition;solid=new BitArray(width*height);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)solid[y*width+x]=BaseSolid(x,y);
        }
        public bool Contains(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height;
        public bool Locked(int x,int y)=>Definition is ICompleteTerrainDefinition ? !Contains(x,y)||Definition.Protected(x,y) : (Definition!=null&&Definition.Protected(x,y))||!Contains(x,y)||x==0||y==0||x==Width-1||y==Height-1||(x%127==63&&y%113==41);
        public bool BaseSolid(int x,int y)
        {
            if(Locked(x,y))return true;
            if(Definition!=null&&Definition.TrySolid(x,y,out bool authored))return authored;
            if(Definition is ICompleteTerrainDefinition)throw new InvalidOperationException("Complete authored definition omitted a cell");
            // Guaranteed nine-cell horizontal/vertical travel axes, winding 3/9-cell branches and chambers.
            if(Math.Abs(y-Height/2)<=4||Math.Abs(x-Width/2)<=4)return false;
            if(x>Width/4&&x<Width*3/4&&Math.Abs(y-Height/4)<=1)return false;
            double phase=(Seed%997)*.01;
            double horizontal=(y+10*Math.Sin(x*.035+phase))%96;
            double vertical=(x+12*Math.Sin(y*.027+phase))%128;
            if(horizontal<1.5||horizontal>94.5||vertical<4.5||vertical>123.5)return false;
            int bx=x/64,by=y/64;uint hash=unchecked((uint)(bx*73856093^by*19349663^Seed));
            double cx=bx*64+20+(hash%25),cy=by*64+20+((hash>>8)%25);
            double dx=x-cx,dy=y-cy;
            if(dx*dx+dy*dy<144)return dx*dx+dy*dy<4; // isolated central pillar
            return true;
        }
        public bool IsSolid(int x,int y){Queries++;return !Contains(x,y)||solid[y*Width+x];}
        bool mutationControlTaken;
        public Func<int,int,bool> TakeExcavationControl(){if(mutationControlTaken)throw new InvalidOperationException("Terrain mutation already has an owner");mutationControlTaken=true;return ExcavateCore;}
        public bool Excavate(int x,int y){if(mutationControlTaken)throw new InvalidOperationException("Use the owning terrain transaction service");return ExcavateCore(x,y);}
        bool ExcavateCore(int x,int y)
        {if(Locked(x,y)||!solid[y*Width+x])return false;solid[y*Width+x]=false;if(IsRefillable(x,y))temporaryOpen.Add(y*Width+x);else edits.Add(y*Width+x);Revision++;Changed?.Invoke(new TerrainCellChange(x,y,Revision,false));return true;}
        bool placementControlTaken;
        internal Func<int,int,bool> TakePlacementControl(){if(!mutationControlTaken||placementControlTaken)throw new InvalidOperationException("Placement requires the existing terrain owner");placementControlTaken=true;return PlaceOwned;}
        bool PlaceOwned(int x,int y){if(Locked(x,y)||solid[y*Width+x])return false;solid[y*Width+x]=true;temporaryOpen.Remove(y*Width+x);edits.Remove(y*Width+x);Revision++;Changed?.Invoke(new TerrainCellChange(x,y,Revision,true));return true;}
        public bool Refill(int x,int y)
        {if(mutationControlTaken)throw new InvalidOperationException("Refill is not authorized by the terrain owner");if(!Contains(x,y)||Locked(x,y)||!IsRefillable(x,y)||solid[y*Width+x])return false;solid[y*Width+x]=true;temporaryOpen.Remove(y*Width+x);Revision++;Changed?.Invoke(new TerrainCellChange(x,y,Revision,true));return true;}
        public void WriteDeltas(BinaryWriter writer)
        {var sorted=new List<int>(edits);sorted.Sort();writer.Write(sorted.Count);foreach(int i in sorted)writer.Write(i);}
        public void ReadDeltas(BinaryReader r)
        {int count=r.ReadInt32();if(count<0||count>Width*Height)throw new InvalidDataException("Invalid delta count");for(int n=0;n<count;n++){int i=r.ReadInt32();if(i<0||i>=Width*Height||!Excavate(i%Width,i/Width))throw new InvalidDataException("Invalid excavation delta");}}
        public void Save(Stream stream)
        {
            if(Definition!=null)throw new InvalidOperationException("Use the authored preview state serializer for a defined fixture");
            using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
            {writer.Write(0x53473437);writer.Write(1);writer.Write(Width);writer.Write(Height);writer.Write(Seed);var sorted=new List<int>(edits);sorted.Sort();writer.Write(sorted.Count);foreach(int i in sorted)writer.Write(i);}
        }
        public static StreamingWorld Load(Stream stream)
        {
            using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
            {
                if(r.ReadInt32()!=0x53473437||r.ReadInt32()!=1)throw new InvalidDataException("Not a Stage 4.7 experiment snapshot");
                var world=new StreamingWorld(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());int count=r.ReadInt32();
                if(count<0||count>world.Width*world.Height)throw new InvalidDataException("Invalid delta count");
                for(int n=0;n<count;n++){int i=r.ReadInt32();if(i<0||i>=world.Width*world.Height||!world.Excavate(i%world.Width,i/world.Width))throw new InvalidDataException("Invalid excavation delta");}return world;
            }
        }
    }
    public static class StreamingCoordinates
    {
        public const int Size=16;
        public static int FloorDiv(int value,int divisor){int q=value/divisor;return value%divisor<0?q-1:q;}
        public static DualTile Owner(int x,int y)=>new DualTile(FloorDiv(x,Size),FloorDiv(y,Size));
        public static ProjectionBounds Bounds(DualTile key,int width,int height)=>new ProjectionBounds(new Point2(key.X*Size+.5,key.Y*Size+.5),new Point2(Math.Min((key.X+1)*Size+.5,width),Math.Min((key.Y+1)*Size+.5,height)));
    }
    public static class StreamingVisibility
    {
        /// <summary>Invert expanded display rectangle; derivatives are positive. Include both wall policies and lip X/Y displacement.</summary>
        public static ProjectionBounds LogicalRegion(FocusProjection map,double displayWidth,double displayHeight,double wallHeight,double travelMargin=0)
        {
            double scale=Math.Max(1,Math.Max(map.Top,map.Foreground));
            double pad=(Math.Max(0,wallHeight)+.22)*scale;
            var a=map.Unproject(new Point2(-displayWidth*.5-pad,-displayHeight*.5-pad));var b=map.Unproject(new Point2(displayWidth*.5+pad,displayHeight*.5+pad));
            return new ProjectionBounds(new Point2(a.X-travelMargin,a.Y-travelMargin),new Point2(b.X+travelMargin,b.Y+travelMargin));
        }
        public static void Select(ProjectionBounds region,int width,int height,HashSet<DualTile> output)
        {
            output.Clear();int size=StreamingCoordinates.Size;
            int x0=Math.Max(0,(int)Math.Floor((region.Min.X-.5)/size)),y0=Math.Max(0,(int)Math.Floor((region.Min.Y-.5)/size));
            int x1=Math.Min((width-2)/size,(int)Math.Floor((region.Max.X-.5)/size)),y1=Math.Min((height-2)/size,(int)Math.Floor((region.Max.Y-.5)/size));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)output.Add(new DualTile(x,y));
        }
        public static bool Contains(ProjectionBounds b,Point2 p)=>p.X>=b.Min.X&&p.Y>=b.Min.Y&&p.X<=b.Max.X&&p.Y<=b.Max.Y;
    }
    public sealed class StreamingActor
    {
        public readonly Point2 Home;public Point2 Position {get;private set;}
        public double Clock {get;private set;} public int Updates {get;private set;}
        public bool Active {get;private set;} public bool Visible {get;private set;}
        public StreamingActor(Point2 home){Home=Position=home;}
        public void Step(Point2 player,ProjectionBounds visible,double dt,IOccupancySource world,double radius=40)
        {
            double dx=Position.X-player.X,dy=Position.Y-player.Y;Active=dx*dx+dy*dy<=radius*radius;
            if(Active&&dt>0){Clock+=dt;Updates++;var next=new Point2(Home.X+Math.Sin(Clock)*1.5,Home.Y);if(!world.IsSolid((int)Math.Floor(next.X),(int)Math.Floor(next.Y)))Position=next;}
            Visible=StreamingVisibility.Contains(visible,Position);
        }
    }
}
