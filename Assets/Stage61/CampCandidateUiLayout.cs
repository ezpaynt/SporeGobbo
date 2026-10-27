using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>Rectangles and text only. Existing Camp views retain all data, buttons and modal ownership.</summary>
public static class CampCandidateUiLayout
{
 public static void Shade(GameObject panel){if(panel!=null&&panel.TryGetComponent<Image>(out var image))image.color=new Color(.02f,.03f,.025f,.94f);}
 static RectTransform Place(Transform target,Transform parent,Vector2 at,Vector2 size,bool top=false)
 {var rect=target as RectTransform;if(rect==null)return null;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,top?1:.5f);rect.anchoredPosition=at;rect.sizeDelta=size;rect.localScale=Vector3.one;return rect;}
 static void Panel(GameObject panel,Vector2 size){if(panel==null)return;Place(panel.transform,panel.transform.parent,Vector2.zero,size);Shade(panel);}
 static void Button(Button button,Transform parent,Vector2 at,Vector2 size)
 {if(button==null)return;Place(button.transform,parent,at,size);foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.fontSize=24;text.enableAutoSizing=false;text.alignment=TextAlignmentOptions.Center;text.margin=Vector4.zero;var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}}
 static void Label(TMP_Text text,Transform parent,Vector2 at,Vector2 size,int font)
 {if(text==null)return;Place(text.transform,parent,at,size);text.fontSize=font;text.enableAutoSizing=false;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.Normal;text.rectTransform.localScale=Vector3.one;}
 static void Heading(GameObject panel,TMP_Text body,string title,Transform[] dataRoots)
 {
  foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))
  {
   if(text==body||text.GetComponentInParent<Button>()!=null||text.GetComponentInParent<ScrollRect>()!=null)continue;
   bool data=false;foreach(var root in dataRoots)if(root!=null&&text.transform.IsChildOf(root)){data=true;break;}if(data)continue;
   Label(text,panel.transform,new Vector2(0,390),new Vector2(1450,65),32);text.alignment=TextAlignmentOptions.Center;text.text=title;
  }
 }
 static void Scroll(Transform content,GameObject panel,string id,Vector2 at,Vector2 size,bool rows)
 {
  if(content==null||panel==null)return;var root=panel.transform.Find(id);ScrollRect scroll;
  if(root==null)
  {
   var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(ScrollRect));root=go.transform;root.SetParent(panel.transform,false);go.GetComponent<Image>().color=new Color(1,1,1,.025f);scroll=go.GetComponent<ScrollRect>();
   var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));viewport.transform.SetParent(root,false);var rect=viewport.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;scroll.viewport=rect;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
  }
  else scroll=root.GetComponent<ScrollRect>();
  Place(root,panel.transform,at,size);var child=content as RectTransform;child.SetParent(scroll.viewport,false);child.anchorMin=new Vector2(0,1);child.anchorMax=Vector2.one;child.pivot=new Vector2(.5f,1);child.sizeDelta=new Vector2(-16,0);child.anchoredPosition=new Vector2(0,-8);child.localScale=Vector3.one;
  var fitter=content.GetComponent<ContentSizeFitter>()??content.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  if(rows){var layout=content.GetComponent<VerticalLayoutGroup>()??content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(0,0,0,0);layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;}
  scroll.content=child;scroll.verticalNormalizedPosition=1;
 }
 public static void Squad(CampSquadSelect view)
 {
  if(view.panel==null)return;Panel(view.panel,new Vector2(1300,900));Label(view.titleText,view.panel.transform,new Vector2(0,350),new Vector2(1200,100),34);view.titleText.alignment=TextAlignmentOptions.Center;
  Place(view.activeListParent,view.panel.transform,new Vector2(-310,230),new Vector2(590,600),true);Place(view.reserveListParent,view.panel.transform,new Vector2(310,230),new Vector2(590,600),true);Button(view.closeButton,view.panel.transform,new Vector2(0,-385),new Vector2(270,55));
 }
 public static void RunStats(CampSceneController view)
 {
  if(view.runStatsPanel==null||view.runStatsText==null)return;Panel(view.runStatsPanel,new Vector2(1500,920));Heading(view.runStatsPanel,view.runStatsText,"Back at Camp",new Transform[0]);view.runStatsText.alignment=TextAlignmentOptions.TopLeft;view.runStatsText.fontSize=24;view.runStatsText.enableAutoSizing=false;view.runStatsText.textWrappingMode=TextWrappingModes.Normal;view.runStatsText.margin=new Vector4(8,6,8,6);
  Scroll(view.runStatsText.transform,view.runStatsPanel,"Readable run report",new Vector2(0,-5),new Vector2(1350,650),false);Button(view.continueToSurvivorsButton,view.runStatsPanel.transform,new Vector2(0,-390),new Vector2(270,55));
 }
 public static void Survivors(CampSceneController view)
 {
  if(view.survivorsPanel==null)return;Panel(view.survivorsPanel,new Vector2(1660,940));var squad=view.runBuddyListParent!=null?view.runBuddyListParent:view.activeSquadListParent;Heading(view.survivorsPanel,view.survivorsText,"Buddies after the run",new[]{squad,view.reserveListParent,view.pendingEvolutionListParent});
  if(view.survivorsText!=null){view.survivorsText.alignment=TextAlignmentOptions.TopLeft;view.survivorsText.fontSize=22;view.survivorsText.enableAutoSizing=false;view.survivorsText.textWrappingMode=TextWrappingModes.Normal;view.survivorsText.margin=Vector4.zero;Scroll(view.survivorsText.transform,view.survivorsPanel,"Readable roll call",new Vector2(-550,0),new Vector2(470,650),false);}
  Scroll(squad,view.survivorsPanel,"Readable departed squad",Vector2.zero,new Vector2(480,650),true);Scroll(view.reserveListParent,view.survivorsPanel,"Readable Camp and recruits",new Vector2(550,0),new Vector2(480,650),true);
  Scroll(view.pendingEvolutionListParent,view.survivorsPanel,"Readable pending growth",new Vector2(-150,-385),new Vector2(1050,90),true);Button(view.continueToCampButton,view.survivorsPanel.transform,new Vector2(650,-390),new Vector2(240,55));
 }
}
