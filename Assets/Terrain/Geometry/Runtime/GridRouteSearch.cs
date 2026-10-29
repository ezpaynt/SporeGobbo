using System;
using System.Collections.Generic;

namespace SporeGobbo.Geometry
{
    public enum GridRouteStatus { Pending, Succeeded, Unreachable, Canceled }

    /// <summary>Resumable, deterministic four-neighbour A*. Reads the existing terrain owner only.
    /// Work includes stale heap entries and path reconstruction; no Unity or generation calls.</summary>
    public sealed class GridRouteSearch
    {
        readonly RegionTerrainState terrain;
        readonly int revision, width, goal;
        readonly double halfX, halfY, offsetX, offsetY;
        readonly List<Entry> heap = new List<Entry>();
        sealed class Page { public readonly int[] Costs=new int[256],Parents=new int[256]; }
        readonly Dictionary<int,Page> records=new Dictionary<int,Page>();
        bool Cost(int index,out int value)
        {if(records.TryGetValue(index>>8,out var p)&&p.Costs[index&255]>0){value=p.Costs[index&255]-1;return true;}value=0;return false;}
        void Record(int index,int cost,int parent)
        {if(!records.TryGetValue(index>>8,out var p)){p=new Page();records.Add(index>>8,p);}p.Costs[index&255]=cost+1;p.Parents[index&255]=parent;}
        int Parent(int index)=>records[index>>8].Parents[index&255];
        readonly List<Point2> result = new List<Point2>();
        int trace = -1, reverse;
        bool reconstructing, reversing;
        public GridRouteStatus Status { get; private set; } = GridRouteStatus.Pending;
        public int Visited { get; private set; }
        public long Work { get; private set; }
        public int Revision => revision;
        public IReadOnlyList<Point2> Path => result;

        readonly struct Entry
        {
            public readonly int Index, G, H;
            public Entry(int index,int g,int h) { Index=index; G=g; H=h; }
        }
        static bool Before(Entry a,Entry b)
        {
            long af=(long)a.G+a.H,bf=(long)b.G+b.H;
            return af<bf || af==bf && (a.H<b.H || a.H==b.H && a.Index<b.Index);
        }
        public GridRouteSearch(RegionTerrainState terrain,Point2 start,Point2 end,double halfX,double halfY)
        {
            this.terrain=terrain ?? throw new ArgumentNullException(nameof(terrain));
            if(double.IsNaN(halfX)||double.IsNaN(halfY)||double.IsInfinity(halfX)||double.IsInfinity(halfY)||halfX<=0||halfY<=0)
                throw new ArgumentOutOfRangeException(nameof(halfX));
            this.halfX=halfX;this.halfY=halfY;revision=terrain.Revision;width=terrain.World.Width;
            // Even-cell footprints use boundary-centred anchors; no forced odd-cell inflation.
            offsetX=((int)Math.Ceiling(halfX*2-.000001)%2)==0?0:.5;
            offsetY=((int)Math.Ceiling(halfY*2-.000001)%2)==0?0:.5;
            if(!Finite(start)||!Finite(end)||!Fits(start)||!Fits(end)) { Status=GridRouteStatus.Unreachable;return; }
            int sx=(int)Math.Floor(start.X-offsetX+.5),sy=(int)Math.Floor(start.Y-offsetY+.5),gx=(int)Math.Floor(end.X-offsetX+.5),gy=(int)Math.Floor(end.Y-offsetY+.5);
            if(!terrain.World.Contains(sx,sy)||!terrain.World.Contains(gx,gy)) { Status=GridRouteStatus.Unreachable;return; }
            int first=sy*width+sx;goal=gy*width+gx;
            // Search centres must also fit; exact endpoints are swept by the existing physical adapter.
            if(!Fits(Center(first))||!Fits(Center(goal))) { Status=GridRouteStatus.Unreachable;return; }
            Record(first,0,-1);Push(new Entry(first,0,Heuristic(first)));
        }
        static bool Finite(Point2 p)=>!double.IsNaN(p.X)&&!double.IsNaN(p.Y)&&!double.IsInfinity(p.X)&&!double.IsInfinity(p.Y);
        Point2 Center(int i)=>new Point2(i%width+offsetX,i/width+offsetY);
        bool Fits(Point2 p)
        {
            if(p.X-halfX<0||p.Y-halfY<0||p.X+halfX>terrain.World.Width||p.Y+halfY>terrain.World.Height)return false;
            for(int y=(int)Math.Floor(p.Y-halfY+.000001);y<=(int)Math.Floor(p.Y+halfY-.000001);y++)
                for(int x=(int)Math.Floor(p.X-halfX+.000001);x<=(int)Math.Floor(p.X+halfX-.000001);x++)
                    if(terrain.World.IsSolid(x,y))return false;
            return true;
        }
        int Heuristic(int index)=>Math.Abs(index%width-goal%width)+Math.Abs(index/width-goal/width);
        public void Cancel() { if(Status==GridRouteStatus.Pending) { Status=GridRouteStatus.Canceled;result.Clear(); } }
        public GridRouteStatus Step(int budget)
        {
            if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            if(Status!=GridRouteStatus.Pending)return Status;
            if(revision!=terrain.Revision) { Cancel();return Status; }
            for(int n=0;n<budget && Status==GridRouteStatus.Pending;n++)
            {
                Work++;
                if(reversing)
                {
                    int opposite=result.Count-1-reverse;
                    if(reverse>=opposite) { Status=GridRouteStatus.Succeeded;continue; }
                    var p=result[reverse];result[reverse]=result[opposite];result[opposite]=p;reverse++;continue;
                }
                if(reconstructing)
                {
                    if(trace<0) { reversing=true;continue; }
                    result.Add(Center(trace));trace=Parent(trace);continue;
                }
                if(heap.Count==0) { Status=GridRouteStatus.Unreachable;continue; }
                Entry item=Pop();
                if(!Cost(item.Index,out int known)||known!=item.G)continue;
                Visited++;
                if(item.Index==goal) { trace=goal;reconstructing=true;continue; }
                int x=item.Index%width,y=item.Index/width;
                for(int d=0;d<4;d++)
                {
                    int nx=x+(d==0?-1:d==1?1:0),ny=y+(d==2?-1:d==3?1:0);
                    if(!terrain.World.Contains(nx,ny))continue;
                    int next=ny*width+nx,cost=item.G+1;
                    if(Cost(next,out int old)&&old<=cost||!Fits(Center(next)))continue;
                    Record(next,cost,item.Index);Push(new Entry(next,cost,Heuristic(next)));
                }
            }
            return Status;
        }
        void Push(Entry value)
        {
            int i=heap.Count;heap.Add(value);
            while(i>0) { int parent=(i-1)/2;if(!Before(value,heap[parent]))break;heap[i]=heap[parent];i=parent; }
            heap[i]=value;
        }
        Entry Pop()
        {
            Entry first=heap[0],last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);
            if(heap.Count==0)return first;
            int i=0;
            while(i*2+1<heap.Count)
            {
                int child=i*2+1;if(child+1<heap.Count&&Before(heap[child+1],heap[child]))child++;
                if(!Before(heap[child],last))break;heap[i]=heap[child];i=child;
            }
            heap[i]=last;return first;
        }
    }
}
