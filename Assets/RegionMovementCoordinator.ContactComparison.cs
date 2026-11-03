using System;
using UnityEngine;

// Expedition relationship policy owned by the existing regional coordinator.
// Membership is declared by canonical squad spawning through Configure, never species inference.
public sealed partial class RegionMovementCoordinator
{
    bool expeditionCooperativeContact=true;
    bool OrdinaryExpeditionPair(Actor a,Actor b)=>!camp&&Cooperative(a,b)&&
        a.Binding.GetComponent<CombatSessionBinding>()?.Rules==null&&
        b.Binding.GetComponent<CombatSessionBinding>()?.Rules==null;
    bool PassThrough(Actor a,Actor b)=>expeditionCooperativeContact&&OrdinaryExpeditionPair(a,b)&&a.Human!=b.Human;
    bool ExpeditionSoft(Actor a,Actor b)=>expeditionCooperativeContact&&!a.Human&&!b.Human&&OrdinaryExpeditionPair(a,b);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Retained solely to compare legacy strict Buddy coordination. Player passage stays explicit.
    public void SetDiagnosticCooperativeContact(bool enabled)
    {
        if(!SporeGobbo.Safety.Persistence.Isolated||!V1RuntimeProfile.DevelopmentToolsEnabled)
            throw new InvalidOperationException("Owned development contact comparison only");
        if(expeditionCooperativeContact!=enabled)
        {
            expeditionCooperativeContact=enabled;
            foreach(var actor in actors)EndWait(actor,"DiagnosticContactPolicyChanged");
        }
        contactSignature=int.MinValue;RefreshContacts();
    }
#endif
}
