using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Transient body-aware destinations owned by a region, never campaign state.</summary>
public sealed class RegionMovementSlots : IDisposable
{
    sealed class Lease
    {
        public int Key,Index;
        public bool Contact;
        public Vector2 Center,Point,Extents;
        public Vector2 TargetExtents;
        public bool Occupied;
    }
    readonly IDiggableTerrain terrain;
    readonly Dictionary<int,Lease> leases=new Dictionary<int,Lease>();
    readonly Collider2D[] occupants=new Collider2D[32];
    public int Count=>leases.Count;
    public bool HasDestination(ActorTerrainBinding actor)=>actor!=null&&leases.ContainsKey(actor.GetInstanceID());
    public RegionMovementSlots(IDiggableTerrain terrain){this.terrain=terrain;}
    public Vector2 Choose(ActorTerrainBinding actor,int key,Vector2 center,Vector2 targetExtents,bool contact=false)
    {
        var body=actor.GetComponent<Rigidbody2D>();if(body==null)return center;
        Vector2 extents=TileMover.GetMapClearanceExtents(body,actor.MinimumBodyRadius);
        bool pointDestination=!contact&&targetExtents.sqrMagnitude<.000001f;
        int id=actor.GetInstanceID();
        if(leases.TryGetValue(id,out var old)&&old.Key==key&&old.Contact==contact&&(old.Extents-extents).sqrMagnitude<.000001f&&(old.TargetExtents-targetExtents).sqrMagnitude<.000001f)
        {
            Vector2 translated=old.Point+center-old.Center;
            if(actor.RouteStatus!=SporeGobbo.Geometry.GridRouteStatus.Unreachable&&(!pointDestination||TileMover.CanTraverseBox(terrain,center,translated,extents))&&Free(id,key,contact,translated,extents))
            {old.Center=center;old.Point=translated;return translated;}
        }
        float best=float.PositiveInfinity;Vector2 chosen=body.position;int selected=-1;
        if(pointDestination&&Free(id,key,contact,center,extents,body))
        {leases[id]=new Lease{Key=key,Index=24,Contact=false,Center=center,Point=center,Extents=extents,TargetExtents=targetExtents};return center;}
        Vector2 centreBody=targetExtents;
        if(pointDestination)
        {
            centreBody=extents;
            foreach(var pair in leases)if(pair.Key!=id&&pair.Value.Key==key&&pair.Value.Index==24){centreBody=pair.Value.Extents;break;}
            int count=Physics2D.OverlapBoxNonAlloc(center,extents*2+Vector2.one*.04f,0,occupants);
            for(int i=0;i<count;i++)
            {
                var c=occupants[i];var other=c!=null&&!c.isTrigger?c.attachedRigidbody:null;
                if(other==null||other==body||other.bodyType==RigidbodyType2D.Static)continue;
                var delta=(Vector2)c.bounds.center-center;var occupied=(Vector2)c.bounds.extents+new Vector2(Mathf.Abs(delta.x),Mathf.Abs(delta.y));
                centreBody=Vector2.Max(centreBody,occupied);
            }
        }
        int start=(id&int.MaxValue)%8;
        for(int n=0;n<(pointDestination?24:8);n++)
        {
            int ring=n/8;int directionIndex=(start+n)%8;int index=ring*8+directionIndex;
            // A diagonal AABB corner is not contact with a rounded body. Conservative
            // cardinal approaches preserve the real collider contact/attack contract.
            if(contact&&(index&1)!=0)continue;
            if(old!=null&&actor.RouteStatus==SporeGobbo.Geometry.GridRouteStatus.Unreachable&&old.Key==key&&index==old.Index)continue;
            float angle=directionIndex*Mathf.PI*.25f;Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            // Contact-adjacent position only; the existing melee checks still authorize hits.
            Vector2 combined=centreBody+extents+Vector2.one*(pointDestination ? .05f : .01f);
            float distance=Mathf.Min(Mathf.Abs(direction.x)>.001f?combined.x/Mathf.Abs(direction.x):float.PositiveInfinity,Mathf.Abs(direction.y)>.001f?combined.y/Mathf.Abs(direction.y):float.PositiveInfinity);
            if(pointDestination)distance+=ring*(2*Mathf.Max(extents.x,extents.y)+.08f);
            Vector2 point=center+direction*distance;
            var brain=actor.GetComponent<BuddyBrain>();if(brain!=null&&!brain.AllowsDestination(point))continue;
            if(pointDestination&&!TileMover.CanTraverseBox(terrain,center,point,extents))continue;
            if(!Free(id,key,contact,point,extents,pointDestination?body:null))continue;
            float cost=(point-body.position).sqrMagnitude;
            if(cost<best){best=cost;chosen=point;selected=index;}
        }
        if(selected<0){Release(actor);return body.position;}
        leases[id]=new Lease{Key=key,Index=selected,Contact=contact,Center=center,Point=chosen,Extents=extents,TargetExtents=targetExtents};return chosen;
    }
    bool Free(int actor,int key,bool contact,Vector2 point,Vector2 extents,Rigidbody2D body=null)
    {
        if(!TileMover.CanOccupyBox(terrain,point,extents))return false;
        if(body!=null)
        {
            int count=Physics2D.OverlapBoxNonAlloc(point,extents*2+Vector2.one*.04f,0,occupants);
            if(count==occupants.Length)return false;
            for(int i=0;i<count;i++)
            {
                var c=occupants[i];var other=c!=null&&!c.isTrigger?c.attachedRigidbody:null;
                if(other!=null&&other!=body&&other.bodyType!=RigidbodyType2D.Static)return false;
            }
        }
        foreach(var pair in leases)
        {
            if(pair.Key==actor)continue;
            // Opposing approaches are not a shared gathering destination. Physical
            // colliders and canonical melee checks still govern actual contact.
            if(contact&&pair.Value.Contact&&pair.Value.Key!=key)continue;
            var delta=pair.Value.Point-point;var sum=pair.Value.Extents+extents+Vector2.one*.04f;
            if(Mathf.Abs(delta.x)<sum.x&&Mathf.Abs(delta.y)<sum.y)return false;
        }
        return true;
    }
    // Activity ownership survives arrival. Route cancellation and activity release are separate.
    public void MarkOccupied(ActorTerrainBinding actor){if(actor!=null&&leases.TryGetValue(actor.GetInstanceID(),out var lease))lease.Occupied=true;}
    public bool CanPlace(ActorTerrainBinding actor,Vector2 point,Vector2 extents)=>Free(actor.GetInstanceID(),0,false,point,extents);
    public bool ClaimExact(ActorTerrainBinding actor,int key,Vector2 point)
    {
        var body=actor.GetComponent<Rigidbody2D>();var extents=TileMover.GetMapClearanceExtents(body,actor.MinimumBodyRadius);
        if(!Free(actor.GetInstanceID(),key,false,point,extents))return false;
        leases[actor.GetInstanceID()]=new Lease{Key=key,Index=-1,Center=point,Point=point,Extents=extents};return true;
    }
    public bool RelocateStanding(ActorTerrainBinding actor,Vector2 point)
    {
        if(!leases.TryGetValue(actor.GetInstanceID(),out var lease))return true;
        if(!Free(actor.GetInstanceID(),lease.Key,lease.Contact,point,lease.Extents))return false;
        lease.Point=point;lease.Occupied=false;return true;
    }
    public void Release(ActorTerrainBinding actor){if(actor!=null)leases.Remove(actor.GetInstanceID());}
    public void Dispose()=>leases.Clear();
}
