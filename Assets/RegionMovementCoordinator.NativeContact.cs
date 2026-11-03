using UnityEngine;

// Part of the existing regional contact owner, not another registry or physics authority.
public sealed partial class RegionMovementCoordinator
{
    sealed class NativeContactFilter
    {
        public readonly Collider2D Collider;
        public int Wanted;
        int include,exclude;bool owned;
        public bool Owned=>owned;
        public NativeContactFilter(Collider2D collider){Collider=collider;}
        public int BaseInclude=>owned?include:Collider.includeLayers.value;
        public int BaseExclude=>owned?exclude:Collider.excludeLayers.value;
        public void Apply()
        {
            if(Collider==null)return;
            if(Wanted==0){Restore();return;}
            if(!owned){include=Collider.includeLayers;exclude=Collider.excludeLayers;owned=true;}
            int nextInclude=include|Wanted,nextExclude=exclude&~Wanted;
            if(Collider.includeLayers.value!=nextInclude)Collider.includeLayers=nextInclude;
            if(Collider.excludeLayers.value!=nextExclude)Collider.excludeLayers=nextExclude;
        }
        public void Restore()
        {
            if(owned&&Collider!=null){Collider.includeLayers=include;Collider.excludeLayers=exclude;}
            owned=false;Wanted=0;
        }
    }
    public int OwnedNativeFilterLeases
    {get{int count=0;foreach(var a in actors)foreach(var f in a.Filters)if(f.Owned)count++;return count;}}
    bool OpposingContact(Contact p)
    {
        if(!camp||!Live(p.A)||!Live(p.B)||p.CA==null||p.CB==null||!p.CA.isActiveAndEnabled||!p.CB.isActiveAndEnabled)return false;
        var a=p.A.Binding.GetComponent<CombatSessionBinding>();var b=p.B.Binding.GetComponent<CombatSessionBinding>();
        return a?.Rules is IMovementContactSession roles&&ReferenceEquals(a.Rules,b?.Rules)&&a.Team>=0&&b.Team>=0&&a.Team!=b.Team&&
            roles.IsMovementParticipant(p.A.Binding.gameObject)&&roles.IsMovementParticipant(p.B.Binding.gameObject)&&
            a.Rules.ActionsAllowed(p.A.Binding.gameObject)&&a.Rules.ActionsAllowed(p.B.Binding.gameObject);
    }
    static NativeContactFilter Filter(Actor a,Collider2D c)
    {foreach(var f in a.Filters)if(f.Collider==c)return f;return null;}
    // Unity 6000.4's documented include/exclude arbitration, including attached-body masks.
    // Native contact regressions independently verify this prediction with real simulation.
    static bool LayerDecision(Collider2D a,Collider2D b,int includeA,int excludeA,int includeB,int excludeB)
    {
        bool matrix=!Physics2D.GetIgnoreLayerCollision(a.gameObject.layer,b.gameObject.layer);
        int bitA=1<<a.gameObject.layer,bitB=1<<b.gameObject.layer;
        var ra=a.attachedRigidbody;var rb=b.attachedRigidbody;
        includeA|=ra!=null?ra.includeLayers.value:0;excludeA|=ra!=null?ra.excludeLayers.value:0;
        includeB|=rb!=null?rb.includeLayers.value:0;excludeB|=rb!=null?rb.excludeLayers.value:0;
        bool decisionA=(matrix||(includeA&bitB)!=0)&&(excludeA&bitB)==0;
        bool decisionB=(matrix||(includeB&bitA)!=0)&&(excludeB&bitA)==0;
        if(decisionA==decisionB)return decisionA;
        bool overrideA=((includeA|excludeA)&bitB)!=0,overrideB=((includeB|excludeB)&bitA)!=0;
        if(overrideA!=overrideB)return overrideA?decisionA:decisionB;
        if(a.layerOverridePriority==b.layerOverridePriority)return false;
        return a.layerOverridePriority>b.layerOverridePriority?decisionA:decisionB;
    }
    public static bool EffectiveNativeContact(Collider2D a,Collider2D b)
        =>a!=null&&b!=null&&!a.isTrigger&&!b.isTrigger&&a.isActiveAndEnabled&&b.isActiveAndEnabled&&
          (a.attachedRigidbody==null||a.attachedRigidbody.simulated)&&(b.attachedRigidbody==null||b.attachedRigidbody.simulated)&&
          !Physics2D.GetIgnoreCollision(a,b)&&LayerDecision(a,b,a.includeLayers,a.excludeLayers,b.includeLayers,b.excludeLayers);
    bool BaseLayerDecision(Contact p)
    {
        var a=Filter(p.A,p.CA);var b=Filter(p.B,p.CB);
        return LayerDecision(p.CA,p.CB,a.BaseInclude,a.BaseExclude,b.BaseInclude,b.BaseExclude);
    }
    void ApplyNativeContactLeases()
    {
        foreach(var actor in actors)foreach(var filter in actor.Filters)filter.Wanted=0;
        foreach(var p in contacts)if(OpposingContact(p)&&!BaseLayerDecision(p))
        {
            Filter(p.A,p.CA).Wanted|=1<<p.CB.gameObject.layer;
            Filter(p.B,p.CB).Wanted|=1<<p.CA.gameObject.layer;
        }
        foreach(var actor in actors)foreach(var filter in actor.Filters)filter.Apply();
        foreach(var p in contacts)
        {
            if(p.CA==null||p.CB==null)continue;
            bool required=OpposingContact(p);
            // A layer-wide include must not add contact to registered non-opponents.
            bool preserveExcluded=!required&&!BaseLayerDecision(p)&&
                LayerDecision(p.CA,p.CB,p.CA.includeLayers,p.CA.excludeLayers,p.CB.includeLayers,p.CB.excludeLayers);
            p.Apply(Live(p.A)&&Live(p.B)&&(Soft(p.A,p.B)||PassThrough(p.A,p.B)),required,preserveExcluded);
        }
    }
    int NativeContactSignature()
    {
        int signature=actors.Count;
        foreach(var a in actors)
        {
            a.Role=Role(a.Binding);var session=a.Binding.GetComponent<CombatSessionBinding>();
            unchecked
            {
                signature=signature*31+(int)a.Role;signature=signature*31+a.Group;
                signature=signature*31+(session?.Rules?.GetHashCode()??0);signature=signature*31+(session!=null?session.Team:0);
                signature=signature*31+(Live(a)?1:0);signature=signature*31+(session?.Rules?.ActionsAllowed(a.Binding.gameObject)==true?1:0);
                signature=signature*31+(session?.Rules is IMovementContactSession roles&&roles.IsMovementParticipant(a.Binding.gameObject)?1:0);
                foreach(var c in a.Colliders)
                {
                    signature=signature*31+(c!=null&&c.isActiveAndEnabled?1:0);if(c==null)continue;
                    signature=signature*31+c.gameObject.layer;signature=signature*31+c.includeLayers.value;signature=signature*31+c.excludeLayers.value;
                    signature=signature*31+c.layerOverridePriority;
                }
                if(a.Body!=null){signature=signature*31+a.Body.includeLayers.value;signature=signature*31+a.Body.excludeLayers.value;}
            }
        }
        return signature;
    }
}
