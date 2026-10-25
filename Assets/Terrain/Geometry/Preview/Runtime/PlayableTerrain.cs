using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SporeGobbo.Geometry.Preview
{
    /// <summary>The sole mutable occupancy authority for this experiment. Reset reads the immutable fixture.</summary>
    public sealed class PlayableTerrain : IOccupancySource
    {
        readonly PreviewFixture initial;
        readonly bool[] cells;
        public int Width=>initial.Width;
        public int Height=>initial.Height;
        public PlayableTerrain(string fixture){initial=new PreviewFixture(fixture);cells=new bool[Width*Height];Reset();}
        public bool Contains(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height;
        public long OccupancyQueries {get;private set;}
        public bool IsSolid(int x,int y){OccupancyQueries++;return !Contains(x,y)||cells[y*Width+x];}
        public bool Excavate(DualTile cell){if(!Contains(cell.X,cell.Y)||!IsSolid(cell.X,cell.Y))return false;cells[cell.Y*Width+cell.X]=false;return true;}
        public void Reset(){for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)cells[y*Width+x]=initial.IsSolid(x,y);}
    }

    public static class PreviewMotion
    {
        public static bool Fits(IOccupancySource source,Vector2 position,float width)
        {
            if(width<=0||float.IsNaN(width))return false;
            float half=width*.5f;
            int x0=Mathf.FloorToInt(position.x-half+.00001f),x1=Mathf.CeilToInt(position.x+half-.00001f)-1;
            int y0=Mathf.FloorToInt(position.y-half+.00001f),y1=Mathf.CeilToInt(position.y+half-.00001f)-1;
            if(x0<0||y0<0||x1>=source.Width||y1>=source.Height)return false;
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(source.IsSolid(x,y))return false;
            return true;
        }
        /// <summary>Small swept AABB steps, then axis sliding. Never consults rendered walls or physics.</summary>
        public static Vector2 Move(IOccupancySource source,Vector2 position,Vector2 delta,float width)
        {
            if(!Fits(source,position,width))return position;
            int steps=Math.Max(1,Mathf.CeilToInt(delta.magnitude/.08f));Vector2 step=delta/steps;
            for(int i=0;i<steps;i++)
            {
                if(Fits(source,position+step,width)){position+=step;continue;}
                Vector2 x=new Vector2(step.x,0),y=new Vector2(0,step.y);
                if(Fits(source,position+x,width))position+=x;
                if(Fits(source,position+y,width))position+=y;
            }
            return position;
        }
        public static Vector2 FindSpawn(IOccupancySource source,Vector2 preferred,float width)
        {
            if(Fits(source,preferred,width))return preferred;
            Vector2 best=preferred;float distance=float.PositiveInfinity;
            for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
            {
                Vector2 p=new Vector2(x+.5f,y+.5f);float d=(p-preferred).sqrMagnitude;
                if(d<distance&&Fits(source,p,width)){best=p;distance=d;}
            }
            if(float.IsPositiveInfinity(distance))throw new InvalidOperationException("No open space fits this footprint. Reset or choose a smaller footprint.");
            return best;
        }
    }

    public enum DigBrush { Single, ThreeWide, Radius }
    public static class PreviewDigBrush
    {
        public static void Cells(Vector2 target,DigBrush brush,float radius,IOccupancySource source,List<DualTile> output)
        {
            output.Clear();int cx=Mathf.FloorToInt(target.x),cy=Mathf.FloorToInt(target.y);
            int r=brush==DigBrush.Single?0:brush==DigBrush.ThreeWide?1:Mathf.CeilToInt(Mathf.Clamp(radius,.5f,12));
            for(int y=cy-r;y<=cy+r;y++)for(int x=cx-r;x<=cx+r;x++)
            {
                if(x<0||y<0||x>=source.Width||y>=source.Height)continue;
                if(brush==DigBrush.Radius&&(new Vector2(x+.5f,y+.5f)-new Vector2(cx+.5f,cy+.5f)).sqrMagnitude>radius*radius+.0001f)continue;
                output.Add(new DualTile(x,y));
            }
        }
        public static bool TooSmall(DigBrush brush,float radius,float body)=>brush==DigBrush.Radius?radius<body*.707107f:(brush==DigBrush.Single?1:3)<body;
    }

    public sealed class PreviewEditSession
    {
        public readonly PlayableTerrain Terrain;
        public readonly ContourTerrainCache Cache;
        public readonly Dictionary<DualTile,PreviewMeshData[]> Patches=new Dictionary<DualTile,PreviewMeshData[]>();
        public readonly List<DualTile> LastDirty=new List<DualTile>();
        public readonly List<DualTile> Changed=new List<DualTile>();
        readonly PreviewMeshWorkspace workspace=new PreviewMeshWorkspace();
        readonly HashSet<DualTile> dirty=new HashSet<DualTile>();
        public int PatchSize {get;private set;}
        public int DigCount {get;private set;}
        public double LastMilliseconds,TotalMilliseconds,WorstMilliseconds;
        public long LastAllocatedBytes,TotalAllocatedBytes;
        public double AverageMilliseconds=>DigCount==0?0:TotalMilliseconds/DigCount;
        public int OrientationEvaluations=>Cache.Orientation.Evaluations;
        public int LastPatchCount=>LastDirty.Count;
        public PreviewEditSession(string fixture,int patchSize=16)
        {Terrain=new PlayableTerrain(fixture);Cache=new ContourTerrainCache(Terrain);Partition(patchSize);}
        public void Partition(int size)
        {
            if(size!=8&&size!=16&&size!=32)throw new ArgumentOutOfRangeException(nameof(size));
            PatchSize=size;Patches.Clear();FullRebuild();
        }
        public void FullRebuild()
        {
            Cache.Clear();LastDirty.Clear();
            for(int y=0;y<Terrain.Height-1;y+=PatchSize)for(int x=0;x<Terrain.Width-1;x+=PatchSize)
            {var key=new DualTile(x,y);Patches.TryGetValue(key,out var reuse);Patches[key]=PreviewMeshBuilder.BuildPatch(Terrain,Cache,x,y,PatchSize,reuse,workspace);LastDirty.Add(key);}
        }
        public bool Dig(IReadOnlyList<DualTile> requested,bool fullComparison=false)
        {
            long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            using(GeometryTrace.Measure(GeometryPhase.Edit)) {
            Changed.Clear();LastDirty.Clear();dirty.Clear();
            foreach(var cell in requested)if(Terrain.Excavate(cell))Changed.Add(cell);
            if(Changed.Count==0){LastMilliseconds=0;LastAllocatedBytes=0;return false;}
            }
            // Invalidate all cells first, then rebuild each owned patch once for the entire action.
            Cache.InvalidateCells(Changed);
            using(GeometryTrace.Measure(GeometryPhase.DirtyRegion)) {
            foreach(var tile in Cache.LastInvalidatedTiles)
                dirty.Add(new DualTile(tile.X/PatchSize*PatchSize,tile.Y/PatchSize*PatchSize));
            }
            if(fullComparison)FullRebuild();
            else
            {
                LastDirty.AddRange(dirty);LastDirty.Sort((a,b)=>a.Y==b.Y?a.X.CompareTo(b.X):a.Y.CompareTo(b.Y));
                foreach(var key in LastDirty)Patches[key]=PreviewMeshBuilder.BuildPatch(Terrain,Cache,key.X,key.Y,PatchSize,Patches[key],workspace);
            }
            timer.Stop();LastMilliseconds=timer.Elapsed.TotalMilliseconds;LastAllocatedBytes=allocated==0?-1:GC.GetAllocatedBytesForCurrentThread()-allocated;
            DigCount++;TotalMilliseconds+=LastMilliseconds;WorstMilliseconds=Math.Max(WorstMilliseconds,LastMilliseconds);TotalAllocatedBytes=LastAllocatedBytes<0?-1:TotalAllocatedBytes+LastAllocatedBytes;return true;
        }
        public void Reset(){Terrain.Reset();FullRebuild();Changed.Clear();DigCount=0;LastMilliseconds=TotalMilliseconds=WorstMilliseconds=0;TotalAllocatedBytes=LastAllocatedBytes=0;}
    }
}
