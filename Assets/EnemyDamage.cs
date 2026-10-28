using UnityEngine;

public enum EnemyDamageFailure { None, Invalid, Dead, NotReady, Ineligible, Obstructed, Cancelled, Duplicate, Protected }

/// <summary>A receipt from the existing health owner, never a second health store.</summary>
public readonly struct EnemyDamageResult
{
    public readonly Component Target;
    public readonly int AppliedDamage;
    public readonly bool Killed;
    public readonly EnemyDamageFailure Failure;
    public bool Accepted => Failure == EnemyDamageFailure.None && AppliedDamage > 0;
    public EnemyDamageResult(Component target,int damage,bool killed,EnemyDamageFailure failure=EnemyDamageFailure.None)
    { Target=target;AppliedDamage=Mathf.Max(0,damage);Killed=killed;Failure=failure; }
    public static EnemyDamageResult Reject(Component target,EnemyDamageFailure reason)=>new EnemyDamageResult(target,0,false,reason);
}

public sealed class EnemyDamageRequest
{
    public readonly int amount;
    public readonly string gobboSourceId;
    public readonly GameObject source;
    public readonly bool hadSource;
    public EnemyDamageRequest(int amount,string gobboSourceId="",GameObject source=null)
    {this.amount=Mathf.Max(0,amount);this.gobboSourceId=string.IsNullOrWhiteSpace(gobboSourceId)?"":gobboSourceId.Trim();this.hadSource=!object.ReferenceEquals(source,null);this.source=EnemyDamage.Resolve(source)?.gameObject??source;}
}

public interface IEnemyDamageReceiver { EnemyDamageResult ReceiveEnemyDamage(EnemyDamageRequest request); }

public static class EnemyDamage
{
    public static Component Resolve(GameObject target)
    {
        for(Transform t=target!=null?target.transform:null;t!=null;t=t.parent)
        {
            var enemy=t.GetComponent<IEnemyDamageReceiver>() as Component;
            if(enemy!=null)return enemy;
            if(t.TryGetComponent<GobboController>(out var player))return player;
            if(t.TryGetComponent<BuddyUnit>(out var buddy))return buddy;
        }
        return null;
    }
    public static bool Alive(Component target)
    {
        if(target==null||!target.gameObject.activeInHierarchy||target is Behaviour b&&!b.isActiveAndEnabled)return false;
        if(target is GobboController p)return p.CombatAlive;
        if(target is BuddyUnit u)return u.CombatAlive;
        if(target is TunnelWeevilEnemy w)return w.health>0;
        if(target is BlobSpitterEnemy s)return s.health>0;
        if(target is EnemyHealth e)return e.health>0;
        return false;
    }
    public static bool Ready(GameObject actor)
    {
        actor=Resolve(actor)?.gameObject??actor;
        if(actor==null||!actor.activeInHierarchy)return false;
        var binding=actor.GetComponent<ActorTerrainBinding>();
        return binding==null||binding.Terrain!=null&&(binding.Ready==null||binding.Ready());
    }
    public static bool Eligible(GameObject source,Component target)
    {
        source=Resolve(source)?.gameObject??source;
        if(!Alive(target)||source==target.gameObject||!Ready(target.gameObject))return false;
        if(source!=null&&(!Ready(source)||!Alive(Resolve(source))||!CombatSessionBinding.AllowsActions(source)))return false;
        var a=source!=null?source.GetComponent<CombatSessionBinding>():null;
        var b=target.GetComponent<CombatSessionBinding>();
        if(a!=null&&a.Rules!=null||b!=null&&b.Rules!=null)
            return a!=null&&b!=null&&ReferenceEquals(a.Rules,b.Rules)&&a.Rules!=null&&a.Rules.Allows(source,target.gameObject);
        if(source==null)return true; // Existing direct/environmental damage entry, not an AI target policy.
        bool gobbo=source.GetComponent<GobboController>()!=null||source.GetComponent<BuddyUnit>()!=null;
        bool recipientGobbo=target is GobboController||target is BuddyUnit;
        return gobbo!=recipientGobbo;
    }
    public static bool AttackClear(GameObject source,Component target)
    {
        if(source==null||target==null||!Ready(source)||!Ready(target.gameObject))return false;
        var a=source.GetComponent<ActorTerrainBinding>();var b=target.GetComponent<ActorTerrainBinding>();
        if(a!=null&&b!=null&&!ReferenceEquals(a.Terrain,b.Terrain))return false;
        return ActorTerrainBinding.HasSight(source,source.transform.position,target.transform.position);
    }
    public static EnemyDamageResult TryApply(GameObject target,EnemyDamageRequest request,bool checkObstruction=false)
    {
        Component owner=Resolve(target);
        if(request!=null&&request.hadSource&&request.source==null)return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Cancelled);
        if(owner==null||request==null||request.amount<=0)return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Invalid);
        if(!Alive(owner))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Dead);
        if(!Ready(owner.gameObject)||request.source!=null&&!Ready(request.source))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.NotReady);
        if(!Eligible(request.source,owner))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Ineligible);
        if(checkObstruction&&!AttackClear(request.source,owner))return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Obstructed);
        if(owner is IEnemyDamageReceiver enemy)return enemy.ReceiveEnemyDamage(request);
        if(owner is GobboController player)return player.ReceiveCombatDamage(request);
        if(owner is BuddyUnit buddy)return buddy.ReceiveCombatDamage(request);
        return EnemyDamageResult.Reject(owner,EnemyDamageFailure.Invalid);
    }
    public static bool Apply(GameObject target,int amount,string gobboSourceId="")=>TryApply(target,new EnemyDamageRequest(amount,gobboSourceId)).Accepted;
    // Called by each existing owner immediately before HP mutation. Session policy has no HP of its own.
    public static int Constrain(Component owner,EnemyDamageRequest request,int proposed)
    {
        if(request!=null&&request.hadSource&&request.source==null)return 0;
        if(owner==null||request==null||request.amount<=0||!Alive(owner)||!Ready(owner.gameObject))return 0;
        if(request.source!=null&&!Eligible(request.source,owner))return 0;
        var session=owner.GetComponent<CombatSessionBinding>();
        if(session!=null&&session.Rules!=null&&!Eligible(request.source,owner))return 0;
        var binding=owner.GetComponent<CombatSessionBinding>();
        return binding!=null&&binding.Rules!=null?binding.Rules.LimitDamage(owner.gameObject,request,Mathf.Max(0,proposed)):Mathf.Max(0,proposed);
    }
    public static EnemyDamageResult Receipt(Component owner,int before,int after,EnemyDamageRequest request)
    {
        int lost=Mathf.Max(0,before-Mathf.Max(0,after));
        var result=lost>0?new EnemyDamageResult(owner,lost,after<=0):EnemyDamageResult.Reject(owner,EnemyDamageFailure.Protected);
        var binding=owner!=null?owner.GetComponent<CombatSessionBinding>():null;
        if(binding!=null&&binding.Rules!=null)binding.Rules.AfterDamage(owner.gameObject,request,result);
        return result;
    }
}
