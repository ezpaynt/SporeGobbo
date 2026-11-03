#if UNITY_EDITOR
using UnityEngine;
// Fixture-only intent producer. No collision decisions, direct velocity writes or physics overrides.
// Runs before the production RegionRoutePump (2000) and after ordinary gameplay controllers.
[DefaultExecutionOrder(1000)]
public sealed class Stage64DContactProbe : MonoBehaviour
{
    public Vector2 Requested;
    void Awake(){if(!SporeGobbo.Safety.Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!Application.isPlaying)throw new System.InvalidOperationException("Owned editor contact fixture only");}
    void FixedUpdate()
    {
        var body=GetComponent<Rigidbody2D>();
        var binding=GetComponent<ActorTerrainBinding>();
        if(GetComponent<GobboController>()!=null)TileMover.Move(body,Requested,binding.MinimumBodyRadius);
        else ActorMotionIntent.Submit(this,body,Requested,binding.MinimumBodyRadius,1000,false);
    }
    void OnDisable(){ActorMotionIntent.Cancel(gameObject);}
}
#endif
