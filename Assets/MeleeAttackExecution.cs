using System.Collections.Generic;
using UnityEngine;

/// <summary>One caller-owned instant action. No clock/cooldown, global registry or retained hit history.</summary>
public sealed class MeleeAttackExecution
{
    readonly GameObject source;
    readonly HashSet<int> recipients=new HashSet<int>();
    bool cancelled;
    public MeleeAttackExecution(GameObject source){this.source=source;}
    public void Cancel()=>cancelled=true;
    public EnemyDamageResult Commit(GameObject candidate,int damage,string sourceId="")
    {
        Component owner=EnemyDamage.Resolve(candidate);
        if(cancelled||source==null||!CombatSessionBinding.AllowsActions(source))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Cancelled);
        if(owner==null)return EnemyDamageResult.Reject(null,EnemyDamageFailure.Invalid);
        if(!recipients.Add(owner.GetInstanceID()))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Duplicate);
        return EnemyDamage.TryApply(owner.gameObject,new EnemyDamageRequest(damage,sourceId,source),true);
    }
    public static Collider2D[] Footprint(Vector2 center,float radius,LayerMask layers)
        =>layers.value==0?Physics2D.OverlapCircleAll(center,radius):Physics2D.OverlapCircleAll(center,radius,layers);
}
