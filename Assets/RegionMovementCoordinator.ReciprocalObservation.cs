#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SporeGobbo.Safety;

// Bounded observation only. Never publishes motion, requests a route or mutates a cursor/lease.
public static class Stage64DReciprocalObservation
{
    [Serializable] public sealed class Row
    {
        public string kind,key,peer,detail,task,waitTask,peerTask,waitPeerTask,waitPeer,waitStamp,currentStamp;
        public float time;public double tick;public int region,cursor,revision,waitRevision,pathId,option,taskId,waitTaskId,peerTaskId,waitPeerTaskId;
        public long ticket,peerTicket;public bool waiting,noPlan,selected,ready,peerReady,terrainClear,boundsClear,peersClear,prefixClear,pairClear,hypothetical,following,peerLive,cooperative;
        public MovementContactRole role,waitRole,peerRole,waitPeerRole;
        public Vector2 position,extents,preferred,resolved,goal,end,delta,peerPosition,peerExtents,peerPreferred,waitPeerPosition,waitGoal,waitOwnDirection,waitPeerDirection,waitExtents,waitPeerExtents;
        public string blockedPeers;public Vector2[] path;
    }
    static readonly List<Row> rows=new List<Row>(512);
    static readonly Dictionary<string,float> sampled=new Dictionary<string,float>();
    static bool active;static int region,triggers,dropped,pathDropped;static double lastTrigger=-1;
    public static bool Active=>active;
    public static void Begin(){if(!Persistence.Isolated)throw new InvalidOperationException("Owned reciprocal observer only");rows.Clear();sampled.Clear();region=triggers=dropped=pathDropped=0;lastTrigger=-1;active=true;}
    public static bool Watching(int id)=>active&&region==id&&triggers>0;
    public static bool Trigger(int id,double tick)
    {if(!active||triggers>=4||lastTrigger==tick)return false;region=id;lastTrigger=tick;triggers++;return true;}
    public static bool Sample(int id,string key,float time)
    {if(!Watching(id)||sampled.TryGetValue(key,out var before)&&time-before<.25f)return false;sampled[key]=time;return true;}
    public static void Add(Row row){if(rows.Count>=512){dropped++;return;}rows.Add(row);}
    public static Vector2[] CopyPath(IReadOnlyList<Vector2> path)
    {if(path==null)return Array.Empty<Vector2>();if(path.Count>128){pathDropped+=path.Count;return Array.Empty<Vector2>();}var copy=new Vector2[path.Count];for(int i=0;i<copy.Length;i++)copy[i]=path[i];return copy;}
    public static void Export(string directory)
    {
        if(!active)return;active=false;
        using(var f=new StreamWriter(Path.Combine(directory,"reciprocal-observation.jsonl")))foreach(var row in rows)f.WriteLine(JsonUtility.ToJson(row));
        File.WriteAllText(Path.Combine(directory,"reciprocal-observer-status.txt"),$"rows={rows.Count}\ncapacity=512\ntriggers={triggers}\ntriggerLimit=4\ndropped={dropped}\npathDropped={pathDropped}\nObserver-only query evidence, not native counterfactual movement. Hypothetical endpoints keep all other peers frozen; no route searches or writes. Bounds at hypothetical route nodes use the actual actor's command context. Not performance evidence.\n");
        rows.Clear();sampled.Clear();
    }
}

