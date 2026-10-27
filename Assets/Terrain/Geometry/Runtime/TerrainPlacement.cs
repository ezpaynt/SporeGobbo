using System;using System.Collections.Generic;
namespace SporeGobbo.Geometry
{
 public sealed class TerrainPlacementRequest
 {public readonly Guid Instance;public readonly int Revision;public readonly string EventId;public readonly IReadOnlyList<DualTile> Cells;public TerrainPlacementRequest(Guid instance,int revision,string id,IReadOnlyList<DualTile> cells){Instance=instance;Revision=revision;EventId=id;Cells=cells;}}
 public readonly struct TerrainPlacementResult
 {public readonly int Count;public readonly string Rejection;public bool Accepted=>Count>0&&Rejection==null;public TerrainPlacementResult(int count,string rejection){Count=count;Rejection=rejection;}}
}
