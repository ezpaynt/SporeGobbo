using System;
using System.Collections.Generic;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
    [DefaultExecutionOrder(900),RequireComponent(typeof(Stage4CameraPreview))]
    public sealed class Stage45MovingDemo:MonoBehaviour
    {
        public Sprite[] referenceSprites;
        public bool paused,showRange=true,showProjectiles=true,showTrajectories=true;
        public float speedFactor=1,actorSpeed=1.8f,attackRange=5,projectileSpeed=8,projectileRange=30;
        public double Clock {get;private set;}
        public double BuildMs {get;private set;}
        public PreviewLogicalRoute[] Routes {get;private set;}
        public PreviewLogicalProjectile[] Shots {get;private set;}
        public Point2[] ActorPositions {get;private set;}
        public int MeshCount=>meshes.Count;
        public int BlockedCount {get{int count=0;if(Shots!=null)foreach(var shot in Shots)if(shot.Blocked)count++;return count;}}
        public const double LogicalFootprint=1;
        readonly List<GameObject> owners=new List<GameObject>();readonly List<Mesh> meshes=new List<Mesh>();
        PreviewMeshData[] actors;MeshFilter[] actorFilters;PreviewMeshData footprints,range,projectiles,trajectories;MeshFilter footprintFilter,rangeFilter,projectileFilter,trajectoryFilter;
        Stage4CameraPreview view;PlayableCavePreview live;bool ready;double lastClock=-1;float oldRange=-1,oldProjectileRange=-1;bool oldShowRange,oldShowShots,oldShowTrajectories;double projectileClock;
        public void Initialize()
        {
            if(ready)return;view=GetComponent<Stage4CameraPreview>();live=GetComponent<PlayableCavePreview>();view.Refresh(0);
            Routes=AuthoredRoutes();ActorPositions=new Point2[4];actors=new PreviewMeshData[4];actorFilters=new MeshFilter[4];
            for(int i=0;i<4;i++){actors[i]=new PreviewMeshData("Moving reference "+i,PreviewLayer.Gobbo);actorFilters[i]=Create(actors[i],22,Color.white);}
            footprints=new PreviewMeshData("Moving logical footprints",PreviewLayer.Gobbo);footprintFilter=Create(footprints,20,new Color(.3f,1,.45f,.8f));
            range=new PreviewMeshData("Five-cell logical range",PreviewLayer.Gobbo);rangeFilter=Create(range,19,new Color(.2f,.9f,1,.7f));
            projectiles=new PreviewMeshData("Logical projectile states",PreviewLayer.Gobbo);projectileFilter=Create(projectiles,23,new Color(1,.35f,.18f,1));
            trajectories=new PreviewMeshData("Logical projectile trajectories",PreviewLayer.Gobbo);trajectoryFilter=Create(trajectories,18,new Color(1,.9f,.5f,.4f));
            Shots=new[]{new PreviewLogicalProjectile(new Point2(55.5,39.5),new Point2(0,-1)),new PreviewLogicalProjectile(new Point2(65.5,39.5),new Point2(1,0)),new PreviewLogicalProjectile(new Point2(90.5,39.5),new Point2(-1,0)),new PreviewLogicalProjectile(new Point2(45.5,24.5),new Point2(1,.35))};
            ready=true;Build();
        }
        MeshFilter Create(PreviewMeshData data,int order,Color color)
        {
            var go=new GameObject(data.Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);owners.Add(go);var mesh=new Mesh{name=data.Name,hideFlags=HideFlags.DontSave};mesh.MarkDynamic();meshes.Add(mesh);
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=live.layerMaterials[(int)PreviewLayer.Gobbo];renderer.sortingOrder=order;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var block=new MaterialPropertyBlock();block.SetColor("_Color",color);renderer.SetPropertyBlock(block);return filter;
        }
        void Update(){if(Application.isPlaying)Advance(Mathf.Min(Time.deltaTime,.1f));}
        public void Advance(double delta)
        {
            Initialize();BuildMs=0;if(!paused){double dt=Math.Max(0,delta)*Math.Max(0,speedFactor);Clock+=dt;projectileClock+=dt;if(projectileClock>=10){projectileClock%=10;foreach(var shot in Shots)shot.Reset();}
                if(showProjectiles)foreach(var shot in Shots)shot.Advance(dt,projectileSpeed,projectileRange,live.Session.Terrain);}
            Build();
        }
        public void Restart(){Initialize();Clock=projectileClock=0;foreach(var shot in Shots)shot.Reset();lastClock=-1;Build();}
        void Build()
        {
            if(lastClock==Clock&&oldRange==attackRange&&oldProjectileRange==projectileRange&&oldShowRange==showRange&&oldShowShots==showProjectiles&&oldShowTrajectories==showTrajectories)return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int i=0;i<4;i++)
            {
                var anchor=Routes[i].AtDistance(Clock*actorSpeed);ActorPositions[i]=anchor;var data=actors[i];data.Clear();data.VisualActorAnchor=anchor;
                Sprite sprite=referenceSprites!=null&&referenceSprites.Length>i?referenceSprites[i]:null;
                if(sprite!=null)
                {
                    var rect=sprite.textureRect;double height=1.3,width=height*rect.width/rect.height;
                    data.Rectangle(anchor.X-width*.5,anchor.Y-height,anchor.X+width*.5,anchor.Y);data.PreviewTexture=sprite.texture;
                    float x0=rect.xMin/sprite.texture.width,x1=rect.xMax/sprite.texture.width,y0=rect.yMin/sprite.texture.height,y1=rect.yMax/sprite.texture.height;
                    data.TextureCoordinates.Add(new Vector2(x0,y1));data.TextureCoordinates.Add(new Vector2(x1,y1));data.TextureCoordinates.Add(new Vector2(x1,y0));data.TextureCoordinates.Add(new Vector2(x0,y1));data.TextureCoordinates.Add(new Vector2(x1,y0));data.TextureCoordinates.Add(new Vector2(x0,y0));
                }
                else
                {
                    data.Rectangle(anchor.X-.35,anchor.Y-1,anchor.X+.35,anchor.Y);
                    data.Triangle(new Point2(anchor.X-.3,anchor.Y-.8),new Point2(anchor.X-.8,anchor.Y-1.1),new Point2(anchor.X-.3,anchor.Y-.4));
                    data.Triangle(new Point2(anchor.X+.3,anchor.Y-.8),new Point2(anchor.X+.8,anchor.Y-1.1),new Point2(anchor.X+.3,anchor.Y-.4));
                }
                view.RegisterUpload(data,actorFilters[i]);
            }
            footprints.Clear();foreach(var p in ActorPositions)Ring(footprints,p.X-.5,p.Y-.5,p.X+.5,p.Y+.5,.035);view.RegisterUpload(footprints,footprintFilter);
            range.Clear();if(showRange)Circle(range,ActorPositions[0],Math.Max(.1,attackRange),.035,96);view.RegisterUpload(range,rangeFilter);
            projectiles.Clear();if(showProjectiles)foreach(var shot in Shots)
            {var p=shot.Position;if(shot.Blocked){Line(projectiles,new Point2(p.X-.17,p.Y-.17),new Point2(p.X+.17,p.Y+.17),.08);Line(projectiles,new Point2(p.X+.17,p.Y-.17),new Point2(p.X-.17,p.Y+.17),.08);}else Circle(projectiles,p,.13,.12,12);}
            view.RegisterUpload(projectiles,projectileFilter);
            if(oldProjectileRange!=projectileRange||oldShowTrajectories!=showTrajectories||lastClock<0)
            {
                trajectories.Clear();if(showTrajectories)foreach(var shot in Shots)
                {int count=(int)Math.Ceiling(projectileRange/.5);for(int i=0;i<count;i++){double a=Math.Min(projectileRange,i*.5),b=Math.Min(projectileRange,(i+1)*.5);Line(trajectories,new Point2(shot.Origin.X+shot.Direction.X*a,shot.Origin.Y+shot.Direction.Y*a),new Point2(shot.Origin.X+shot.Direction.X*b,shot.Origin.Y+shot.Direction.Y*b),.02);}}
                view.RegisterUpload(trajectories,trajectoryFilter);
            }
            lastClock=Clock;oldRange=attackRange;oldProjectileRange=projectileRange;oldShowRange=showRange;oldShowShots=showProjectiles;oldShowTrajectories=showTrajectories;
            BuildMs=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        public static Point2 RangePoint(Point2 anchor,double logicalRange,double angle)=>new Point2(anchor.X+logicalRange*Math.Cos(angle),anchor.Y+logicalRange*Math.Sin(angle));
        public static void Circle(PreviewMeshData data,Point2 center,double radius,double width,int segments)
        {for(int i=0;i<segments;i++)Line(data,RangePoint(center,radius,i*2*Math.PI/segments),RangePoint(center,radius,(i+1)*2*Math.PI/segments),width);}
        public static void Line(PreviewMeshData data,Point2 a,Point2 b,double width)
        {double dx=b.X-a.X,dy=b.Y-a.Y,length=Math.Sqrt(dx*dx+dy*dy);if(length<=1e-10)return;double nx=-dy/length*width*.5,ny=dx/length*width*.5;data.Quad(new Point2(a.X+nx,a.Y+ny),new Point2(b.X+nx,b.Y+ny),new Point2(b.X-nx,b.Y-ny),new Point2(a.X-nx,a.Y-ny));}
        static void Ring(PreviewMeshData d,double x0,double y0,double x1,double y1,double w)
        {d.Rectangle(x0,y0,x1,y0+w);d.Rectangle(x0,y1-w,x1,y1);d.Rectangle(x0,y0+w,x0+w,y1-w);d.Rectangle(x1-w,y0+w,x1,y1-w);}
        public static PreviewLogicalRoute[] AuthoredRoutes()=>new[]{
            new PreviewLogicalRoute(new Point2(54.5,18.5),new Point2(54.5,19.5),new Point2(51.5,19.5),new Point2(51.5,20.5),new Point2(49.5,20.5),new Point2(49.5,21.5),new Point2(48.5,21.5),new Point2(48.5,22.5),new Point2(46.5,22.5),new Point2(46.5,23.5),new Point2(45.5,23.5),new Point2(45.5,25.5),new Point2(44.5,25.5),new Point2(44.5,36.5),new Point2(45.5,36.5),new Point2(45.5,39.5),new Point2(55.5,39.5)),
            new PreviewLogicalRoute(new Point2(55.5,39.5),new Point2(90.5,39.5)),
            new PreviewLogicalRoute(new Point2(55.5,39.5),new Point2(55.5,46.5),new Point2(53.5,46.5),new Point2(53.5,47.5),new Point2(48.5,47.5),new Point2(48.5,48.5),new Point2(47.5,48.5),new Point2(47.5,50.5),new Point2(46.5,50.5),new Point2(46.5,55.5),new Point2(48.5,55.5),new Point2(48.5,58.5)),
            new PreviewLogicalRoute(new Point2(20.5,30.5),new Point2(20.5,31.5),new Point2(43.5,31.5),new Point2(43.5,30.5))};
        public void DrawControls()
        {
            paused=GUILayout.Toggle(paused,"Pause actor/projectile demonstrations");if(GUILayout.Button("Restart logical demonstrations"))Restart();
            GUILayout.Label("Demo speed: "+speedFactor.ToString("F2"));speedFactor=GUILayout.HorizontalSlider(speedFactor,.1f,2);
            showRange=GUILayout.Toggle(showRange,"Logical attack range on upper actor");GUILayout.Label("Logical range: "+attackRange.ToString("F1"));attackRange=GUILayout.HorizontalSlider(attackRange,1,12);
            showProjectiles=GUILayout.Toggle(showProjectiles,"Logical projectiles (X = blocked)");showTrajectories=GUILayout.Toggle(showTrajectories,"Logical trajectory overlays");
            GUILayout.Label("Projectile speed: "+projectileSpeed.ToString("F1"));projectileSpeed=GUILayout.HorizontalSlider(projectileSpeed,1,20);
            GUILayout.Label("Projectile range: "+projectileRange.ToString("F1"));projectileRange=GUILayout.HorizontalSlider(projectileRange,5,45);
            GUILayout.Label($"Demo {Clock:F1}s · {BlockedCount}/4 shots blocked\n1-cell ground rings; logical actors {actorSpeed:F1} cells/s\nStatic Gobbo/Weevil sprites; no AI or combat.");
        }
        void OnDestroy(){foreach(var owner in owners)if(owner!=null){if(Application.isPlaying)Destroy(owner);else DestroyImmediate(owner);}foreach(var mesh in meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