public sealed partial class RegionMovementCoordinator
{
    static bool ObservedPair(Actor a)=>a!=null&&(a.Key=="development-b-buddy-1"||a.Key=="development-b-buddy-4");
    Stage64DReciprocalObservation.Row ObserveActor(string kind,Actor a,Actor peer)
    {
        var r=new Stage64DReciprocalObservation.Row{kind=kind,key=a.Key,time=Time.realtimeSinceStartup,tick=lastTick,region=GetHashCode(),position=a.Position,extents=a.Extents,preferred=a.Preferred,resolved=a.Resolved,goal=a.Binding.CurrentMovementGoal,ticket=a.Ticket,waiting=a.Waiting,noPlan=a.NoPlan,selected=a.Selected,ready=a.Ready,revision=a.Binding.Revision,waitRevision=a.WaitRevision,role=a.Role,waitRole=a.WaitRole,task=a.Motion?.SelectedTask?.GetType().Name??"None",waitTask=a.WaitTask?.GetType().Name??"None",waitPeer=a.WaitPeer?.Key??"",waitPeerPosition=a.WaitPeerPosition,waitGoal=a.WaitGoal,waitOwnDirection=a.WaitOwnDirection,waitPeerDirection=a.WaitPeerDirection,waitExtents=a.WaitExtents,waitPeerExtents=a.WaitPeerExtents,waitStamp=a.WaitSpaceStamp.ToString(),waitPeerTask=a.WaitPeerTask?.GetType().Name??"None",waitPeerRole=a.WaitPeerRole};
        r.taskId=a.Motion?.SelectedTask?.GetInstanceID()??0;r.waitTaskId=a.WaitTask?.GetInstanceID()??0;r.waitPeerTaskId=a.WaitPeerTask?.GetInstanceID()??0;
        if(peer!=null){r.peerLive=Live(peer);r.cooperative=Cooperative(a,peer);r.peerTaskId=peer.Motion?.SelectedTask?.GetInstanceID()??0;r.peer=peer.Key;r.peerTicket=peer.Ticket;r.peerPosition=peer.Position;r.peerExtents=peer.Extents;r.peerPreferred=peer.Preferred;r.peerReady=peer.Ready;r.peerRole=peer.Role;r.peerTask=peer.Motion?.SelectedTask?.GetType().Name??"None";r.currentStamp=LocalSpaceStamp(a,peer).ToString();}
        var follow=a.Binding.GetComponent<BuddyFollow>();var brain=a.Binding.GetComponent<BuddyBrain>();r.following=brain!=null&&brain.Following;
        if(follow!=null){r.cursor=follow.ObservedCursor;r.pathId=Stage64DRouteObservation.Id(follow.ObservedPath);}
        return r;
    }
    bool ObserveConnector(string kind,Actor a,Vector2 from,Vector2 end,Actor held=null,Vector2 heldPosition=default,int option=-1)
    {
        var r=ObserveActor(kind,a,held);r.position=from;r.end=end;r.delta=end-from;r.option=option;r.hypothetical=held!=null||from!=a.Position;
        r.terrainClear=a.Ready&&TileMover.CanTraverseBox(terrain,from,end,a.Extents);
        r.boundsClear=a.Motion==null||(a.Motion.Bound(r.delta,1)-r.delta).sqrMagnitude<=.000001f;
        r.blockedPeers="";
        foreach(var other in order)
        {
            if(other==a||!other.Live||PassThrough(a,other))continue;var p=other==held?heldPosition:other.Position;
            if(!MovementContactPolicy.PeerSweepClear(from-p,r.delta,a.Extents,other.Extents,Soft(a,other),Physics2D.defaultContactOffset))r.blockedPeers+=(r.blockedPeers.Length==0?"":"|")+other.Key;
        }
        r.peersClear=r.blockedPeers.Length==0;Stage64DReciprocalObservation.Add(r);return r.terrainClear&&r.boundsClear&&r.peersClear;
    }
    void ObserveReciprocal(string stage,Actor a,Actor peer)
    {
        if(!Stage64DReciprocalObservation.Active||camp||!ObservedPair(a))return;
        int id=GetHashCode();
        if(stage=="Snapshot"&&Stage64DReciprocalObservation.Sample(id,a.Key,Time.realtimeSinceStartup))Stage64DReciprocalObservation.Add(ObserveActor("WaitSnapshot",a,a.WaitPeer));
        if(stage=="WaitContextChanged"&&Stage64DReciprocalObservation.Watching(id))Stage64DReciprocalObservation.Add(ObserveActor("WaitContextChanged",a,peer));
        if(stage!="NoLocalYieldPlan"||!ObservedPair(peer)||peer==a)return;
        var four=a.Key=="development-b-buddy-4"?a:peer;var one=a==four?peer:a;
        if((four.Position-new Vector2(22.8000011f,29.0853958f)).sqrMagnitude>1||
           (one.Position-new Vector2(23.3939114f,29.9704037f)).sqrMagnitude>1)return;
        Stage64DReciprocalObservation.Add(ObserveActor("WaitTransition",a,peer));
        if(!Stage64DReciprocalObservation.Trigger(id,lastTick))return;
        foreach(var actor in new[]{four,one})
        {
            var row=ObserveActor("ActualPublishedRoute",actor,actor==four?one:four);var follow=actor.Binding.GetComponent<BuddyFollow>();row.path=Stage64DReciprocalObservation.CopyPath(follow?.ObservedPath);Stage64DReciprocalObservation.Add(row);
            ObserveConnector("RequestedStepOthersHeld",actor,actor.Position,actor.Position+actor.Preferred*resolvedDelta);
        }
        var pair=ObserveActor("BothRequestedPair",four,one);pair.pairClear=PairClear(four,four.Preferred,one,one.Preferred,resolvedDelta);Stage64DReciprocalObservation.Add(pair);
        var path=one.Binding.GetComponent<BuddyFollow>();var nodes=path?.ObservedPath;int cursor=path?.ObservedCursor??-1;
        if(nodes==null||cursor<0||cursor>=nodes.Count)return;
        bool prefix=true;var start=one.Position;
        for(int i=cursor;i<Mathf.Min(nodes.Count,cursor+3);i++)
        {
            prefix&=ObserveConnector("Buddy1ExistingRouteSegmentOthersHeld",one,start,nodes[i],null,default,i);
            var mark=ObserveActor("Buddy1FrozenRoutePrefix",one,four);mark.end=nodes[i];mark.option=i;mark.prefixClear=prefix;mark.hypothetical=true;Stage64DReciprocalObservation.Add(mark);
            ObserveConnector("Buddy4StepAfterBuddy1AtExistingNode",four,four.Position,four.Position+four.Preferred*resolvedDelta,one,nodes[i],i);
            start=nodes[i];
        }
    }
}
#endif
