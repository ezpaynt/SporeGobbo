using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Geometry.Preview;using SporeGobbo.Input;using SporeGobbo.CampLifecycle;
/// <summary>Intro policy over the shared region state/physics/presentation, not a terrain engine or campaign owner.</summary>
[DefaultExecutionOrder(-1800)]public sealed class IntroRegion:MonoBehaviour,IWorldRegion
{
 public Shader terrainShader,spriteShader,landmarkShader;public GameObject playerPrefab;
 public IntroLayout Definition{get;private set;}public RegionTerrainState State{get;private set;}public CaveDiscovery Discovery{get;private set;}public WorldRegionPresentation Presentation{get;private set;}
 public GobboController Player{get;private set;}public Camera RegionCamera{get;private set;}public IntroSequence Sequence{get;private set;}public IntroCampTransition ArrivalTransition{get;private set;}
 public float CellSize=>.6f;public int Revision=>State?.Revision??-1;public bool LogicalReady=>!disposed&&State!=null&&published==Revision;
 public bool PresentationReady=>LogicalReady&&Presentation!=null&&Presentation.ViewAvailable;public Transform PresentationRoot=>transform;public Shader ActorShader=>spriteShader;public FocusProjection Projection=>Presentation?.Snapshot;public Stage4Settings PresentationSettings=>Presentation.settings;public Vector2 Arrival=>World(Definition.Birth);
 public bool ControlReady=>Sequence!=null&&Sequence.ControlReleased&&!Sequence.Arriving;
 public int Excavations{get;private set;}public int Placements{get;private set;}public int RejectedPlacements{get;private set;}
 public event Action<string,Vector2> SoundCue;public readonly List<string> Cues=new List<string>();
 public readonly List<Stage59ActorView> Views=new List<Stage59ActorView>();readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
 Material floorMaterial;Color floorBase;Texture2D surfaceTexture;
 RegionTerrainPhysics physics;Func<IEnumerable<DualTile>,int,Func<DualTile,bool>,int> dig;Func<TerrainPlacementRequest,Func<IReadOnlyList<DualTile>,bool>,TerrainPlacementResult> place;int published=-1;bool disposed;
 void Awake()
 {
  if(!GameSceneCatalog.DevelopmentCandidate||!GameSceneCatalog.AuthoredIntroEnabled||!SporeGobbo.Safety.Persistence.Isolated)throw new InvalidOperationException("Intro requires protected candidate entry");
  SampleSceneModeController.EnterAuthoredIntro();using var profile=new Unity.Profiling.ProfilerMarker("Spore.Intro.Initialize").Auto();
  Definition=new IntroLayout();State=new RegionTerrainState("intro",IntroLayout.Width,IntroLayout.Height,Definition,IntroLayout.Seed);dig=State.TakeMutationControl();place=State.TakePlacementControl();physics=new RegionTerrainPhysics(State,transform);published=Revision;
  Discovery=new CaveDiscovery(State.World,new DiscoveryIndex(State.World,Definition.Concealed)){RevealDistance=9,MaxWaveCells=128,MaxReleases=256,Duration=.9};Discovery.SeedKnown(Definition.Known);
  RegionCamera=new GameObject("Shared projected Intro camera").AddComponent<Camera>();RegionCamera.tag="MainCamera";RegionCamera.orthographic=true;RegionCamera.clearFlags=CameraClearFlags.SolidColor;RegionCamera.gameObject.AddComponent<AudioListener>();
  var palette=ScriptableObject.CreateInstance<Stage55Palette>();owned.Add(palette);RegionCamera.backgroundColor=palette.Color("VoidBlackGreen");
  var surface=new Texture2D(IntroLayout.Width,IntroLayout.Height,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(surface);surfaceTexture=surface;var pixels=new Color[IntroLayout.Width*IntroLayout.Height];for(int y=0;y<IntroLayout.Height;y++)for(int x=0;x<IntroLayout.Width;x++)pixels[y*IntroLayout.Width+x]=SurfacePermanent(x,y)?Color.white:Color.black;surface.SetPixels(pixels);surface.Apply();
  string[] names={"FloorSlateBlue","UndugDirtMass","DigWallUpper","FreshFractureEdge","PermanentMossBlack","FloorPlumShadow","SicklyLichen"};var layers=new Material[names.Length];for(int i=0;i<layers.Length;i++){var m=new Material(terrainShader);owned.Add(m);m.SetColor("_Color",palette.Color(names[i]));m.SetFloat("_Layer",i);m.SetVector("_RegionSize",new Vector4(IntroLayout.Width,IntroLayout.Height,0,0));m.SetTexture("_CampSurface",surface);m.SetColor("_Permanent",palette.Color("PermanentMossBlack"));m.SetColor("_PermanentFace",palette.Color("PermanentWallFace"));m.SetColor("_WallUpper",palette.Color("DigWallUpper"));m.SetColor("_WallLower",palette.Color("DigWallLower"));m.SetColor("_Foot",palette.Color("WallFootOcclusion"));layers[i]=m;}
  floorMaterial=layers[0];floorBase=floorMaterial.color;
  Presentation=gameObject.AddComponent<WorldRegionPresentation>();Presentation.AllowPartialView=true;Presentation.ForegroundSortingOrder=120;Presentation.PhysicalToLogical=p=>{var q=LogicalPosition(p);return new Vector2((float)q.X,(float)q.Y);};Presentation.Initialize(State.World,layers,terrainShader,RegionCamera,new Vector2((float)Definition.Birth.X,(float)Definition.Birth.Y),new DiscoveredTerrainPresentation(Discovery),true);
  State.Committed+=Publish;DiggableTerrainService.Register(this);Sequence=gameObject.AddComponent<IntroSequence>();Sequence.Initialize(this);ArrivalTransition=gameObject.AddComponent<IntroCampTransition>();ArrivalTransition.shakeStrength=0;ArrivalTransition.rumbleSeconds=0;ArrivalTransition.fadeSeconds=.7f;
  gameObject.AddComponent<IntroLandmarks>().Initialize(this,palette);var initializer=gameObject.AddComponent<RunStartInitializer>();initializer.playerPrefab=playerPrefab;
 }
 public void Cue(string id,Vector2 ground){if(Cues.Count<256)Cues.Add(id);SoundCue?.Invoke(id,ground);}
 public Point2 LogicalPosition(Vector2 p)=>physics.Logical(p);public Vector2 World(Point2 p)=>physics.World(p);public Vector3 Render(Vector2 p)=>Presentation.Project(LogicalPosition(p));
 public Vector2Int WorldToCell(Vector2 p){var q=LogicalPosition(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}public Vector2 CellToWorld(Vector2Int c)=>World(new Point2(c.x+.5,c.y+.5));
 public bool IsBlocked(Vector2Int c)=>!LogicalReady||State.World.IsSolid(c.x,c.y);public bool IsDiggable(Vector2Int c)=>LogicalReady&&!Definition.Protected(c.x,c.y)&&State.World.IsSolid(c.x,c.y);
 public bool RayClear(Vector2 a,Vector2 b,float radius=0)=>LogicalReady&&TileMover.CanTraverseBox(this,a,b,Vector2.one*Math.Max(.005f,radius));public List<Vector2> Route(Vector2 a,Vector2 b,Vector2 extent)=>LogicalReady?physics.Route(this,a,b,extent):new List<Vector2>();
 public void BindActor(GameObject actor)
 {
  RegionActorViews.Bind(this,RegionCamera,Views,actor,()=>LogicalReady&&ControlReady&&Projection!=null);
  if(actor.TryGetComponent<GobboController>(out var player)){if(Player!=null&&Player!=player)throw new InvalidOperationException("Duplicate Intro player");Player=player;player.gameObject.AddComponent<IntroNonlethalProtection>();Sequence.BindPlayer(player);}
 }
 public void BindVisual(GameObject visual)=>RegionActorViews.BindVisual(this,Views,visual);
 public void DigCircle(Vector2 p,float radius)=>DigCircle(p,radius,TerrainDigAuthority.Player,0,null);
 public TerrainDigResult DigCircle(Vector2 p,float radius,TerrainDigAuthority authority,int stage,IReadOnlyCollection<Vector2Int> authorized)
 {
  if(!LogicalReady||!ControlReady||Time.timeScale<=0||SporeInputReader.Instance==null||SporeInputReader.Instance.Context!=SporeInputContext.Gameplay||!float.IsFinite(radius)||radius<=0)return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
  var center=WorldToCell(p);int range=Mathf.CeilToInt(radius/CellSize)+1,count=0;var cells=new List<DualTile>();for(int y=center.y-range;y<=center.y+range;y++)for(int x=center.x-range;x<=center.x+range;x++){var c=new Vector2Int(x,y);if((CellToWorld(c)-p).sqrMagnitude>radius*radius)continue;count++;if(IsDiggable(c))cells.Add(new DualTile(x,y));}
  int n=dig(cells,Revision,c=>true);Excavations+=n;if(n>0)Cue("dirt.excavation",p);return new TerrainDigResult(count,cells.Count,n,n>0?TerrainDigFailureReason.None:TerrainDigFailureReason.NoDirtRemoved);
 }
 public TerrainPlacementResult Place(string id,IReadOnlyList<DualTile> cells,int expected)
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Intro.TerrainPlacement").Auto();
  if(!LogicalReady||!ReferenceEquals(DiggableTerrainService.Active,this)||!ControlReady)return new TerrainPlacementResult(0,"region not ready/owned");
  var result=place(new TerrainPlacementRequest(State.InstanceId,expected,id,cells),SafePlacement);if(result.Accepted)Placements+=result.Count;else RejectedPlacements++;return result;
 }
 bool SafePlacement(IReadOnlyList<DualTile> cells)
 {
  if(Player==null)return false;foreach(var actor in Views){if(actor==null)continue;var body=actor.GetComponent<Rigidbody2D>();if(body==null)continue;var extent=TileMover.GetMapClearanceExtents(body,.22f);var p=body.position;var next=p+body.linearVelocity*.25f;var lo=Vector2.Min(p,next)-extent-Vector2.one*.05f;var hi=Vector2.Max(p,next)+extent+Vector2.one*.05f;foreach(var c in cells){var a=World(new Point2(c.X,c.Y));var b=World(new Point2(c.X+1,c.Y+1));var min=Vector2.Min(a,b);var max=Vector2.Max(a,b);if(min.x<hi.x&&max.x>lo.x&&min.y<hi.y&&max.y>lo.y)return false;}}
  return IntroPlacementSafety.HasForwardRoute(State.World,Definition,LogicalPosition(Player.transform.position),cells);
 }
 bool Rim(int x,int y)=>x<6||y<6||x>=IntroLayout.Width-6||y>=IntroLayout.Height-6;
 bool SurfacePermanent(int x,int y)=>Definition.Protected(x,y)&&(!Rim(x,y)||!State.World.IsSolid(x-1,y)||!State.World.IsSolid(x+1,y)||!State.World.IsSolid(x,y-1)||!State.World.IsSolid(x,y+1));
 void Publish(IReadOnlyList<DualTile> cells,int revision){physics.Publish(cells);bool changed=false;foreach(var c in cells)for(int y=c.Y-1;y<=c.Y+1;y++)for(int x=c.X-1;x<=c.X+1;x++)if(State.World.Contains(x,y)&&Rim(x,y)&&Definition.Protected(x,y)){surfaceTexture.SetPixel(x,y,SurfacePermanent(x,y)?Color.white:Color.black);changed=true;}if(changed)surfaceTexture.Apply();Presentation.Stream.RefreshPhysicalCells(cells);published=revision;}

 void LateUpdate(){if(Player==null)return;var p=LogicalPosition(Player.transform.position);floorMaterial.color=Color.Lerp(floorBase,new Color(.43f,.38f,.27f),Mathf.Clamp01(((float)p.X-128)/18)*.3f);using(new Unity.Profiling.ProfilerMarker("Spore.Intro.Discovery").Auto()){Discovery.ProcessChanges(p);Discovery.Explore(p);if(Discovery.Tick(Sequence.ClockActive?Time.deltaTime:0)>0)Presentation.Stream.QueuePresentation(Discovery.Released);}Presentation.Refresh(new Vector2((float)p.X,(float)p.Y),Sequence.ClockActive||!Sequence.ControlReleased?Time.deltaTime:0);RegionActorViews.Draw(Views);}
 public void PrepareArrival(){Sequence.StopForArrival();foreach(var rock in GetComponentsInChildren<FallingRockHazard>())rock.Cancel();Cue("camp.arrival",Player.transform.position);}
 void OnDisable()=>DiggableTerrainService.Unregister(this);
 void OnDestroy(){disposed=true;DiggableTerrainService.Unregister(this);if(State!=null)State.Committed-=Publish;Discovery?.Dispose();physics?.Dispose();foreach(var view in Views)if(view!=null)view.Release();if(RegionCamera!=null)Destroy(RegionCamera.gameObject);foreach(var o in owned)if(o!=null)Destroy(o);}
}
