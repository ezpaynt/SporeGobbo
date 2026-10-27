using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Failure-only normal UI. Commands delegate to the existing return/save authority.</summary>
public sealed class RunReturnFailureView : MonoBehaviour
{
    public static void Show(Object owner)
    {
        var component=owner as Component;
        if(component!=null&&component.GetComponent<RunReturnFailureView>()==null)component.gameObject.AddComponent<RunReturnFailureView>();
    }
    void OnGUI()
    {
        if(string.IsNullOrEmpty(RunReturnService.LastFailure))return;
        GUILayout.BeginArea(new Rect(Screen.width*.5f-240,Screen.height*.5f-100,480,210),GUI.skin.box);
        GUILayout.Label(RunReturnService.LastFailure);
        if(!RunReturnService.DevelopmentPreparationFailed&&GUILayout.Button("Retry return to Camp"))
        {
            if(RunReturnService.HasPreparedCampSimulation)RunReturnService.ReturnSimulatedCampOutcome(()=>{});
            else RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnService.LastReturnReason,"Return recovery UI");
        }
        if(GUILayout.Button("Return to Main Menu")){SporePauseService.ResetAll();SceneManager.LoadScene(GameSceneCatalog.MainMenu);}
        GUILayout.EndArea();
    }
}
