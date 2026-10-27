using UnityEngine;
using System.Collections.Generic;
using SporeGobbo.Input;
/// <summary>Brief presentation driven only by the existing first-home routine. Owns no needs or jobs.</summary>
[DefaultExecutionOrder(2800)]
public sealed class BuddyHomeThought:MonoBehaviour
{
 BuddyUnit buddy;Stage59ActorView view;SettlementCampRegion region;float until;GUIStyle style;CampInteractionDetector detector;readonly Vector3[] promptCorners=new Vector3[4];
 static readonly List<BuddyHomeThought> visibleCues=new List<BuddyHomeThought>();
 public static int ActiveCount=>visibleCues.Count;
 public static int RenderedCount {get {int count=0;foreach(var cue in visibleCues)if(cue.lastRenderedFrame>=Time.frameCount-2)count++;return count;}}
 int lastRenderedFrame=-100;
 public static void Show(BuddyUnit actor,float seconds)
 {var cue=actor.GetComponent<BuddyHomeThought>();if(cue==null)cue=actor.gameObject.AddComponent<BuddyHomeThought>();cue.buddy=actor;cue.until=Time.time+Mathf.Clamp(seconds,.1f,4);}
 void OnEnable(){visibleCues.Add(this);}void OnDisable(){visibleCues.Remove(this);}
 void Update()
 {if(CampStartRoutineManager.Instance==null||!CampStartRoutineManager.Instance.IsCampVisitRoutineRunning||buddy==null||buddy.unitData==null||buddy.unitData.isDead||buddy.unitData.campResidentialSlotId>0||Time.time>=until){Destroy(this);return;}if(view==null)view=GetComponent<Stage59ActorView>();if(region==null)region=Object.FindAnyObjectByType<SettlementCampRegion>();if(detector==null)detector=Object.FindAnyObjectByType<CampInteractionDetector>();}
 void OnGUI()
 {
  if(view==null||!view.IsPresented||region==null||!region.Ready||region.RegionCamera.cullingMask==0||Time.timeScale<=0||SporeInputReader.Instance==null||SporeInputReader.Instance.Context!=SporeInputContext.Gameplay)return;
  if(style==null){style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter};style.normal.textColor=new Color(1,.95f,.8f);}
  float scale=Screen.height/1080f;style.fontSize=Mathf.RoundToInt(23*scale);
  var screen=region.RegionCamera.WorldToScreenPoint(view.ProjectedHead);
  if(screen.z<=0||screen.x<0||screen.x>Screen.width||screen.y<32*scale||screen.y>Screen.height)return;
  foreach(var other in visibleCues)
  {
   if(other==this||other.view==null||other.buddy==null||other.buddy.unitData==null)continue;
   if(string.CompareOrdinal(other.buddy.unitData.uniqueId,buddy.unitData.uniqueId)>=0)continue;
   var point=region.RegionCamera.WorldToScreenPoint(other.view.ProjectedHead);
   if(Mathf.Abs(point.x-screen.x)<110*scale&&Mathf.Abs(point.y-screen.y)<32*scale)return;
  }
  if(detector!=null&&detector.promptPanel!=null&&detector.promptPanel.activeInHierarchy)
  {
   var rect=detector.promptPanel.GetComponent<RectTransform>();var canvas=rect.GetComponentInParent<Canvas>();rect.GetWorldCorners(promptCorners);var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
   var lo=RectTransformUtility.WorldToScreenPoint(camera,promptCorners[0]);var hi=RectTransformUtility.WorldToScreenPoint(camera,promptCorners[2]);
   if(new Rect(screen.x-55*scale,screen.y+2*scale,110*scale,30*scale).Overlaps(new Rect(lo,hi-lo)))return;
  }
  GUI.Label(new Rect(screen.x-55*scale,Screen.height-screen.y-32*scale,110*scale,30*scale),"Zzz...",style);
  if(Event.current.type==EventType.Repaint)lastRenderedFrame=Time.frameCount;
 }
}
