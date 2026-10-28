using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;
using SporeGobbo.Input;
/// <summary>Camp authored-region adapter. Campaign/visit/job ownership stays in existing services.</summary>
[DefaultExecutionOrder(-1800)]
public sealed class SettlementCampRegion:MonoBehaviour,ICampSpatialTerrain,IProjectedActorContext,IRegionActorBinding,IRegionVisualBinding,ICampRegionSnapshotSource,ICampDepartureState,ICampaignTransientActivity
{
 static readonly Unity.Profiling.ProfilerMarker stage61SortingProfile=new Unity.Profiling.ProfilerMarker("Spore.Stage61.Sorting");
 static readonly Unity.Profiling.ProfilerMarker stage61InitializeProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.RegionInitialize");
 static readonly Unity.Profiling.ProfilerMarker stage61CollisionProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.CollisionPublish");
 static readonly Unity.Profiling.ProfilerMarker stage61NavigationProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.Navigation");
 static readonly Unity.Profiling.ProfilerMarker stage61DiggingProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.Digging");

 public Shader terrainShader,spriteShader;public GameObject fightPitWeevil,fightPitBlob;
 Texture2D surfaceTexture;int persistentRevision;object arenaOwner;string arenaPreset="Open";readonly List<DualTile> arenaCells=new List<DualTile>();Func<TerrainPlacementRequest,Func<IReadOnlyList<DualTile>,bool>,TerrainPlacementResult> place;
 public RegionTerrainState State{get;private set;}public AuthoredCamp Definition{get;private set;}
 public WorldRegionPresentation Presentation{get;private set;}public Camera RegionCamera{get;private set;}
 public int TotalResidentialCapacity=>catalog.TotalCapacity;public float CellSize=>.6f;
 public bool LogicalReady=>!disposed&&State!=null&&publishedRevision==State.Revision;
 public bool Ready=>LogicalReady&&Presentation!=null&&Presentation.Ready;
 public bool ResidentialEntranceOpen=>State!=null&&SettlementCampLayout.RootCells().All(c=>!State.World.IsSolid(c.X,c.Y));
 public Vector2 ResidentialEntranceWorld=>World(SettlementCampLayout.RootFront);
 public bool TryOpenResidentialEntrance(GobboController player)=>TryOpenRoot(player);
 public string RegionId=>"camp";public string LayoutId=>Definition.Id;public int Revision=>State?.Revision??-1;
 public double CollisionMs{get;private set;}public int CollisionPatches=>physics?.PatchCount??0;public int RejectedRoutes=>physics?.RejectedRoutes??0;
 readonly LogicalGridTransform grid=new LogicalGridTransform(new Point2(0,0),.6,192);
 RegionTerrainPhysics physics;
 readonly List<Stage59ActorView> views=new List<Stage59ActorView>();readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
 readonly HashSet<int> establishedBeds=new HashSet<int>();
 readonly HashSet<DualTile> dirty=new HashSet<DualTile>();CampResidentialCatalog catalog;GameObject collisionRoot;int publishedRevision=-1;bool disposed;
 readonly Dictionary<string,(int revision,bool access)> accessCache=new Dictionary<string,(int,bool)>();
 Vector2 focus;GameState campaign;CampSceneController lifecycle;CampPlayableSpawner spawner;
 Func<IEnumerable<DualTile>,int,Func<DualTile,bool>,int> commit;
 public Transform PresentationRoot=>transform;public Shader ActorShader=>spriteShader;
 public FocusProjection Projection=>Presentation.Snapshot;public Stage4Settings PresentationSettings=>Presentation.settings;public bool PresentationReady=>Ready;
 void Awake()
 {
 using var _stage61Profile = stage61InitializeProfile.Auto();

  if(!GameSceneCatalog.IsCamp(gameObject.scene))throw new InvalidOperationException("Camp region is not the configured destination; use the protected development entry");campaign=GameState.Instance;
  if(campaign==null)throw new InvalidOperationException("Replacement Camp requires the real campaign entry flow");
  lifecycle=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();spawner=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>();
  Definition=SettlementCampLayout.Shared;catalog=SettlementCampLayout.Catalog(Definition);
  var restored=campaign.campTerrainState?.regionSnapshot;
  if(restored==null)throw new InvalidOperationException("Candidate campaign has no compatible region snapshot");
  restored.ValidatePhysical();State=restored.RestoreTerrain();establishedBeds.UnionWith(restored.establishedBeds);commit=State.TakeMutationControl();place=State.TakePlacementControl();persistentRevision=restored.terrainRevision;
  if(campaign.campTerrainState.residentialSlotsEstablished!=establishedBeds.Count)throw new InvalidOperationException("Physical beds and campaign compatibility count disagree");
  long start=System.Diagnostics.Stopwatch.GetTimestamp();physics=new RegionTerrainPhysics(State,transform);CollisionMs=StreamingGeometry.Elapsed(start);publishedRevision=Revision;
  RegionCamera=new GameObject("Camp projected camera").AddComponent<Camera>();RegionCamera.gameObject.AddComponent<AudioListener>();RegionCamera.tag="MainCamera";RegionCamera.orthographic=true;RegionCamera.clearFlags=CameraClearFlags.SolidColor;RegionCamera.transform.position=new Vector3(0,0,-10);
  var palette=ScriptableObject.CreateInstance<Stage55Palette>();owned.Add(palette);RegionCamera.backgroundColor=palette.Color("VoidBlackGreen");
  var surface=new Texture2D(192,192,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(surface);surfaceTexture=surface;var pixels=new Color[192*192];for(int y=0;y<192;y++)for(int x=0;x<192;x++)pixels[y*192+x]=Definition.Protected(x,y)?Color.white:Color.black;surface.SetPixels(pixels);surface.Apply();
  string[] names={"FloorSlateBlue","UndugDirtMass","DigWallUpper","FreshFractureEdge","PermanentMossBlack","FloorPlumShadow","SicklyLichen"};var layers=new Material[names.Length];
  for(int i=0;i<layers.Length;i++){var m=new Material(terrainShader);owned.Add(m);m.SetColor("_Color",palette.Color(names[i]));m.SetFloat("_Layer",i);m.SetTexture("_CampSurface",surface);m.SetColor("_Permanent",palette.Color("PermanentMossBlack"));m.SetColor("_PermanentFace",palette.Color("PermanentWallFace"));m.SetColor("_WallUpper",palette.Color("DigWallUpper"));m.SetColor("_WallLower",palette.Color("DigWallLower"));m.SetColor("_Foot",palette.Color("WallFootOcclusion"));layers[i]=m;}
  focus=new Vector2((float)Definition.Marker("arrival.first").Interaction.X,(float)Definition.Marker("arrival.first").Interaction.Y);Presentation=gameObject.AddComponent<WorldRegionPresentation>();Presentation.PhysicalToLogical=physical=>{var logical=LogicalPosition(physical);return new Vector2((float)logical.X,(float)logical.Y);};Presentation.Initialize(State.World,layers,terrainShader,RegionCamera,focus);
  State.Committed+=Publish;DiggableTerrainService.Register(this);
  if(V1RuntimeProfile.DevelopmentToolsEnabled)gameObject.AddComponent<CampFightPitLab>();
 }
 public Point2 LogicalPosition(Vector2 physical)=>grid.ToLogical(new Point2(physical.x,physical.y));
 public Vector2 World(Point2 logical){var p=grid.ToWorld(logical);return new Vector2((float)p.X,(float)p.Y);}
 public Vector2Int WorldToCell(Vector2 p){var q=LogicalPosition(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
 public Vector2 CellToWorld(Vector2Int c)=>World(new Point2(c.x+.5,c.y+.5));
 public Vector3 Render(Vector2 physical)=>Presentation.Project(LogicalPosition(physical));
 public bool IsBlocked(Vector2Int c)=>disposed||State.World.IsSolid(c.x,c.y);
 public bool IsDiggable(Vector2Int c){if(arenaOwner!=null&&arenaCells.Contains(new DualTile(c.x,c.y)))return arenaPreset=="Dirt"&&IsBlocked(c);var k=Definition.Category(c.x,c.y);return !disposed&&IsBlocked(c)&&(k==CampCell.Expansion||k==CampCell.Residential);}
 public bool IsDerivedTerrainCollider(Collider2D c)=>physics!=null&&physics.Owns(c);
 public CampDigCategory GetSpatialDigCategory(Vector2Int c)=>Definition.Category(c.x,c.y)==CampCell.Residential?CampDigCategory.ResidentialReserved:CampDigCategory.NormalCampDiggable;
 public CampResidentialCatalog GetResidentialCatalog()=>catalog;
 public ResidentialSlotRecord GetResidentialSlot(int slot)=>catalog.GetSlot(slot)?.ToRecord()??default;
 public int GetResidentialProgressionIndexForSlot(int slot)=>catalog.TryGetRoomForSlot(slot,out var r)?r.ProgressionIndex:0;
 public List<Vector2Int> GetResidentialSlotFootprint(int slot)=>catalog.GetSlot(slot).ExcavationFootprint.Select(p=>new Vector2Int(p.x,p.y)).ToList();
 public List<Vector2Int> GetResidentialConstructionRoute(int slot)=>catalog.GetSlot(slot).ConstructionRoute.Select(p=>new Vector2Int(p.x,p.y)).ToList();
 public bool HasOpenAccessToFootprint(string id)
 {
  if(accessCache.TryGetValue(id,out var cached)&&cached.revision==Revision)return cached.access;
  string marker=id==CampEarlyMapCatalog.SquadFootprintId?"squad":id==CampEarlyMapCatalog.BonesFootprintId?"bones":id;
  try{var p=Definition.Marker(marker).Interaction;bool accessible=State.Fits(p,3)&&State.Route(Definition.Marker("fire").Interaction,p,3).Count>0;accessCache[id]=(Revision,accessible);return accessible;}catch(ArgumentException){return false;}
 }
 public void BindStation(GameObject station,string marker)
 {
  if(station.GetComponentsInChildren<SpriteRenderer>(true).Length==0||station.GetComponent<Collider2D>()==null)return;
  if(marker=="run.departure")
  {
   foreach(var source in station.GetComponentsInChildren<SpriteRenderer>(true))source.enabled=false;
   station.AddComponent<CampDepartureMouth>().Initialize(this);return;
  }
  int resourceIndex=0;
  foreach(var source in station.GetComponentsInChildren<SpriteRenderer>(true))
  {var view=station.AddComponent<Stage59ActorView>();view.Context=this;view.SourceOverride=source;view.Outline=false;view.ShowGroundShadow=false;view.FrameMetrics=marker=="fire"||marker=="storage"?CampSpriteContacts.Frame:null;view.PresentationLift=marker=="fire"&&source.name=="Flame"?.12f:0;view.GroundAnchorOverride=World(Definition.Marker(marker).Anchor)+(marker=="storage"?new Vector2((resourceIndex++-.5f)*1.5f,0):Vector2.zero);view.PresentationHeight=marker=="fire"?(source.name=="Logs"?.65f:1.8f):marker=="storage"?.85f:marker=="squad"?.45f:marker=="bones"?2.2f:0;view.Initialize();views.Add(view);}
 }
 public void BindStaticVisual(GameObject visual,Transform anchor)
 {var projected=visual.AddComponent<ProjectedWorldSprite>();projected.Initialize(this,anchor);}
 public bool DeparturePending{get;private set;}
 public void BeginDeparture(){FinishForCampaignCapture(campaign);DeparturePending=true;}
 public void CancelDeparture(){DeparturePending=false;}
 bool CanAct=>!DeparturePending&&LogicalReady&&(lifecycle?.WorldInputReady??false)&&Time.timeScale>0&&SporeInputReader.Instance!=null&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay;
 public void DigCircle(Vector2 point,float radius)=>DigCircle(point,radius,TerrainDigAuthority.Player,0,null);
 public TerrainDigResult DigCircle(Vector2 point,float radius,TerrainDigAuthority authority,int progression,IReadOnlyCollection<Vector2Int> authorized)
 {
 using var _stage61Profile = stage61DiggingProfile.Auto();

  if(!CanAct||!float.IsFinite(radius)||radius<=0)return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
  HashSet<Vector2Int> allowed=null;
  if(authority==TerrainDigAuthority.ResidentialProgression)
  {
   int next=establishedBeds.Count+1;if(authorized==null||next>10||GetResidentialProgressionIndexForSlot(next)!=progression)return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);
   allowed=new HashSet<Vector2Int>(GetResidentialSlotFootprint(next));if(authorized.Any(c=>!allowed.Contains(c)))return new TerrainDigResult(0,0,0,TerrainDigFailureReason.AuthorizationRejected);allowed.IntersectWith(authorized);
  }
  var center=WorldToCell(point);int range=Mathf.CeilToInt(radius/CellSize)+1,evaluated=0;var cuts=new List<DualTile>();
  for(int y=center.y-range;y<=center.y+range;y++)for(int x=center.x-range;x<=center.x+range;x++){var c=new Vector2Int(x,y);if((CellToWorld(c)-point).sqrMagnitude>radius*radius)continue;evaluated++;if(!IsDiggable(c))continue;if(authority==TerrainDigAuthority.Player||authority==TerrainDigAuthority.Buddy||authority==TerrainDigAuthority.ResidentialProgression&&allowed.Contains(c))cuts.Add(new DualTile(x,y));}
  int changed=commit(cuts,Revision,c=>true);return new TerrainDigResult(evaluated,cuts.Count,changed,changed>0?TerrainDigFailureReason.None:TerrainDigFailureReason.NoDirtRemoved);
 }
 public bool TryOpenRoot(GobboController player)
 {
  if(!CanAct||ResidentialEntranceOpen||player==null)return false;var front=World(SettlementCampLayout.RootFront);var p=(Vector2)player.transform.position;
  if(Vector2.Distance(p,front)>1.8f||!TileMover.CanTraverseBox(this,p,front,TileMover.GetMapClearanceExtents(player.GetComponent<Rigidbody2D>(),.25f)))return false;
  bool help=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Any(b=>CampResidentialOccupancyResolver.IsLivingBuddy(b.unitData)&&Vector2.Distance(b.transform.position,p)<=2.5f&&TileMover.CanTraverseBox(this,b.transform.position,p,TileMover.GetMapClearanceExtents(b.GetComponent<Rigidbody2D>(),.25f)));
  bool opened=help&&commit(SettlementCampLayout.RootCells(),Revision,c=>true)>0;
  if(opened)SporeSaveManager.SaveCurrentSlotFromGameState();
  return opened;
 }
 public bool CompleteResidentialSlotForProgression(int progression,int slot)
 {
  if(DeparturePending||!LogicalReady)return false;
  if(slot!=establishedBeds.Count+1||GetResidentialProgressionIndexForSlot(slot)!=progression||GetResidentialSlotFootprint(slot).Any(IsBlocked))return false;
  var p=catalog.GetSlot(slot).RestCell;if(State.Route(new Point2(p.x+.5,p.y+.5),Definition.Marker("fire").Interaction,3).Count==0)return false;
  establishedBeds.Add(slot);campaign.campTerrainState.residentialSlotsEstablished=establishedBeds.Count;campaign.campTerrainState.residentialStage=Math.Max(campaign.campTerrainState.residentialStage,progression);return true;
 }
 public CampRegionSnapshot CaptureRegionSnapshot(GameState expectedCampaign)
 {
  if(!ReferenceEquals(expectedCampaign,campaign)||!LogicalReady)throw new InvalidOperationException("Region snapshot requires its ready canonical campaign");
  var snapshot=CampRegionSnapshot.Pristine();snapshot.terrainRevision=persistentRevision;snapshot.rootOpened=ResidentialEntranceOpen;snapshot.excavatedCells=State.ExportDeltas();snapshot.establishedBeds=establishedBeds.OrderBy(id=>id).ToArray();return snapshot;
 }
 public List<Vector2> Route(Vector2 start,Vector2 goal,Vector2 extents)
 {
 using var _stage61Profile = stage61NavigationProfile.Auto();

  return physics.Route(this,start,goal,extents);
 }
 public void BindActor(GameObject actor)
 {
  var body=actor.GetComponent<Rigidbody2D>();var binding=actor.GetComponent<ActorTerrainBinding>()??actor.AddComponent<ActorTerrainBinding>();binding.Bind(this);binding.SuppressCampaignBootstrap=false;binding.Ready=()=>LogicalReady&&(lifecycle?.WorldInputReady??false);binding.ReadRevision=()=>disposed?-1:Revision;binding.FindRoute=(a,b)=>Route(a,b,TileMover.GetMapClearanceExtents(body,.25f));binding.PointerToWorld=screen=>{var render=RegionCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));return World(Projection.Unproject(new Point2(render.x,-render.y)));};
  var dig=actor.GetComponent<BuddyDigAbility>();if(dig!=null)dig.BindTerrain(this);
  Physics2D.SyncTransforms();var extents=TileMover.GetMapClearanceExtents(body,.25f);
  bool SpawnFits(Vector2 position)
  {
   if(!TileMover.CanOccupyBox(this,position,extents))return false;
   foreach(var other in views)
   {
    if(other==null||other.GetComponent<BuddyUnit>()==null&&other.GetComponent<GobboController>()==null)continue;
    var otherBody=other.GetComponent<Rigidbody2D>();if(otherBody==null)continue;var otherExtents=TileMover.GetMapClearanceExtents(otherBody,.25f);var gap=position-otherBody.position;
    if(Math.Abs(gap.x)<extents.x+otherExtents.x+.03f&&Math.Abs(gap.y)<extents.y+otherExtents.y+.03f)return false;
   }
   return Route(position,World(Definition.Marker("fire").Interaction),extents).Count>0;
  }
  if(!SpawnFits(body.position))
  {
   var origin=WorldToCell(body.position);var source=spawner!=null&&spawner.SpawnedPlayer!=null?LogicalPosition(spawner.SpawnedPlayer.transform.position):Definition.Marker("arrival.first").Interaction;Vector2 selected=default;float best=float.PositiveInfinity;
   // Arrival ring offsets are suggestions. Select a fitting point in the same local open component.
   for(int y=-6;y<=6;y++)for(int x=-6;x<=6;x++)
   {
    var cell=origin+new Vector2Int(x,y);var candidate=CellToWorld(cell);float distance=(candidate-body.position).sqrMagnitude;
    if(distance>=best||!SpawnFits(candidate))continue;
    if(State.Route(source,new Point2(cell.x+.5,cell.y+.5),3).Count==0)continue;
    selected=candidate;best=distance;
   }
   if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No locally reachable spawn fits actual actor collider: "+actor.name+" / "+extents);
   body.position=selected;actor.transform.position=new Vector3(selected.x,selected.y,actor.transform.position.z);body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
  }
  var view=actor.GetComponent<Stage59ActorView>()??actor.AddComponent<Stage59ActorView>();view.Context=this;view.Outline=false;view.Initialize();views.Add(view);
  Physics2D.SyncTransforms();if(!TileMover.CanOccupyBox(this,body.position,TileMover.GetMapClearanceExtents(body,.25f)))throw new InvalidOperationException("Spawned actor lacks logical clearance: "+actor.name);
 }
 public void FinishForCampaignCapture(GameState expected)
 {if(!ReferenceEquals(expected,campaign))throw new InvalidOperationException("Transient cleanup requires canonical campaign");var lab=GetComponent<CampFightPitLab>();lab?.StopSession("Campaign capture or departure");if(lab!=null&&lab.Active)throw new InvalidOperationException("Fight Pit cleanup incomplete; campaign commit blocked: "+lab.Outcome);}
 public void FinishForCampaignRestore(GameState expected)
 {if(!ReferenceEquals(expected,campaign))throw new InvalidOperationException("Restore requires canonical campaign");var lab=GetComponent<CampFightPitLab>();lab?.StopSession("Campaign restore");if(lab!=null&&lab.Active&&!lab.RestoringCommittedCampaign)throw new InvalidOperationException("Use explicit committed recovery after failed Pit cleanup");}
 public bool OpenPitDevelopmentEntrance()
 {if(!V1RuntimeProfile.DevelopmentToolsEnabled||!LogicalReady||arenaOwner!=null)return false;_ = SporeGobbo.Safety.Persistence.ExperimentRoot;if(ResidentialEntranceOpen)return true;return commit(SettlementCampLayout.RootCells(),Revision,c=>true)>0;}
 public bool AcquireArenaTerrain(object owner)
 {if(owner==null||arenaOwner!=null||!V1RuntimeProfile.DevelopmentToolsEnabled||!LogicalReady)return false;_ = SporeGobbo.Safety.Persistence.ExperimentRoot;arenaOwner=owner;return true;}
 public void ReleaseArenaTerrain(object owner)
 {if(!ReferenceEquals(owner,arenaOwner))return;ClearArenaTerrain(owner);arenaOwner=null;arenaPreset="Open";}
 public void ClearArenaTerrain(object owner)
 {
  if(!ReferenceEquals(owner,arenaOwner))throw new InvalidOperationException("Wrong arena owner");
  if(arenaCells.Any(c=>!Definition.PlacementAllowed(c.X,c.Y)||State.World.BaseSolid(c.X,c.Y)))throw new InvalidOperationException("Arena ownership escaped authored floor");
  commit(arenaCells,Revision,c=>true);foreach(var c in arenaCells)surfaceTexture.SetPixel(c.X,c.Y,Color.black);surfaceTexture.Apply();arenaCells.Clear();
 }
 public void ApplyArenaTerrain(object owner,string preset)
 {
  if(!ReferenceEquals(owner,arenaOwner))throw new InvalidOperationException("Wrong arena owner");if(preset!="Open"&&preset!="Rock"&&preset!="Roots"&&preset!="Dirt")throw new ArgumentException("Unknown authored preset");ClearArenaTerrain(owner);arenaPreset=preset;if(preset=="Open")return;
  var cells=new List<DualTile>();
  if(preset=="Rock"){for(int y=78;y<=81;y++)for(int x=148;x<=150;x++)cells.Add(new DualTile(x,y));for(int y=85;y<=87;y++)for(int x=154;x<=157;x++)cells.Add(new DualTile(x,y));}
  else {for(int y=76;y<=86;y++)for(int x=151;x<=153;x++)cells.Add(new DualTile(x,y));}
  var result=place(new TerrainPlacementRequest(State.InstanceId,Revision,"pit-"+Guid.NewGuid().ToString("N"),cells),requested=>
  {foreach(var actor in views){if(actor==null)continue;var body=actor.GetComponent<Rigidbody2D>();if(body==null||!body.simulated)continue;var ext=TileMover.GetMapClearanceExtents(body,.25f);foreach(var cell in requested){Vector2 center=CellToWorld(new Vector2Int(cell.X,cell.Y));if(Math.Abs(center.x-body.position.x)<ext.x+.3f&&Math.Abs(center.y-body.position.y)<ext.y+.3f)return false;}}return true;});
  if(!result.Accepted)throw new InvalidOperationException("Arena preset refused: "+result.Rejection);arenaCells.AddRange(cells);foreach(var c in cells)surfaceTexture.SetPixel(c.X,c.Y,preset=="Rock"?Color.white:preset=="Roots"?new Color(0,1,0):Color.black);surfaceTexture.Apply();
 }
 void Publish(IReadOnlyList<DualTile> changed,int revision)
 {
 using var _stage61Profile = stage61CollisionProfile.Auto();

  if(changed.Any(c=>State.World.BaseSolid(c.X,c.Y)))persistentRevision++;
  long start=System.Diagnostics.Stopwatch.GetTimestamp();physics.Publish(changed);CollisionMs=StreamingGeometry.Elapsed(start);Presentation.Stream.RefreshPhysicalCells(changed);publishedRevision=revision;
 }
 void LateUpdate()
 {
  if(Presentation==null)return;var player=spawner?.SpawnedPlayer;if(player!=null){var p=LogicalPosition(player.transform.position);focus=new Vector2((float)p.X,(float)p.Y);}Presentation.Refresh(focus,Time.deltaTime);
  using(stage61SortingProfile.Auto()){views.RemoveAll(v=>v==null);views.Sort((a,b)=>b.PhysicalFoot.y.CompareTo(a.PhysicalFoot.y));}for(int i=0;i<views.Count;i++)views[i].Draw(20+i);
 }
 void OnDisable(){try{FinishForCampaignCapture(campaign);}finally{DiggableTerrainService.Unregister(this);}}
 void OnDestroy(){disposed=true;DiggableTerrainService.Unregister(this);if(State!=null)State.Committed-=Publish;foreach(var v in views)if(v!=null)v.Release();views.Clear();physics?.Dispose();if(RegionCamera!=null)Destroy(RegionCamera.gameObject);foreach(var o in owned)if(o!=null)Destroy(o);}
}
