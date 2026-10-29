using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Geometry.Preview;using SporeGobbo.Input;using SporeGobbo.CampLifecycle;
/// <summary>Authored expedition policy over the same physical authority, collider publisher and presenter as Camp.</summary>
[DefaultExecutionOrder(-1800)]public sealed class ExpeditionRegion:MonoBehaviour,IWorldRegion,IRegionNavigation
{
 public Shader terrainShader,spriteShader,landmarkShader;public GameObject playerPrefab,buddyPrefab,weevilPrefab,blobPrefab,sporePrefab,shinyPrefab,foodPrefab,retreatPrefab,exitPrefab;
 public ExpeditionLayout Definition{get;private set;}public RegionTerrainState State{get;private set;}public CaveDiscovery Discovery{get;private set;}public WorldRegionPresentation Presentation{get;private set;}
 public IReadOnlyList<Stage59ActorView> PresentedViews=>views;
 public GobboController Player{get;private set;}public Camera RegionCamera{get;private set;}public float CellSize=>.6f;public int Revision=>State?.Revision??-1;
 public bool LogicalReady=>!disposed&&State!=null&&published==Revision;public bool PresentationReady=>LogicalReady&&Presentation!=null&&Presentation.ViewAvailable;
 public Transform PresentationRoot=>transform;public Shader ActorShader=>spriteShader;public FocusProjection Projection=>Presentation?.Snapshot;public Stage4Settings PresentationSettings=>Presentation.settings;
 public Vector2 Arrival=>World(Definition.Anchors["arrival"]);public int TestDigPower{get;set;}=1;
 public int Excavations{get;private set;}public int RejectedResistance{get;private set;}public double InitMs,CollisionMs;public int ActiveEncounters{get;private set;}
 RegionTerrainPhysics physics;Func<IEnumerable<DualTile>,int,Func<DualTile,bool>,int> commit;int published=-1;bool disposed;
 readonly List<Stage59ActorView> views=new List<Stage59ActorView>();readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
 readonly List<(string id,GameObject prefab,Point2 point)> waiting=new List<(string,GameObject,Point2)>();
 readonly HashSet<string> spawned=new HashSet<string>();
 void Awake()
 {
  if(!GameSceneCatalog.IsExpedition(gameObject.scene))throw new InvalidOperationException("Expedition scene role is invalid");
  SampleSceneModeController.EnterAuthoredRun();
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Region.Initialize").Auto();
  long start=System.Diagnostics.Stopwatch.GetTimestamp();Definition=new ExpeditionLayout();State=new RegionTerrainState("expedition",ExpeditionLayout.Width,ExpeditionLayout.Height,Definition,ExpeditionLayout.Seed);commit=State.TakeMutationControl();physics=new RegionTerrainPhysics(State,transform);published=Revision;
  Discovery=new CaveDiscovery(State.World,new DiscoveryIndex(State.World,Definition.Concealed)){RevealDistance=12,MaxWaveCells=512,MaxReleases=1024,Duration=.9};Discovery.SeedKnown(Definition.InitialKnown);
  RegionCamera=new GameObject("Shared projected region camera").AddComponent<Camera>();RegionCamera.tag="MainCamera";RegionCamera.orthographic=true;RegionCamera.clearFlags=CameraClearFlags.SolidColor;RegionCamera.gameObject.AddComponent<AudioListener>();
  var palette=ScriptableObject.CreateInstance<Stage55Palette>();owned.Add(palette);RegionCamera.backgroundColor=palette.Color("VoidBlackGreen");
  var surface=new Texture2D(ExpeditionLayout.Width,ExpeditionLayout.Height,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(surface);var pixels=new Color[ExpeditionLayout.Width*ExpeditionLayout.Height];for(int y=0;y<ExpeditionLayout.Height;y++)for(int x=0;x<ExpeditionLayout.Width;x++)pixels[y*ExpeditionLayout.Width+x]=Definition.Protected(x,y)?Color.white:y>=106&&y<=107&&x>=80&&x<=93?new Color(0,Definition.Resistance(x,y)/3f,0):Color.black;surface.SetPixels(pixels);surface.Apply();
  string[] names={"FloorSlateBlue","UndugDirtMass","DigWallUpper","FreshFractureEdge","PermanentMossBlack","FloorPlumShadow","SicklyLichen"};var layers=new Material[names.Length];for(int i=0;i<layers.Length;i++){var m=new Material(terrainShader);owned.Add(m);m.SetColor("_Color",palette.Color(names[i]));m.SetFloat("_Layer",i);m.SetVector("_RegionSize",new Vector4(ExpeditionLayout.Width,ExpeditionLayout.Height,0,0));m.SetTexture("_CampSurface",surface);m.SetColor("_Permanent",palette.Color("PermanentMossBlack"));m.SetColor("_PermanentFace",palette.Color("PermanentWallFace"));m.SetColor("_WallUpper",palette.Color("DigWallUpper"));m.SetColor("_WallLower",palette.Color("DigWallLower"));m.SetColor("_Foot",palette.Color("WallFootOcclusion"));layers[i]=m;}
  Presentation=gameObject.AddComponent<WorldRegionPresentation>();Presentation.AllowPartialView=true;Presentation.ForegroundSortingOrder=120;Presentation.PhysicalToLogical=physical=>{var logical=LogicalPosition(physical);return new Vector2((float)logical.X,(float)logical.Y);};var a=Definition.Anchors["arrival"];Presentation.Initialize(State.World,layers,terrainShader,RegionCamera,new Vector2((float)a.X,(float)a.Y),new DiscoveredTerrainPresentation(Discovery),true);
  State.Committed+=Publish;DiggableTerrainService.Register(this);
  var squad=gameObject.AddComponent<RunSquadSpawner>();squad.buddyPrefab=buddyPrefab;squad.spawnOnStart=false;var initializer=gameObject.AddComponent<RunStartInitializer>();initializer.playerPrefab=playerPrefab;initializer.runSquadSpawner=squad;
  waiting.Add(("weevil.a",weevilPrefab,new Point2(67.5,92.5)));waiting.Add(("weevil.b",weevilPrefab,new Point2(75.5,94.5)));waiting.Add(("weevil.hidden",weevilPrefab,new Point2(108.5,113.5)));waiting.Add(("blob",blobPrefab,new Point2(113.5,58.5)));
  Place(sporePrefab,Definition.Anchors["nursery"]+new Point2(2,0),"spore.nursery");Place(sporePrefab,new Point2(102.5,101.5),"spore.fossil");
  int shinyIndex=0,foodIndex=0;
  foreach(var p in new[]{new Point2(37.5,92.5),new Point2(29.5,65.5),new Point2(103.5,98.5)})Place(shinyPrefab,p,"shiny."+shinyIndex++);
  foreach(var p in new[]{new Point2(28.5,60.5),new Point2(65.5,96.5),new Point2(119.5,80.5)})Place(foodPrefab,p,"food."+foodIndex++);
  Place(retreatPrefab,Definition.Anchors["arrival"]+new Point2(-6,0),"retreat");Place(exitPrefab,Definition.Anchors["exit"],"normal.exit");
  gameObject.AddComponent<ExpeditionLandmarks>().Initialize(this,palette);gameObject.AddComponent<ExpeditionDevelopmentControls>();InitMs=StreamingGeometry.Elapsed(start);
 }
 System.Collections.IEnumerator Start(){while(Player==null||!PresentationReady)yield return null;var state=GameState.Instance;if(state!=null&&MushWormIntroEligibility.IsEligible(state.currentRunNumber,state.storyProgress,true))MushWormIntroRuntime.Create(this,new RegionStoryLocation{markerKey=MushWormIntroRuntime.MarkerKey,actorCell=new Vector2Int(62,29),focusCell=new Vector2Int(62,33),triggerCell=new Vector2Int(60,33),despawnCell=new Vector2Int(69,30),departureWaypoints=new List<Vector2Int>{new Vector2Int(65,27),new Vector2Int(68,28)}});}
 public Point2 LogicalPosition(Vector2 p)=>physics.Logical(p);public Vector2 World(Point2 p)=>physics.World(p);public Vector3 Render(Vector2 p)=>Presentation.Project(LogicalPosition(p));
 public Vector2Int WorldToCell(Vector2 p){var q=LogicalPosition(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}public Vector2 CellToWorld(Vector2Int c)=>World(new Point2(c.x+.5,c.y+.5));
 public bool IsBlocked(Vector2Int c)=>!LogicalReady||State.World.IsSolid(c.x,c.y);public bool IsDiggable(Vector2Int c)=>LogicalReady&&!Definition.Protected(c.x,c.y)&&State.World.IsSolid(c.x,c.y);
 public bool RayClear(Vector2 a,Vector2 b,float radius=0)=>LogicalReady&&TileMover.CanTraverseBox(this,a,b,Vector2.one*Math.Max(.005f,radius));
 public List<Vector2> Route(Vector2 a,Vector2 b,Vector2 extents)=>LogicalReady?physics.Route(this,a,b,extents):new List<Vector2>();
 public bool DerivedCollider(Collider2D c)=>physics!=null&&physics.Owns(c);
 public void DigCircle(Vector2 p,float radius)=>DigCircle(p,radius,TerrainDigAuthority.Player,0,null);
 public TerrainDigResult DigCircle(Vector2 p,float radius,TerrainDigAuthority authority,int stage,IReadOnlyCollection<Vector2Int> authorized)
 {
  if(!LogicalReady||Time.timeScale<=0||SporeInputReader.Instance==null||SporeInputReader.Instance.Context!=SporeInputContext.Gameplay||!float.IsFinite(radius)||radius<=0)return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
  var center=WorldToCell(p);int range=Mathf.CeilToInt(radius/CellSize)+1,evaluated=0;var cells=new List<DualTile>();for(int y=center.y-range;y<=center.y+range;y++)for(int x=center.x-range;x<=center.x+range;x++){var c=new Vector2Int(x,y);if((CellToWorld(c)-p).sqrMagnitude>radius*radius)continue;evaluated++;if(!IsDiggable(c))continue;if(Definition.Resistance(x,y)>TestDigPower){RejectedResistance++;continue;}cells.Add(new DualTile(x,y));}
  int n=commit(cells,Revision,c=>true);Excavations+=n;return new TerrainDigResult(evaluated,cells.Count,n,n>0?TerrainDigFailureReason.None:TerrainDigFailureReason.NoDirtRemoved);
 }
 void Publish(IReadOnlyList<DualTile> cells,int revision){long start=System.Diagnostics.Stopwatch.GetTimestamp();physics.Publish(cells);CollisionMs=StreamingGeometry.Elapsed(start);Presentation.Stream.RefreshPhysicalCells(cells);published=revision;}
 public void BindNavigation(ActorTerrainBinding binding)=>physics.BindNavigation(binding,this);
 public void BindActor(GameObject actor)
 {
  RegionActorViews.Bind(this,RegionCamera,views,actor,()=>LogicalReady&&Projection!=null);
  if(actor.TryGetComponent<GobboController>(out var player))Player=player;
 }
 public void BindVisual(GameObject visual)
 =>RegionActorViews.BindVisual(this,views,visual);
 void Place(GameObject prefab,Point2 p,string id){if(prefab==null)throw new InvalidOperationException("Missing real prefab "+id);var position=World(p);if(!TileMover.CanOccupyBox(this,position,Vector2.one*.12f))throw new InvalidOperationException("Invalid content anchor "+id);var go=Instantiate(prefab,position,Quaternion.identity);go.name=id;if(id.StartsWith("shiny.",StringComparison.Ordinal))RunContentSpawner.EnsureShinyPickup(go);BindVisual(go);}
 void LateUpdate()
 {
  if(Player==null)return;var p=LogicalPosition(Player.transform.position);using(new Unity.Profiling.ProfilerMarker("Spore.Expedition.Discovery").Auto()){Discovery.ProcessChanges(p);Discovery.Explore(p);if(Discovery.Tick(Time.deltaTime)>0)Presentation.Stream.QueuePresentation(Discovery.Released);}Presentation.Refresh(new Vector2((float)p.X,(float)p.Y),Time.deltaTime);
  foreach(var entry in waiting){if(spawned.Contains(entry.id)||entry.id.EndsWith(".hidden",StringComparison.Ordinal)&&!Presentation.Stream.Resident.ContainsKey(new DualTile((int)entry.point.X/16,(int)entry.point.Y/16))||!Discovery.Shown((int)entry.point.X,(int)entry.point.Y)||!Presentation.Stream.IsPresentationCurrent(new DualTile((int)entry.point.X/16,(int)entry.point.Y/16)))continue;var go=Instantiate(entry.prefab,World(entry.point),Quaternion.identity);go.name=entry.id;BindActor(go);spawned.Add(entry.id);ActiveEncounters++;}
  RegionActorViews.Draw(views);
 }
 void OnDisable()=>DiggableTerrainService.Unregister(this);
 void OnDestroy(){disposed=true;DiggableTerrainService.Unregister(this);if(State!=null)State.Committed-=Publish;Discovery?.Dispose();physics?.Dispose();foreach(var v in views)if(v!=null)v.Release();if(RegionCamera!=null)Destroy(RegionCamera.gameObject);foreach(var o in owned)if(o!=null)Destroy(o);}
}
