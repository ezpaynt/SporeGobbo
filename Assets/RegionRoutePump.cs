using UnityEngine;
/// <summary>Lifetime-owned by existing region physics; never registers a second terrain service.</summary>
public sealed class RegionRoutePump : MonoBehaviour
{
    public RegionRouteQueue Queue;
    void Update(){if(Time.deltaTime>0)Queue?.Advance();}
    void OnDestroy(){Queue?.Dispose();Queue=null;}
}
