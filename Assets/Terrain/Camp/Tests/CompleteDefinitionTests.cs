using System;
using NUnit.Framework;
using SporeGobbo.Geometry;

namespace SporeGobbo.CampCandidate.Tests
{
    public sealed class CompleteDefinitionTests
    {
        sealed class OpenAuthored : ICompleteTerrainDefinition
        {
            public bool Protected(int x, int y) => false;
            public bool TrySolid(int x, int y, out bool solid) { solid = false; return true; }
        }

        sealed class MissingCell : ICompleteTerrainDefinition
        {
            public bool Protected(int x, int y) => false;
            public bool TrySolid(int x, int y, out bool solid) { solid = false; return x != 12 || y != 13; }
        }

        [Test]
        public void CompleteOpenDefinitionOverridesStressCellsAndDefaultBoundaryLocks()
        {
            var world = new StreamingWorld(192, 192, 56056, new OpenAuthored());
            Assert.That(world.IsSolid(63, 41), Is.False, "This interior cell is locked by the legacy stress fixture.");
            Assert.That(world.Locked(63, 41), Is.False);
            Assert.That(world.IsSolid(0, 0), Is.False, "Authored borders are defined by their source, not injected.");
            Assert.That(world.IsSolid(-1, 0), Is.True, "Out-of-world queries remain blocked.");
        }

        [Test]
        public void CompleteDefinitionCannotSilentlyFallBackForAnUnspecifiedCell()
        {
            Assert.Throws<InvalidOperationException>(() => new StreamingWorld(192, 192, 56056, new MissingCell()));
        }
    }
}
