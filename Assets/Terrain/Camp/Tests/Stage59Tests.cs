using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Geometry;

namespace SporeGobbo.CampCandidate.Tests
{
    public sealed class Stage59Tests
    {
        static Type AuthorityType=>Type.GetType("Stage59Authority, Assembly-CSharp",true);
        static Type BindingType=>Type.GetType("ActorTerrainBinding, Assembly-CSharp",true);
        static object New(CampSession session)=>Activator.CreateInstance(AuthorityType,new object[]{session});
        static object Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method).Invoke(owner,args);
        static Vector2 World(object a,Point2 p)=>(Vector2)Call(a,"ToWorld",p);
        [Test]public void PhysicalCellCentersAndYDirectionMatchExistingGridContract()
        {
            var session=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(session))
            foreach(var p in new[]{new Point2(.5,.5),new Point2(84.5,117.5),new Point2(191.5,191.5)})
            {var world=World(a,p);var restored=(Point2)Call(a,"ToLogical",world);Assert.That(restored.X,Is.EqualTo(p.X).Within(.00002));Assert.That(restored.Y,Is.EqualTo(p.Y).Within(.00002));var cell=(Vector2Int)Call(a,"WorldToCell",world);Assert.That(cell.x,Is.EqualTo((int)p.X));Assert.That(cell.y,Is.EqualTo((int)p.Y));Assert.That((Vector2)Call(a,"CellToWorld",cell),Is.EqualTo(world));}
        }
        [Test]public void ActorTerrainAdapterReadsEveryCellFromTheSingleSessionAuthority()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            for(int y=-1;y<=192;y++)for(int x=-1;x<=192;x++)Assert.That((bool)Call(a,"IsBlocked",new Vector2Int(x,y)),Is.EqualTo(s.Terrain.IsSolid(x,y)));
        }
        [Test]public void OrdinaryEditMutatesSessionButProtectedStationDoesNot()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            {var dig=AuthorityType.GetMethod("DigCircle",new[]{typeof(Vector2),typeof(float)});dig.Invoke(a,new object[]{World(a,new Point2(84.5,111.5)),.1f});Assert.That(s.Revision,Is.Zero);int x=0,y=0;for(int i=0;i<192*192;i++)if(s.Definition.Category(i%192,i/192)==CampCell.Expansion&&s.Terrain.IsSolid(i%192,i/192)){x=i%192;y=i/192;break;}dig.Invoke(a,new object[]{World(a,new Point2(x+.5,y+.5)),.1f});Assert.That(s.Revision,Is.EqualTo(1));Assert.That(s.Terrain.IsSolid(x,y),Is.False);}
        }
        [Test]public void EditPermissionRejectsBeforeAnyTerrainMutation()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            {AuthorityType.GetField("AllowEdits").SetValue(a,(Func<bool>)(()=>false));AuthorityType.GetMethod("DigCircle",new[]{typeof(Vector2),typeof(float)}).Invoke(a,new object[]{World(a,new Point2(84.5,127.5)),3f});Assert.That(s.Revision,Is.Zero);}
        }
        [Test]public void RouteWorkspaceProducesPhysicallyClearSegmentsForActualBodyBox()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            {var from=World(a,s.Definition.Marker("fire").Interaction);var to=World(a,s.Definition.Marker("squad").Interaction);var route=(System.Collections.Generic.List<Vector2>)Call(a,"Route",from,to,new Vector2(.5f,.5f));Assert.That(route.Count,Is.GreaterThan(0));var mover=Type.GetType("TileMover, Assembly-CSharp",true).GetMethod("CanTraverseBox");Vector2 prev=from;foreach(var p in route){Assert.That((bool)mover.Invoke(null,new object[]{a,prev,p,new Vector2(.5f,.5f)}));prev=p;}}
        }
        [Test]public void RouteRejectsSolidDestinationRatherThanPassingWithoutMapGenerator()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            {var route=(System.Collections.Generic.List<Vector2>)Call(a,"Route",World(a,s.Definition.Marker("fire").Interaction),World(a,new Point2(84.5,111.5)),new Vector2(.5f,.5f));Assert.That(route,Is.Empty);}
        }
        [Test]public void BoundRouteStampRejectsTerrainRevisionChanges()
        {
            var s=new CampSession(Stage57Layout.Create());var go=new GameObject("Stage59 isolated route stamp test");
            using(var a=(IDisposable)New(s))try{var binding=go.AddComponent(BindingType);Call(binding,"Bind",a);BindingType.GetField("ReadRevision").SetValue(binding,(Func<int>)(()=>s.Revision));Assert.That((bool)Call(binding,"AcceptsRoute",0));AuthorityType.GetMethod("DigCircle",new[]{typeof(Vector2),typeof(float)}).Invoke(a,new object[]{World(a,new Point2(84.5,127.5)),.2f});Assert.That(s.Revision,Is.EqualTo(1));Assert.That((bool)Call(binding,"AcceptsRoute",0),Is.False);}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void BindingNeverRegistersGlobalTerrain()
        {
            var global=Type.GetType("DiggableTerrainService, Assembly-CSharp",true).GetProperty("Active");var before=global.GetValue(null);var go=new GameObject("Stage59 isolated binding test");
            using(var a=(IDisposable)New(new CampSession(Stage57Layout.Create())))try{var b=go.AddComponent(BindingType);Call(b,"Bind",a);Assert.That(global.GetValue(null),Is.SameAs(before));Assert.That(BindingType.GetMethod("Resolve").Invoke(null,new object[]{go}),Is.SameAs(a));}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void CollisionCleanupUnsubscribesAndDisposesTwice()
        {
            var s=new CampSession(Stage57Layout.Create());var a=New(s);var parent=new GameObject("Stage59 collision lifetime test");
            try{Call(a,"CreateCollision",parent.transform);Assert.That(parent.GetComponentsInChildren<PolygonCollider2D>().Length,Is.EqualTo(144));((IDisposable)a).Dispose();((IDisposable)a).Dispose();Assert.That(parent.GetComponentsInChildren<PolygonCollider2D>(),Is.Empty);Assert.That(typeof(CampSession).GetField("Committed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s),Is.Null);AuthorityType.GetMethod("DigCircle",new[]{typeof(Vector2),typeof(float)}).Invoke(a,new object[]{World(a,new Point2(84.5,127.5)),1f});Assert.That((int)AuthorityType.GetProperty("PublishedRevision").GetValue(a),Is.Zero);}finally{((IDisposable)a).Dispose();UnityEngine.Object.DestroyImmediate(parent);}
        }
        [Test]public void FractionalWallContactConnectsToSafeGridAnchorBeforeNavigation()
        {
            var s=new CampSession(Stage57Layout.Create());using(var a=(IDisposable)New(s))
            {var from=new Vector2(52.69097f,47.70639f);var extents=new Vector2(.28f,.28f);var mover=Type.GetType("TileMover, Assembly-CSharp",true).GetMethod("CanOccupyBox");Assert.That((bool)mover.Invoke(null,new object[]{a,from,extents}));var route=(System.Collections.Generic.List<Vector2>)Call(a,"Route",from,World(a,new Point2(75.5,111.5)),extents);Assert.That(route.Count,Is.GreaterThan(0));}
        }
        [Test]public void UnboundExplicitActorDependencyFailsClosedInsteadOfUsingGlobalFallback()
        {
            var go=new GameObject("Stage59 unfinished binding test");try{var body=go.AddComponent<Rigidbody2D>();go.AddComponent(BindingType);Type.GetType("TileMover, Assembly-CSharp",true).GetMethod("Move").Invoke(null,new object[]{body,new Vector2(4,0),.3f});Assert.That(body.linearVelocity,Is.EqualTo(Vector2.zero));}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void ActorScaleKeepsPhysicalSizesIndependentOfCameraParameters()
        {
            var grid=new LogicalGridTransform(new Point2(0,0),.6,192);var point=new Point2(84.5,117.5);var world=grid.ToWorld(point);
            foreach(double sideDistance in new[]{45d,67.5}){var p=new FocusProjection(new Point2(84,117),52,28,30,.55,.12,1.9,new Point2(0,10),false,sideDistance,24,12.5,.75);var q=p.Unproject(p.Project(grid.ToLogical(world)));Assert.That(q.X,Is.EqualTo(point.X).Within(1e-9));Assert.That(q.Y,Is.EqualTo(point.Y).Within(1e-9));Assert.That(grid.CellSize,Is.EqualTo(.6));}
        }
    }
}
