using UnityEngine;
/// <summary>Lifetime-owned by existing region physics; never registers a second terrain service.</summary>
[DefaultExecutionOrder(2000)]
public sealed class RegionRoutePump : MonoBehaviour
{
    public RegionRouteQueue Queue;
    public RegionMovementCoordinator Movement;
    void Update(){if(Time.deltaTime>0)Queue?.Advance();}
    void FixedUpdate(){Movement?.Resolve(Time.fixedTimeAsDouble,Time.fixedDeltaTime);}
    void OnDestroy(){Movement?.Dispose();Movement=null;Queue?.Dispose();Queue=null;}
}
