using UnityEngine;
/// <summary>Development-only Pit entrance prompt delegates to the existing tools hub.</summary>
public sealed class CampFightPitInteraction:MonoBehaviour,ICampInteractable,IWorldInteractionMetadata,IProjectedInteractionAnchor
{
 SettlementCampRegion region;CampFightPitLab lab;
 public void Initialize(SettlementCampRegion owner,CampFightPitLab session){region=owner;lab=session;}
 public string GetInteractPrompt()=>"Configure Fight Pit lab";
 public bool CanInteract(GobboController player)=>V1RuntimeProfile.DevelopmentToolsEnabled&&player!=null&&lab!=null&&!lab.Active;
 public Vector2 GetInteractionPoint()=>transform.position;public int InteractionPriority=>4;public float InteractionRange=>1.8f;
 public Vector3 ProjectedPromptAnchor=>region!=null?region.Render(transform.position)+Vector3.up*.4f:Vector3.zero;
 public void Interact(GobboController player){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
 if(CanInteract(player))DevelopmentGameControls.OpenContext();
#endif
 }
}
