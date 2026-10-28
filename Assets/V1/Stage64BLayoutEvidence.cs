#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.CampCandidate;using SporeGobbo.Safety;
/// <summary>Owned authored-grid and projected-view evidence. Validated warps are explicit setup, not natural traversal.</summary>
public sealed class Stage64BLayoutEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(),errors=new List<string>();string directory;bool ended;float deadline;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+180;Application.logMessageReceived+=Observe;}
 void Observe(string m,string t,LogType k){if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(m+"\n"+t);}
 void Update(){if(!ended&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 void Check(string n,bool v){checks.Add((v?"PASS ":"FAIL ")+n);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);if(!v)Finish();}
 IEnumerator Start()
 {
  yield return null;yield return null;Check("owned layout development evidence",Persistence.Isolated&&V1RuntimeProfile.DevelopmentToolsEnabled&&V1RuntimeProfile.EvidenceEnabled);if(ended)yield break;
  Check("owned slot missing before compatibility proof",SporeSaveManager.InspectSlot(2)==SporeSaveManager.SlotStatus.Missing);if(ended)yield break;
  var fixture=Resources.Load<TextAsset>("Stage61Fixtures/EstablishedB");DevelopmentCampaignSnapshot.Decode(fixture.text,2,out _);var oldFixture=JsonUtility.FromJson<DevelopmentCampaignSnapshot>(fixture.text);
  var oldV1=new V1CampaignSnapshot{format=V1CampaignSnapshot.Format,version=V1CampaignSnapshot.CurrentVersion,storageProfile=V1RuntimeProfile.StorageProfile,campaignId=V1RuntimeProfile.CampaignIdPrefix+"64b-layout-proof",generation=7,payload=oldFixture.payload,sha256=CampaignSnapshotStorage.Hash(oldFixture.payload)};string oldBytes=JsonUtility.ToJson(oldV1);string slot=SporeSaveManager.GetSlotPath(2);
  CampaignSnapshotStorage.Commit(slot,oldBytes,text=>V1CampaignSnapshot.Decode(text,2,out _));Check("old V1 Inspect is valid and read only",SporeSaveManager.InspectSlot(2)==SporeSaveManager.SlotStatus.Valid&&CampaignSnapshotStorage.Read(slot)==oldBytes);if(ended)yield break;
  var detached=SporeSaveManager.LoadSlot(2);Check("old V1 Load adapts without writing",detached.hasSave&&detached.campTerrainState.regionSnapshot.layoutId==SettlementCampLayout.LayoutId&&CampaignSnapshotStorage.Read(slot)==oldBytes);if(ended)yield break;
  SporeSaveManager.SaveSlot(detached);var committed=V1CampaignSnapshot.Decode(CampaignSnapshotStorage.Read(slot),2,out _);Check("next actual save preserves identity generation and exact old backup",committed.campaignId==oldV1.campaignId&&committed.generation==8&&CampaignSnapshotStorage.Read(slot+".bak")==oldBytes);if(ended)yield break;
  Find<MainMenuController>().LoadGame(2);float end=Time.realtimeSinceStartup+60;while(!ended&&Find<CampSceneController>()?.WorldInputReady!=true&&Time.realtimeSinceStartup<end)yield return null;
  var region=Find<SettlementCampRegion>();Check("real adapted Camp ready",region!=null&&region.Ready);if(ended)yield break;
  var layout=region.Definition;File.WriteAllText(Path.Combine(directory,"layout-identity.txt"),layout.Id+"\n"+layout.Fingerprint()+"\n");using(var writer=new StreamWriter(Path.Combine(directory,"camp-logical-grid.csv"))){writer.WriteLine("x,y,category,solid,protected");for(int y=0;y<192;y++)for(int x=0;x<192;x++)writer.WriteLine(x+","+y+","+layout.Category(x,y)+","+region.State.World.IsSolid(x,y)+","+layout.Protected(x,y));}
  var player=Find<CampPlayableSpawner>().SpawnedPlayer;var body=player.GetComponent<Rigidbody2D>();var size=TileMover.GetMapClearanceExtents(body,.25f);
  foreach(var c in new[]{("fire-centered",new Point2(90.5,113.5)),("bones-exit-approach",layout.Marker("bones").Interaction),("pit-entry",CampFightPitLayout.Entry),("pit-center",CampFightPitLayout.Center)})
  {Vector2 position=region.World(c.Item2);Check("actual player clearance / "+c.Item1,TileMover.CanOccupyBox(region,position,size));if(ended)yield break;body.position=position;player.transform.position=position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForSeconds(1.5f);yield return Capture(c.Item1);}
  Check("reference projection and cell scale preserved",region.CellSize==.6f&&region.Presentation.settings.anchor==.62f);
  Check("ten physical beds and canonical identities retained",region.CaptureRegionSnapshot(GameState.Instance).establishedBeds.Length==10&&GameState.Instance.ownedGobbos.All(u=>u.campResidentialSlotId==0||u.campResidentialSlotId<=10));
  var before=JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance));SporeSaveManager.SaveCurrentSlotFromGameState();Check("actual save succeeds",SporeSaveManager.InspectSlot(2)==SporeSaveManager.SlotStatus.Valid);
  var loaded=SporeSaveManager.LoadSlot(2);Check("actual load retains adapted physical Camp",loaded!=null&&JsonUtility.ToJson(loaded.campTerrainState.regionSnapshot)==before);
  Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();end=Time.realtimeSinceStartup+60;while(!ended&&Find<MainMenuController>()==null&&Time.realtimeSinceStartup<end)yield return null;yield return null;yield return null;Check("layout proof releases terrain and actor resources",DiggableTerrainService.Active==null&&Stage59ActorView.OwnedMeshes==0);Finish();
 }
 IEnumerator Capture(string name){Stage61RuntimeMetrics.Exclude("layout capture");Stage61RuntimeMetrics.Category("capture");yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());}finally{Destroy(texture);}}
 void Finish(){if(ended)return;ended=true;if(Time.realtimeSinceStartup>deadline)errors.Add("Layout proof watchdog");Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Layout proof failed":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray(),method="Authentic established fixture imported through existing save authority, actual Main Menu/Camp, collider-validated camera setup warps, actual save/load. Not natural walking or human acceptance."},true));Application.Quit(errors.Count==0&&checks.Count>0&&checks.All(c=>c.StartsWith("PASS "))?0:1);}
 [Serializable]sealed class Result{public bool finished;public string error,method;public string[] checks,runtimeErrors;}
 void OnDestroy()=>Application.logMessageReceived-=Observe;
}
#endif

