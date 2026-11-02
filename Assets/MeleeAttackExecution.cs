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

    // Existing Buddy contact fallback, shared without changing its tolerance or ownership rules.
    // Caller-owned lists avoid allocating component arrays during range checks.
    public static bool AtBodyContact(GameObject source,Rigidbody2D sourceBody,Component target,
        List<Collider2D> sourceColliders,List<Collider2D> targetColliders)
    {
        if(source==null||target==null)return false;
        source.GetComponentsInChildren<Collider2D>(false,sourceColliders);
        target.GetComponentsInChildren<Collider2D>(false,targetColliders);
        var targetBody=target.GetComponent<Rigidbody2D>();
        float tolerance=Physics2D.defaultContactOffset*2f+.001f;
        foreach(var a in sourceColliders)
        {
            if(a==null||!a.enabled||a.isTrigger||a.attachedRigidbody!=sourceBody||EnemyDamage.Resolve(a.gameObject)?.gameObject!=source)continue;
            foreach(var b in targetColliders)
            {
                if(b==null||!b.enabled||b.isTrigger||b.attachedRigidbody!=targetBody||EnemyDamage.Resolve(b.gameObject)!=target)continue;
                var distance=a.Distance(b);if(distance.isValid&&distance.distance<=tolerance)return true;
            }
        }
        return false;
    }

    // Geometry only: same target ownership/layer selection as the existing circle hit query.
    // Timing, eligibility, obstruction and final damage remain with their existing owners.
    public static bool TargetInsideCircle(Component target,Vector2 center,float radius,LayerMask layers,
        List<Collider2D> targetColliders)
    {
        if(target==null)return false;
        target.GetComponentsInChildren<Collider2D>(false,targetColliders);
        float squared=Mathf.Max(0,radius)*Mathf.Max(0,radius);
        foreach(var c in targetColliders)
            if(c!=null&&c.enabled&&(layers.value==0||(layers.value&(1<<c.gameObject.layer))!=0)&&
               EnemyDamage.Resolve(c.gameObject)==target&&((Vector2)c.ClosestPoint(center)-center).sqrMagnitude<=squared)return true;
        return false;
    }
}
