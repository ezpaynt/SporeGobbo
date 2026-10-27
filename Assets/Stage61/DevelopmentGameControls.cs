#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SporeGobbo.Safety;
/// <summary>Development-only commands through real services; stores no campaign facts.</summary>
[DefaultExecutionOrder(-9000)]
public sealed class DevelopmentGameControls:MonoBehaviour
{
    bool visible,modal;string message="";SporeUiCoordinator ui;
    void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;}
    void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;}
    void SceneLoaded(Scene scene,LoadSceneMode mode){if(mode==LoadSceneMode.Single)modal=false;}
    void Update()
    {
        if(Keyboard.current!=null&&Keyboard.current.f9Key.wasPressedThisFrame)visible=!visible;
        ui=SporeUiCoordinator.Instance;bool hover=visible&&Mouse.current!=null&&Mouse.current.position.ReadValue().x<370&&Mouse.current.position.ReadValue().y>Screen.height-530;
        if(hover!=modal&&ui!=null){modal=hover;if(hover)ui.PushModal(this,null,false);else ui.PopModal(this);}
    }
    void Command(Action command){try{command();message="Done through real game services.";}catch(Exception error){message=error.Message;Debug.LogWarning("[Development control] "+message);}}
    void OnGUI()
    {
        var intro=UnityEngine.Object.FindAnyObjectByType<IntroRegion>();if(intro!=null&&!intro.ControlReady)return;
        GUI.Label(new Rect(12,Screen.height-55,Screen.width-24,25),"ISOLATED DEVELOPMENT GAME — F9 campaign/lifecycle controls / F10 Camp controls");
        if(!visible)return;
        GUILayout.BeginArea(new Rect(8,8,355,520),GUI.skin.box);GUILayout.Label("Development A / B / C — owned storage");GUILayout.Label(Persistence.ExperimentRoot);
        bool menu=SceneManager.GetActiveScene().name=="MainMenu";
        if(menu)
        {
            for(int slot=1;slot<=3;slot++){int owned=slot;var status=SporeSaveManager.InspectSlot(slot);GUILayout.Label("Campaign "+(char)('A'+slot-1)+": "+status);
                if(status==SporeSaveManager.SlotStatus.Missing&&GUILayout.Button("Import "+(slot==1?"fresh A":slot==2?"established ten-bed B":"recovery C")))Command(()=>DevelopmentCampaignFixtures.Import(owned));
                if(status==SporeSaveManager.SlotStatus.RecoveryAvailable&&GUILayout.Button("Recover owned campaign "+slot))Command(()=>SporeSaveManager.RecoverDevelopmentSlot(owned));
            }
            if(GUILayout.Button("Reset owned Fresh A (archive previous A)"))Command(()=>{SporeSaveManager.DeleteSlot(1);DevelopmentCampaignFixtures.Import(1);});
        }
        var controller=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();
        if(controller!=null&&!controller.WorldInputReady&&GUILayout.Button("Retry ready Camp arrival"))Command(controller.RetryDevelopmentArrival);
        bool normal=SceneManager.GetActiveScene().name=="SampleScene"&&!SampleSceneModeController.IsIntroMode;
        var player=UnityEngine.Object.FindAnyObjectByType<GobboController>();
        if(normal&&player!=null)
        {
            if(GUILayout.Button("Grant one spore — then plant with Q"))Command(()=>player.GetComponent<SporeInventory>().AddSpore(1));
            if(GUILayout.Button("Return normally through real service"))Command(()=>RunReturnService.ReturnToCamp());
            if(GUILayout.Button("Retreat through real service"))Command(()=>RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.Retreat,"development controls"));
            if(GUILayout.Button("Fatal damage — real death/succession"))Command(()=>player.TakeDamage(1000000));
        }
        if(!menu&&GUILayout.Button("Recover last committed campaign to Main Menu"))Command(()=>{int slot=SporeSaveManager.GetCurrentSlot();if(!SporeSaveManager.LoadSlotIntoGameState(slot))throw new InvalidOperationException("No validated campaign to recover");SporePauseService.ResetAll();SceneManager.LoadScene("MainMenu");});
        GUILayout.Label(message);GUILayout.EndArea();
    }
    void OnDestroy(){if(modal&&ui!=null)ui.PopModal(this);}
}
#endif
