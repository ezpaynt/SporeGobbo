using System;
using System.Collections.Generic;

namespace SporeGobbo.Geometry
{
    public readonly struct DualTile : IEquatable<DualTile>
    {
        public readonly int X,Y;
        public DualTile(int x,int y){X=x;Y=y;}
        public bool Equals(DualTile other)=>X==other.X&&Y==other.Y;
        public override bool Equals(object obj)=>obj is DualTile other&&Equals(other);
        public override int GetHashCode()=>unchecked(X*397^Y);
    }

    public sealed class ContourStrip
    {
        public DualTile Tile { get; }
        public int CaseId { get; }
        public ContourTemplate Template { get; }
        public IReadOnlyList<WallSample> Samples { get; }
        internal ContourStrip(DualTile tile,int caseId,ContourTemplate template,WallOrientationCache orientation)
        {
            Tile=tile;CaseId=caseId;Template=template;
            var samples=new WallSample[ContourGeometry.Subdivisions+1];
            for(int i=0;i<samples.Length;i++)samples[i]=orientation.Get(ContourGeometry.GlobalSample(tile.X,tile.Y,template,i));
            Samples=Array.AsReadOnly(samples);
        }
    }

    /// <summary>Pure derived cache for a bounded fixture/patch, not a terrain model. Source occupancy remains
    /// authoritative. Full-world ownership/streaming and mesh rendering are deliberately outside Stage 1.
    /// Boundary queries must see global neighbors, not clamp independently at future chunk edges.</summary>
    public sealed class ContourTerrainCache
    {
        readonly IOccupancySource source;
        readonly DiagonalPolicy policy;
        readonly Dictionary<DualTile,IReadOnlyList<ContourStrip>> tiles=new Dictionary<DualTile,IReadOnlyList<ContourStrip>>();
        readonly HashSet<DualTile> invalidTiles=new HashSet<DualTile>();
        public IReadOnlyCollection<DualTile> LastInvalidatedTiles=>invalidTiles;
        public DiagonalPolicy Policy=>policy;
        public int CachedTileCount => tiles.Count;
        public WallOrientationCache Orientation { get; }
        public ContourTerrainCache(IOccupancySource source,DiagonalPolicy policy=DiagonalPolicy.ConnectSolid)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));
            ContourGeometry.Templates(0,policy);this.policy=policy;Orientation=new WallOrientationCache(source);
        }
        public IReadOnlyList<ContourStrip> GetTile(int x,int y)
        {
            using(GeometryTrace.Measure(GeometryPhase.Contour))
            {
            if(x<0||y<0||x>=source.Width-1||y>=source.Height-1)throw new ArgumentOutOfRangeException(nameof(x));
            var tile=new DualTile(x,y);
            if(!tiles.TryGetValue(tile,out IReadOnlyList<ContourStrip> result))
            {
                int c=ContourGeometry.Classify(source.IsSolid(x,y),source.IsSolid(x+1,y),source.IsSolid(x+1,y+1),source.IsSolid(x,y+1));
                var templates=ContourGeometry.Templates(c,policy);var strips=new ContourStrip[templates.Count];
                for(int i=0;i<strips.Length;i++)strips[i]=new ContourStrip(tile,c,templates[i],Orientation);
                result=Array.AsReadOnly(strips);tiles.Add(tile,result);
            }
            return result;
        
            }
        }
        void SelectTiles(int x,int y)
        {
            for(int yy=Math.Max(0,y-4);yy<Math.Min(source.Height-1,y+4);yy++)
            for(int xx=Math.Max(0,x-4);xx<Math.Min(source.Width-1,x+4);xx++)invalidTiles.Add(new DualTile(xx,yy));
        }
        void ValidateCell(int x,int y)
        {if(x<0||y<0||x>=source.Width||y>=source.Height)throw new ArgumentOutOfRangeException(nameof(x));}
        public void InvalidateCell(int x,int y)
        {
            ValidateCell(x,y);Orientation.InvalidateCell(x,y);invalidTiles.Clear();SelectTiles(x,y);
            foreach(var tile in invalidTiles)tiles.Remove(tile);
        }
        public void InvalidateCells(IReadOnlyList<DualTile> cells)
        {
            if(cells==null)throw new ArgumentNullException(nameof(cells));
            using(GeometryTrace.Measure(GeometryPhase.CacheInvalidation))
            {
                for(int i=0;i<cells.Count;i++)ValidateCell(cells[i].X,cells[i].Y);
                Orientation.InvalidateCells(cells);invalidTiles.Clear();
                for(int i=0;i<cells.Count;i++)SelectTiles(cells[i].X,cells[i].Y);
                foreach(var tile in invalidTiles)tiles.Remove(tile);
            }
        }
        public void Clear(){tiles.Clear();Orientation.Clear();invalidTiles.Clear();}
        public static IEnumerable<DualTile> DirtyContourTiles(int x,int y,int width,int height)
        {
            for(int yy=y-1;yy<=y;yy++)for(int xx=x-1;xx<=x;xx++)
                if(xx>=0&&yy>=0&&xx<width-1&&yy<height-1)yield return new DualTile(xx,yy);
        }
        /// <summary>Reference half-open halo: x-4..x+3, y-4..y+3; 64 tiles away from world edges.</summary>
        public static IEnumerable<DualTile> DirtyWallTiles(int x,int y,int width,int height)
        {
            for(int yy=Math.Max(0,y-4);yy<Math.Min(height-1,y+4);yy++)
            for(int xx=Math.Max(0,x-4);xx<Math.Min(width-1,x+4);xx++)yield return new DualTile(xx,yy);
        }
    }
}
