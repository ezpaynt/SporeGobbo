using System;

namespace SporeGobbo.Geometry
{
    public enum GeometryPhase
    {
        Dig, Edit, DirtyRegion, CacheInvalidation, WallInvalidation, WallFilter,
        Contour, MeshBuild, Triangulation, MeshUpload, Bounds, Edge, Visibility, Overlay, UI, Navigation, Construction, Count
    }

    /// <summary>Optional diagnostics boundary: the geometry assembly retains no engine dependency.</summary>
    public interface IGeometryTrace
    {
        void Begin(GeometryPhase phase);
        void End(GeometryPhase phase);
    }

    public static class GeometryTrace
    {
        public static IGeometryTrace Sink { get; set; }
        public static Scope Measure(GeometryPhase phase) => new Scope(Sink, phase);
        public readonly struct Scope : IDisposable
        {
            readonly IGeometryTrace sink;
            readonly GeometryPhase phase;
            public Scope(IGeometryTrace sink, GeometryPhase phase)
            { this.sink=sink;this.phase=phase;sink?.Begin(phase); }
            public void Dispose() => sink?.End(phase);
        }
    }
}
