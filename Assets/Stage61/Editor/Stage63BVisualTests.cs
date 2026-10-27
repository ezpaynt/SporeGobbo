#if UNITY_EDITOR
using System;
using NUnit.Framework;
using SporeGobbo.Geometry;

/// <summary>Pure ground-contact/depth contracts. These are not screenshot or arbitrary wall-mask acceptance.</summary>
public sealed class Stage63BVisualTests
{
    [Test] public void FrontEdgeContactUsesWholePermanentFootprintNotFirstPathSection()
    {
        var footprint=new[]{new DualTile(2,4),new DualTile(6,7),new DualTile(3,5)};
        var anchor=RegionLandmarkDepth.ContactAnchor(footprint,null);
        Assert.AreEqual(4.5,anchor.X);Assert.AreEqual(8,anchor.Y);
        Array.Reverse(footprint);Assert.AreEqual(anchor,RegionLandmarkDepth.ContactAnchor(footprint,null));
    }
    [Test] public void GroundedLandmarkInterleavesNorthAndSouthActors()
    {
        int order=RegionLandmarkDepth.Order(1,2,RegionLandmarkDepth.Treatment.Grounded);
        Assert.Less(RegionLandmarkDepth.ActorOrder(0),order,"Actor north/behind the ground contact is behind the silhouette");
        Assert.Greater(RegionLandmarkDepth.ActorOrder(1),order,"Actor south/in front is drawn in front");
    }
    [Test] public void SideOrEqualDepthActorUsesDeterministicFrontTieWithoutChangingCollider()
    {
        Assert.IsFalse(RegionLandmarkDepth.IsNorth(5,5));
        Assert.IsTrue(RegionLandmarkDepth.IsNorth(5.01f,5));
        Assert.IsFalse(RegionLandmarkDepth.IsNorth(4.99f,5));
        Assert.Greater(RegionLandmarkDepth.ActorOrder(0),RegionLandmarkDepth.Order(0,1,RegionLandmarkDepth.Treatment.Grounded));
    }
    [Test] public void ExplicitRootOverheadSpanStaysAboveBoundedUnderpassActors()
    {
        for(int count=1;count<=12;count++)for(int north=0;north<=count;north++)
            Assert.Greater(RegionLandmarkDepth.Order(north,count,RegionLandmarkDepth.Treatment.OverheadSpan),RegionLandmarkDepth.ActorOrder(count-1));
    }
    [Test] public void ElevatedHeightDoesNotMoveGroundContactDepth()
    {
        ElevatedPath Path(double height)=>new ElevatedPath("same-ground",new[]{new ElevatedSection(new Point2(10,20),0,1,new Point2(1,0)),new ElevatedSection(new Point2(10,20),height,1,new Point2(1,0))});
        var a=RegionLandmarkDepth.ContactAnchor(null,Path(2));var b=RegionLandmarkDepth.ContactAnchor(null,Path(9));
        Assert.AreEqual(a,b);Assert.AreEqual(10,a.X);Assert.AreEqual(20,a.Y);
    }
    [Test] public void AuthoredOverheadPolicyDoesNotInheritEveryLegacyElevatedPathFlag()
    {
        var p=ElevatedShapes.Path("attached-tail",new[]{new Point2(0,0),new Point2(2,0)},new[]{0d,1d},new[]{1d,1d});
        Assert.IsTrue(p.Overhead,"Historical generic Path builder flag is retained, not silently changed");
        Assert.Less(RegionLandmarkDepth.Order(1,2,RegionLandmarkDepth.Treatment.Grounded),RegionLandmarkDepth.ActorOrder(1));
        Assert.Greater(RegionLandmarkDepth.Order(1,2,RegionLandmarkDepth.Treatment.OverheadSpan),RegionLandmarkDepth.ActorOrder(1));
    }
    [Test] public void InvalidGroundMetadataFailsRatherThanSelectingAnArbitraryLiftedPoint()
    {
        Assert.Throws<ArgumentException>(()=>RegionLandmarkDepth.ContactAnchor(Array.Empty<DualTile>(),null));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RegionLandmarkDepth.Order(2,1,RegionLandmarkDepth.Treatment.Grounded));
    }
    [Test] public void VisualDepthCorrectionPreservesAcceptedIntroAndExpeditionOccupancy()
    {
        Assert.AreEqual("26FB81089CADEFA0",new IntroLayout().Fingerprint());
        // Determinism is checked here; the existing Stage62B suite pins the authored expedition's masks/routes.
        Assert.AreEqual(new ExpeditionLayout().Fingerprint(),new ExpeditionLayout().Fingerprint());
    }
}
#endif
