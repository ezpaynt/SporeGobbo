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
        public float Until;
    }
    readonly IDiggableTerrain terrain;
    readonly Dictionary<int,Lease> leases=new Dictionary<int,Lease>();
    public int Count=>leases.Count;
    public RegionMovementSlots(IDiggableTerrain terrain){this.terrain=terrain;}
    public Vector2 Choose(ActorTerrainBinding actor,int key,Vector2 center,Vector2 targetExtents,bool contact=false)
    {
        var body=actor.GetComponent<Rigidbody2D>();if(body==null)return center;
        Vector2 extents=TileMover.GetMapClearanceExtents(body,actor.MinimumBodyRadius);
        int id=actor.GetInstanceID();
        if(leases.TryGetValue(id,out var old)&&old.Key==key&&old.Contact==contact&&(old.Extents-extents).sqrMagnitude<.000001f&&(old.TargetExtents-targetExtents).sqrMagnitude<.000001f)
        {
            Vector2 translated=old.Point+center-old.Center;
            if(actor.RouteStatus!=SporeGobbo.Geometry.GridRouteStatus.Unreachable&&Free(id,key,contact,translated,extents))
            {old.Center=center;old.Point=translated;old.Until=Time.time+2;return translated;}
        }
        float best=float.PositiveInfinity;Vector2 chosen=body.position;int selected=-1;
        bool pointDestination=!contact&&targetExtents.sqrMagnitude<.000001f;
        if(pointDestination&&Free(id,key,contact,center,extents))
        {leases[id]=new Lease{Key=key,Index=8,Contact=false,Center=center,Point=center,Extents=extents,TargetExtents=targetExtents,Until=Time.time+2};return center;}
        Vector2 centreBody=targetExtents;
        if(pointDestination)
        {
            centreBody=extents;
            foreach(var pair in leases)if(pair.Key!=id&&pair.Value.Key==key&&pair.Value.Index==8&&pair.Value.Until>=Time.time){centreBody=pair.Value.Extents;break;}
        }
        int start=(id&int.MaxValue)%8;
        for(int n=0;n<8;n++)
        {
            int index=(start+n)%8;
            // A diagonal AABB corner is not contact with a rounded body. Conservative
            // cardinal approaches preserve the real collider contact/attack contract.
            if(contact&&(index&1)!=0)continue;
            if(old!=null&&actor.RouteStatus==SporeGobbo.Geometry.GridRouteStatus.Unreachable&&old.Key==key&&index==old.Index)continue;
            float angle=index*Mathf.PI*.25f;Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            // Contact-adjacent position only; the existing melee checks still authorize hits.
            Vector2 combined=centreBody+extents+Vector2.one*(pointDestination ? .05f : .01f);
            float distance=Mathf.Min(Mathf.Abs(direction.x)>.001f?combined.x/Mathf.Abs(direction.x):float.PositiveInfinity,Mathf.Abs(direction.y)>.001f?combined.y/Mathf.Abs(direction.y):float.PositiveInfinity);
            Vector2 point=center+direction*distance;
            var brain=actor.GetComponent<BuddyBrain>();if(brain!=null&&!brain.AllowsDestination(point))continue;
            if(!Free(id,key,contact,point,extents))continue;
            float cost=(point-body.position).sqrMagnitude;
            if(cost<best){best=cost;chosen=point;selected=index;}
        }
        if(selected<0){Release(actor);return body.position;}
        leases[id]=new Lease{Key=key,Index=selected,Contact=contact,Center=center,Point=chosen,Extents=extents,TargetExtents=targetExtents,Until=Time.time+2};return chosen;
    }
    bool Free(int actor,int key,bool contact,Vector2 point,Vector2 extents)
    {
        if(!TileMover.CanOccupyBox(terrain,point,extents))return false;
        foreach(var pair in leases)
        {
            if(pair.Key==actor||pair.Value.Until<Time.time)continue;
            // Opposing approaches are not a shared gathering destination. Physical
            // colliders and canonical melee checks still govern actual contact.
            if(contact&&pair.Value.Contact&&pair.Value.Key!=key)continue;
            var delta=pair.Value.Point-point;var sum=pair.Value.Extents+extents+Vector2.one*.04f;
            if(Mathf.Abs(delta.x)<sum.x&&Mathf.Abs(delta.y)<sum.y)return false;
        }
        return true;
    }
    public void Release(ActorTerrainBinding actor){if(actor!=null)leases.Remove(actor.GetInstanceID());}
    public void Dispose()=>leases.Clear();
}
