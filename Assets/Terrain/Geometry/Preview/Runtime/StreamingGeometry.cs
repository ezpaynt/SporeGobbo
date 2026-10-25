using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry.Preview
{
    public sealed class StreamedChunk
    {
        public readonly ChunkReadWindow ReadWindow;
        public readonly DualTile Key;public readonly ContourTerrainCache Cache;
        public PreviewMeshData[] Layers;public int Revision;
        public StreamedChunk(DualTile key,IOccupancySource source,bool optimized=false){Key=key;if(optimized)ReadWindow=new ChunkReadWindow(source,key);Cache=new ContourTerrainCache(ReadWindow??source);}
        public void Release(){Layers=null;Cache.Clear();}
    }
    /// <summary>Input/UI/Unity-object independent residency. Each chunk owns its derived cache; neighbor reads use the authority.</summary>
    public sealed class StreamingGeometry : IDisposable
    {
        public readonly bool Optimized;public bool SurfaceCellGrid;
        readonly QueueComparer queueComparer=new QueueComparer();
        readonly Stack<PreviewMeshData[]> meshPool=new Stack<PreviewMeshData[]>();
        StreamedChunk preparing;int preparedRows;
        public int PooledData=>meshPool.Count;public int Preparing=>preparing==null?0:1;
        public readonly StreamingWorld World;public readonly IOccupancySource PresentationSource;
        readonly HashSet<DualTile> presentationDirty=new HashSet<DualTile>();readonly List<DualTile> presentationWork=new List<DualTile>();
        public int PendingPresentation=>presentationDirty.Count;public int PresentationRebuilds;public double PresentationMs;
        public bool IsPresentationCurrent(DualTile key)=>!presentationDirty.Contains(key);
        public void QueuePresentation(IReadOnlyList<DualTile> cells)
        {
            foreach(var cell in cells)for(int y=Math.Max(0,cell.Y-4);y<Math.Min(World.Height-1,cell.Y+4);y++)for(int x=Math.Max(0,cell.X-4);x<Math.Min(World.Width-1,cell.X+4);x++)
            {var key=StreamingCoordinates.Owner(x,y);if(Resident.ContainsKey(key))presentationDirty.Add(key);if(preparing!=null&&preparing.Key.Equals(key)){Recycle(preparing);preparing=null;}}
        }
        public int PumpPresentation(int maximum=2,double milliseconds=2)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();int n=0;presentationWork.Clear();presentationWork.AddRange(presentationDirty);
            presentationWork.Sort((a,b)=>{int v=(Visible.Contains(a)?0:1).CompareTo(Visible.Contains(b)?0:1);if(v!=0)return v;v=a.Y.CompareTo(b.Y);return v!=0?v:a.X.CompareTo(b.X);});
            foreach(var key in presentationWork){presentationDirty.Remove(key);if(Resident.TryGetValue(key,out var c)){c.ReadWindow?.Refresh();c.Cache.Clear();Build(c);Rebuilt?.Invoke(c);n++;PresentationRebuilds++;}if(n>=maximum||Elapsed(start)>=milliseconds)break;}PresentationMs=Elapsed(start);return n;
        }
        public readonly Dictionary<DualTile,StreamedChunk> Resident=new Dictionary<DualTile,StreamedChunk>();
        public readonly HashSet<DualTile> Visible=new HashSet<DualTile>(),Desired=new HashSet<DualTile>();
        readonly List<DualTile> pending=new List<DualTile>(),removed=new List<DualTile>();
        readonly List<DualTile> changed=new List<DualTile>();readonly HashSet<DualTile> dirty=new HashSet<DualTile>();
        readonly PreviewMeshWorkspace workspace=new PreviewMeshWorkspace();
        public Action<StreamedChunk> Activated,Evicted,Rebuilt;
        public int Capacity {get;} public int Pending=>pending.Count+(preparing==null?0:1);public int TotalBuilt,TotalEvicted,LastDigRebuilds,DigOperations;
        public double BuildMs,UnloadMs,DigMs;public bool OverBudget {get;private set;}
        public bool Ready {get{foreach(var key in Visible)if(!Resident.ContainsKey(key))return false;return !OverBudget;}}
        public int CacheSamples {get{int n=0;foreach(var c in Resident.Values)n+=c.Cache.Orientation.CachedCount;return n;}}
        public int CacheTiles {get{int n=0;foreach(var c in Resident.Values)n+=c.Cache.CachedTileCount;return n;}}
        public StreamingGeometry(StreamingWorld world,int capacity=384,bool optimized=false,IOccupancySource presentationSource=null){World=world;PresentationSource=presentationSource??world;if(PresentationSource.Width!=world.Width||PresentationSource.Height!=world.Height)throw new ArgumentException("Presentation source dimensions differ");Capacity=capacity;Optimized=optimized;if(optimized)PreviewMeshBuilder.WarmOptimizedTemplates();}
        public void Select(FocusProjection map,double width,double height,double wallHeight,double margin=24)
        {
            StreamingVisibility.Select(StreamingVisibility.LogicalRegion(map,width,height,wallHeight),World.Width,World.Height,Visible);
            StreamingVisibility.Select(StreamingVisibility.LogicalRegion(map,width,height,wallHeight,margin),World.Width,World.Height,Desired);
            OverBudget=Visible.Count>Capacity;
            int missing=0;foreach(var key in Visible)if(!Resident.ContainsKey(key))missing++;bool pressure=Resident.Count+missing>Capacity;
            pending.Clear();removed.Clear();foreach(var key in Resident.Keys)if(!Desired.Contains(key)||(pressure&&!Visible.Contains(key)))removed.Add(key);
            long start=System.Diagnostics.Stopwatch.GetTimestamp();for(int i=0;i<removed.Count&&i<8;i++){Evict(removed[i]);if(Elapsed(start)>=2)break;}UnloadMs=Elapsed(start);
            foreach(var key in Desired)if(!Resident.ContainsKey(key)&&(preparing==null||!preparing.Key.Equals(key)))pending.Add(key);
            // Visible before prefetch; central gameplay before periphery. Deterministic ties.
            if(Optimized){queueComparer.Focus=map.Focus;queueComparer.Visible=Visible;if(pending.Count>1)pending.Sort(queueComparer);}else SortReference(map);
        }
        void SortReference(FocusProjection map){if(pending.Count>1)pending.Sort((a,b)=>{int v=(Visible.Contains(a)?0:1).CompareTo(Visible.Contains(b)?0:1);if(v!=0)return v;double da=Distance(a,map.Focus),db=Distance(b,map.Focus);v=da.CompareTo(db);if(v!=0)return v;v=a.Y.CompareTo(b.Y);return v!=0?v:a.X.CompareTo(b.X);});}
        sealed class QueueComparer:IComparer<DualTile>
        {public Point2 Focus;public HashSet<DualTile> Visible;public int Compare(DualTile a,DualTile b){int v=(Visible.Contains(a)?0:1).CompareTo(Visible.Contains(b)?0:1);if(v!=0)return v;v=Distance(a,Focus).CompareTo(Distance(b,Focus));if(v!=0)return v;v=a.Y.CompareTo(b.Y);return v!=0?v:a.X.CompareTo(b.X);}}
        static double Distance(DualTile k,Point2 focus){double dx=k.X*16+8-focus.X,dy=k.Y*16+8-focus.Y;return dx*dx+dy*dy;}
        public int Pump(int maximum=2,double milliseconds=4)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();int built=0;PresentationMs=0;if(presentationDirty.Count>0)PumpPresentation(int.MaxValue,double.PositiveInfinity);
            if(Optimized)
            {
                if(preparing!=null&&!Desired.Contains(preparing.Key)){Recycle(preparing);preparing=null;}
                while((pending.Count>0||preparing!=null)&&built<maximum&&Resident.Count<Capacity)
                {
                    if(preparing==null){var key=pending[0];pending.RemoveAt(0);preparing=new StreamedChunk(key,PresentationSource,Optimized);if(meshPool.Count>0)preparing.Layers=meshPool.Pop();preparedRows=0;}
                    preparing.Layers=PreviewMeshBuilder.BuildOptimizedRows(PresentationSource,preparing.Cache,preparing.Key.X*16,preparing.Key.Y*16,16,preparedRows,2,preparing.Layers,workspace,preparedRows==0,!SurfaceCellGrid);preparedRows+=2;
                    if(preparedRows>=16){var complete=preparing;preparing=null;complete.Revision++;TotalBuilt++;Resident.Add(complete.Key,complete);Activated?.Invoke(complete);built++;}
                    if(Elapsed(start)>=Math.Min(milliseconds,1.5))break;
                }
                BuildMs=Elapsed(start);return built;
            }
            while(pending.Count>0&&built<maximum&&Resident.Count<Capacity)
            {var key=pending[0];pending.RemoveAt(0);var chunk=new StreamedChunk(key,PresentationSource,Optimized);Build(chunk);Resident.Add(key,chunk);Activated?.Invoke(chunk);built++;if(Elapsed(start)>=milliseconds)break;}
            BuildMs=Elapsed(start);return built;
        }
        void Build(StreamedChunk chunk){chunk.Layers=Optimized?PreviewMeshBuilder.BuildOptimizedRows(PresentationSource,chunk.Cache,chunk.Key.X*16,chunk.Key.Y*16,16,0,16,chunk.Layers,workspace,true,!SurfaceCellGrid):PreviewMeshBuilder.BuildPatch(PresentationSource,chunk.Cache,chunk.Key.X*16,chunk.Key.Y*16,16,chunk.Layers,workspace);chunk.Revision++;TotalBuilt++;}
        void Evict(DualTile key){presentationDirty.Remove(key);var chunk=Resident[key];Evicted?.Invoke(chunk);Recycle(chunk);Resident.Remove(key);TotalEvicted++;}
        void Recycle(StreamedChunk c){if(Optimized&&c.Layers!=null&&meshPool.Count<8){foreach(var layer in c.Layers)layer.Clear();meshPool.Push(c.Layers);}c.Release();}
        public int Refill(IReadOnlyList<DualTile> cells)
        {changed.Clear();foreach(var cell in cells)if(World.Refill(cell.X,cell.Y))changed.Add(cell);RefreshPhysicalCells(changed);return changed.Count;}
        public void RefreshPhysicalCells(IReadOnlyList<DualTile> cells)
        {
            dirty.Clear();LastDigRebuilds=0;
            if(preparing!=null&&cells.Count>0){Recycle(preparing);preparing=null;}
            foreach(var cell in cells)for(int y=Math.Max(0,cell.Y-4);y<Math.Min(World.Height-1,cell.Y+4);y++)for(int x=Math.Max(0,cell.X-4);x<Math.Min(World.Width-1,cell.X+4);x++)dirty.Add(StreamingCoordinates.Owner(x,y));
            foreach(var key in dirty)if(Resident.TryGetValue(key,out var c))
            {c.ReadWindow?.Refresh();c.Cache.InvalidateCells(cells);Build(c);Rebuilt?.Invoke(c);presentationDirty.Remove(key);LastDigRebuilds++;}
        }
        public int Dig(IReadOnlyList<DualTile> cells)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();changed.Clear();dirty.Clear();LastDigRebuilds=0;
            for(int i=0;i<cells.Count;i++){var cell=cells[i];if(World.Excavate(cell.X,cell.Y))changed.Add(cell);}
            RefreshPhysicalCells(changed);
            if(changed.Count>0)DigOperations++;DigMs=Elapsed(start);return changed.Count;
        }
        public void Dispose(){removed.Clear();removed.AddRange(Resident.Keys);foreach(var key in removed)Evict(key);if(preparing!=null){Recycle(preparing);preparing=null;}meshPool.Clear();pending.Clear();presentationDirty.Clear();presentationWork.Clear();Desired.Clear();Visible.Clear();}
        public static double Elapsed(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    }
}



