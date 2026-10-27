using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>Thin view for existing snack rules; inventory/effects/cycle authorization remain in CampfireSnackService.</summary>
public sealed class CampfireSnackMenuView:MonoBehaviour
{
 SporeUiCoordinator ui;CampFireRecovery fire;GameObject host,panel;RectTransform itemContent,targetContent;TMP_Text preview;Button feed,open,back;
 string itemId="",targetId="";ItemTargetType targetType=ItemTargetType.Leader;
 readonly List<GameObject> rows=new List<GameObject>();
 void Start()
 {
  ui=SporeUiCoordinator.Instance;fire=GetComponent<CampFireRecovery>();if(fire==null||fire.fireMenuPanel==null)return;
  fire.MenuClosed+=Close;
  host=new GameObject("Campfire snacks view",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));host.transform.SetParent(fire.fireMenuPanel.transform,false);
  var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=750;
  var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
  open=Button(host.transform,"Snacks",new Vector2(790,430),new Vector2(150,42),Open);
  panel=new GameObject("Snack selection",typeof(RectTransform),typeof(Image));panel.transform.SetParent(host.transform,false);Rect(panel.GetComponent<RectTransform>(),Vector2.zero,new Vector2(700,470));panel.GetComponent<Image>().color=new Color(.08f,.11f,.10f,.99f);
  Text(panel.transform,"Choose a snack and a Gobbo",new Vector2(-60,200),new Vector2(530,36),25);
  back=Button(panel.transform,"Back",new Vector2(275,200),new Vector2(110,36),Close);
  itemContent=Scroll(panel.transform,new Vector2(-165,20));targetContent=Scroll(panel.transform,new Vector2(165,20));
  preview=Text(panel.transform,"",new Vector2(0,-145),new Vector2(650,65),18);feed=Button(panel.transform,"Feed snack",new Vector2(0,-205),new Vector2(190,42),Commit);panel.SetActive(false);
 }
 static void Rect(RectTransform t,Vector2 position,Vector2 size){t.anchorMin=t.anchorMax=new Vector2(.5f,.5f);t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=position;t.sizeDelta=size;}
 TMP_Text Text(Transform parent,string value,Vector2 position,Vector2 size,int fontSize)
 {var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);Rect(go.GetComponent<RectTransform>(),position,size);var text=go.GetComponent<TextMeshProUGUI>();text.font=fire.titleText!=null?fire.titleText.font:TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=fontSize;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;return text;}
 Button Button(Transform parent,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
 {var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);Rect(go.GetComponent<RectTransform>(),position,size);var image=go.GetComponent<Image>();image.color=new Color(.24f,.31f,.26f,1);var button=go.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);Text(go.transform,title,Vector2.zero,size-new Vector2(12,4),18);return button;}
 RectTransform Scroll(Transform parent,Vector2 position)
 {var go=new GameObject("Choices",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));go.transform.SetParent(parent,false);Rect(go.GetComponent<RectTransform>(),position,new Vector2(310,245));go.GetComponent<Image>().color=new Color(.04f,.06f,.05f,.8f);var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(go.transform,false);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;var scroll=go.GetComponent<ScrollRect>();scroll.viewport=go.GetComponent<RectTransform>();scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;return content;}
 public void Open()
 {
  if(panel==null)return;panel.SetActive(true);panel.transform.SetAsLastSibling();var state=GameState.Instance;targetType=ItemTargetType.Leader;targetId=state.GetLeader().uniqueId;Refresh();ui.PushModal(this,Close,false,back,panel);
 }
 public void Close(){if(panel!=null)panel.SetActive(false);if(ui!=null)ui.PopModal(this);}
 void Refresh()
 {
  foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();var state=GameState.Instance;int i=0;
  foreach(var item in CampfireSnackService.EnumerateOwnedCampfireSnacks(state))
  {string id=item.NormalizedId;if(string.IsNullOrEmpty(itemId))itemId=id;var row=Button(itemContent,item.displayName+" x"+InventoryService.GetQuantity(state,id),new Vector2(0,-i*40-20),new Vector2(290,36),()=>{itemId=id;UpdatePreview();});row.GetComponent<RectTransform>().anchorMin=row.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);rows.Add(row.gameObject);i++;}
  itemContent.sizeDelta=new Vector2(0,Mathf.Max(245,i*40));i=0;
  void Target(GobboUnitSaveData unit,ItemTargetType type)
  {string id=unit.uniqueId;var row=Button(targetContent,unit.displayName+(CampfireSnackService.CanTargetEat(unit,state)?"":" (already ate)"),new Vector2(0,-i*40-20),new Vector2(290,36),()=>{targetId=id;targetType=type;UpdatePreview();});row.GetComponent<RectTransform>().anchorMin=row.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,1);rows.Add(row.gameObject);i++;}
  Target(state.GetLeader(),ItemTargetType.Leader);foreach(var unit in state.GetActiveSquadUnits())Target(unit,ItemTargetType.Buddy);targetContent.sizeDelta=new Vector2(0,Mathf.Max(245,i*40));UpdatePreview();
 }
 void UpdatePreview(){var result=CampfireSnackService.BuildPreview(GameState.Instance,itemId,targetType,targetId);preview.text=result.message;feed.interactable=result.success;}
 void Commit(){var result=CampfireSnackService.CommitFeeding(GameState.Instance,itemId,targetType,targetId);Refresh();preview.text=result.message;}
 void OnDestroy(){if(fire!=null)fire.MenuClosed-=Close;if(ui!=null)ui.PopModal(this);if(host!=null)Destroy(host);}
}
