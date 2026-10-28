using UnityEngine;

/// <summary>Temporary eligibility/nonlethal policy; no roster, health, AI or save authority.</summary>
public interface ICombatSessionRules
{
    bool Allows(GameObject source,GameObject target);
    int LimitDamage(GameObject target,EnemyDamageRequest request,int proposedDamage);
    void AfterDamage(GameObject target,EnemyDamageRequest request,EnemyDamageResult result);
    bool ActionsAllowed(GameObject actor);
}
public sealed class CombatSessionBinding:MonoBehaviour
{
    public ICombatSessionRules Rules {get;set;}
    public int Team {get;set;}
    public bool ActionsAllowed=>Rules!=null&&Rules.ActionsAllowed(gameObject);
    public static bool AllowsActions(GameObject actor)
    {var b=actor!=null?actor.GetComponent<CombatSessionBinding>():null;return b==null||b.Rules==null||b.ActionsAllowed;}
    void OnDestroy(){Rules=null;}
}

/// <summary>Transient match recipients, preserving the real wheel and Buddy command executor.</summary>
public interface ICombatCommandSession
{
 System.Collections.Generic.IEnumerable<BuddyBrain> Recipients(GobboController leader);
 bool CanControl(GobboController leader,BuddyBrain buddy);
}
/// <summary>Cancel transient activity before the existing campaign authority captures facts.</summary>
public interface ICampaignTransientActivity { void FinishForCampaignCapture(GameState campaign);void FinishForCampaignRestore(GameState campaign); }
