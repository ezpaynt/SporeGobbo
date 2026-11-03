#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SporeGobbo.Input;

// Bounded, read-only observer. No input/aim/action/clock/health writes or replacement dispatch.
public static class Stage64DPlayerAttackObservation
{
    [Serializable] struct Row
    {
        public int stage,frame,update,device,context,target,code,hits;
        public double wall,eventTime;
        public bool raw,held,started,buffered,suppressed,actionEnabled,actionPressed,pointerValid;
        public float actionValue,eventValue,cooldown,range,radius;
        public Vector2 position,targetPosition,pointer,inverse,aim,queued;
    }
    static GobboController subject;static GameObject opponent;static Mouse mouse;
    static InputAction primary;static Row[] rows;static int count,dropped,editorUpdates;
    static double begin;static Vector2 queued;
    public static void Begin(GobboController player,GameObject target,Mouse device)
    {
        if(subject!=null||!Application.isPlaying||!SporeGobbo.Safety.Persistence.Isolated)throw new InvalidOperationException("Owned isolated player observer only");
        rows=new Row[4096];count=dropped=editorUpdates=0;subject=player;opponent=target;mouse=device;begin=Time.realtimeSinceStartupAsDouble;
        var actions=(InputActionAsset)typeof(SporeInputReader).GetField("actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(SporeInputReader.Instance);
        primary=actions.FindAction("Gameplay/PrimaryAttack",true);
        InputSystem.onEvent+=Event;InputSystem.onAfterUpdate+=AfterInput;
    }
    static bool Active=>subject!=null&&Time.realtimeSinceStartupAsDouble-begin<8;
    static Row New(int stage)
    {
        var input=SporeInputReader.Instance;
        return new Row{stage=stage,frame=Time.frameCount,update=(int)InputState.currentUpdateType,wall=Time.realtimeSinceStartupAsDouble,
            context=input!=null?(int)input.Context:-1,raw=mouse!=null&&mouse.leftButton.isPressed,
            held=input!=null&&input.PrimaryAttack.IsHeld,started=input!=null&&input.PrimaryAttack.StartedThisFrame,
            suppressed=subject.SuppressTemporaryInput||input!=null&&input.GameplayButtonsSuppressed,
            actionEnabled=primary!=null&&primary.enabled,actionPressed=primary!=null&&primary.IsPressed(),actionValue=primary!=null?primary.ReadValue<float>():0,
            position=subject.transform.position,targetPosition=opponent!=null?(Vector2)opponent.transform.position:default,
            pointer=input!=null?input.AimPointer:default,aim=subject.CurrentAimDirection,range=subject.attackRange,radius=subject.attackRadius,queued=queued};
    }
    static void Add(Row r){if(count<rows.Length)rows[count++]=r;else dropped++;}
    public static void Queue(GobboController player,Vector2 pointer,bool after)
    {if(!Active||player!=subject)return;queued=pointer;Add(New(after?1:0));}
    static void Event(InputEventPtr e,InputDevice d)
    {
        if(!Active||d!=mouse||!e.IsA<StateEvent>()&&!e.IsA<DeltaStateEvent>())return;
        var r=New(2);r.device=d.deviceId;r.eventTime=e.time;r.eventValue=mouse.leftButton.ReadValueFromEvent(e);Add(r);
    }
    static void AfterInput()
    {if(!Active)return;if(InputState.currentUpdateType==InputUpdateType.Editor){editorUpdates++;return;}Add(New(3));}
    public static void UpdateEnd(GobboController player)
    {
        if(!Active||player!=subject)return;var r=New(4);var input=SporeInputReader.Instance;
        r.pointerValid=input!=null&&ActorTerrainBinding.TryPointer(player.gameObject,input,Camera.main,out r.inverse);Add(r);
    }
    public static void Consumed(GobboController player,bool buffered,float cooldown)
    {if(!Active||player!=subject)return;var r=New(5);r.buffered=buffered;r.cooldown=cooldown;Add(r);}
    public static void Attempt(GobboController player,float cooldown)
    {if(!Active||player!=subject)return;var r=New(6);r.cooldown=cooldown;Add(r);}
    public static void Commit(GobboController player,Collider2D candidate,EnemyDamageResult result)
    {if(!Active||player!=subject)return;var r=New(7);r.target=result.Target!=null?result.Target.GetInstanceID():candidate.GetInstanceID();r.code=(int)result.Failure;r.hits=result.AppliedDamage;Add(r);}
    public static void Complete(GobboController player,int hits)
    {if(!Active||player!=subject)return;var r=New(8);r.hits=hits;Add(r);}
    public static int End()
    {
        InputSystem.onEvent-=Event;InputSystem.onAfterUpdate-=AfterInput;
        if(rows==null)return 0;
        Debug.Log($"ATTACK_OBSERVER_STATUS count={count} dropped={dropped} editorUpdatesNotSampled={editorUpdates} stages=0:queued,1:dispatched,2:event,3:inputUpdate,4:playerUpdateEnd,5:consumed,6:attempt,7:commit,8:complete");
        for(int i=0;i<count;i++)Debug.Log("ATTACK_OBSERVER "+JsonUtility.ToJson(rows[i]));
        int loss=dropped;subject=null;opponent=null;mouse=null;primary=null;rows=null;return loss;
    }
}
#endif
