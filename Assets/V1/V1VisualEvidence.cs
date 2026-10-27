#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Safety;
/// <summary>Explicit owned setup for representative depth/grounding captures, not natural gameplay evidence.</summary>
public sealed class V1VisualEvidence:MonoBehaviour
{
 string directory;readonly List<string> checks=new List<string>(),errors=new List<string>();bool ended;float deadline;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 void Awake(){directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+300;Application.logMessageReceived+=Observe;}
 void Observe(string m,string t,LogType k){if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(m+"\n"+t);}
 void Update(){if(!ended&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 void Check(string n,bool v){checks.Add((v?"PASS ":"FAIL ")+n);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);}
 IEnumerator Start()
 {
  yield return null;yield return null;Check("visual fixture tools are explicitly enabled",V1RuntimeProfile.DevelopmentToolsEnabled);DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
  float end=Time.realtimeSinceStartup+60;while(Find<CampSceneController>()?.WorldInputReady!=true&&Time.realtimeSinceStartup<end)yield return null;
  Check("visual fixture real Camp ready",Find<CampSceneController>()?.WorldInputReady==true);yield return new WaitForSeconds(1);yield return Capture("camp-reference-grounding");
  var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);portal.goButton.onClick.Invoke();end=Time.realtimeSinceStartup+60;while(!V1NormalEvidence.Expedition()&&Time.realtimeSinceStartup<end)yield return null;Check("visual fixture actual expedition ready",V1NormalEvidence.Expedition());
  ExpeditionDevelopmentControls.SetGodMode(true);var r=Find<ExpeditionRegion>();
  var cases=new[]{new Point2(108.5,52.5),new Point2(114.5,58.5),new Point2(108.5,65.5),new Point2(74.5,63.5),new Point2(78.5,68.5),new Point2(85.5,64.5),new Point2(107.5,97.5),new Point2(115.5,103.5),new Point2(107.5,109.5)};
  string[] names={"stone-back","stone-side","stone-front","root-back","root-underpass","root-side","fossil-back","fossil-side","fossil-front"};
  foreach(int health in new[]{100,250,500})
  {
  var leader=GameState.Instance.GetLeader();leader.maxHealth=health;leader.health=health;GameState.Instance.ApplyToPlayer(r.Player);yield return null;
  for(int i=0;i<cases.Length;i++)
  {
   Vector2 p=r.World(cases[i]);var body=r.Player.GetComponent<Rigidbody2D>();bool fit=TileMover.CanOccupyBox(r,p,TileMover.GetMapClearanceExtents(body,.25f));
   if(!fit){bool found=false;for(int d=1;d<=4&&!found;d++)for(int x=-d;x<=d&&!found;x++)for(int y=-d;y<=d&&!found;y++){var q=r.World(cases[i]+new Point2(x,y));if(TileMover.CanOccupyBox(r,q,TileMover.GetMapClearanceExtents(body,.25f))){p=q;found=true;}}fit=found;}
   Check("representative logical clearance / hp"+health+" / "+names[i],fit);if(!fit)continue;body.position=p;r.Player.transform.position=p;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForSeconds(1.2f);yield return Capture("hp"+health+"-"+names[i]);
   var view=r.Player.GetComponent<Stage59ActorView>();Check("projected feet agree with shared terrain mapping / "+names[i],view!=null&&view.IsPresented&&Vector2.Distance(view.ProjectedFoot,(Vector2)r.Render(view.PhysicalFoot))<.001f);
   if(view!=null)File.AppendAllText(Path.Combine(directory,"grounding.csv"),"hp"+health+"-"+names[i]+","+p.x+","+p.y+","+view.PhysicalFoot.x+","+view.PhysicalFoot.y+","+view.ProjectedHead.x+","+view.ProjectedHead.y+","+r.Player.transform.localScale.x+"\n");
  }
  }
  Check("visual fixtures preserve accepted projection and scale",r.CellSize==.6f&&r.Presentation.settings.anchor==.62f);Finish();
 }
 IEnumerator Capture(string name){Stage61RuntimeMetrics.Exclude("visual capture");Stage61RuntimeMetrics.Category("capture");yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());}finally{Destroy(image);}}
 void Finish(){if(ended)return;ended=true;Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Visual proof error":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(errors.Count==0&&checks.All(x=>x.StartsWith("PASS "))?0:1);}
 [Serializable]sealed class Result{public bool finished;public string error;public string[] checks,runtimeErrors;}
 void OnDestroy()=>Application.logMessageReceived-=Observe;
}
#endif
