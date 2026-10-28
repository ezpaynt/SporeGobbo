using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    public static int LaunchCount{get;private set;}
    public ICombatSessionRules SessionRules{get;private set;}
    [Header("Projectile")]
    public float speed = 5f;
    public int damage = 12;
    public float lifetime = 4f;
    public LayerMask hitLayers;
    public bool destroyOnHit = true;

    readonly RaycastHit2D[] sweepHits=new RaycastHit2D[32];
    readonly System.Collections.Generic.HashSet<int> contacts=new System.Collections.Generic.HashSet<int>();
    bool spent;
    public static int TerrainImpacts{get;private set;}
    void StopAtTerrain(){if(spent)return;spent=true;TerrainImpacts++;if(rb!=null)rb.linearVelocity=Vector2.zero;Destroy(gameObject);}
    private Rigidbody2D rb;
    private Vector2 direction = Vector2.right;
    private Transform ownerRoot;
    private GameObject sourceActor;
    private bool hadOwner;
    private IDiggableTerrain launchTerrain;
    readonly System.Collections.Generic.HashSet<int> recipientContacts=new System.Collections.Generic.HashSet<int>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (hitLayers.value == 0)
        {
            int layers = 0;
            int playerLayer = LayerMask.NameToLayer("Player");
            int buddyLayer = LayerMask.NameToLayer("Buddy");

            if (playerLayer >= 0)
                layers |= 1 << playerLayer;

            if (buddyLayer >= 0)
                layers |= 1 << buddyLayer;

            hitLayers.value = layers;
        }
    }

    void Start()
    {
        if(RegionQueries.Projected!=null)gameObject.AddComponent<ProjectedProjectileView>();
        Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    void FixedUpdate()
    {
        using var profile=new Unity.Profiling.ProfilerMarker("Spore.Projectile.TerrainSweep").Auto();
        if(spent)return;
        Vector2 position=rb!=null?rb.position:(Vector2)transform.position;
        var terrain=launchTerrain??DiggableTerrainService.Active;
        if(!CanContinue()){StopAtTerrain();return;}
        float distance=speed*Time.fixedDeltaTime;
        if(terrain!=null){
            if(!RegionQueries.CombatReady){StopAtTerrain();return;}
            float radius=Mathf.Max(.01f,Mathf.Max(GetComponent<Collider2D>().bounds.extents.x,GetComponent<Collider2D>().bounds.extents.y));
            float wall=1.01f;
            if(!RegionQueries.ProjectileClear(terrain,position,position+direction*distance,radius)){
                float lo=0,hi=1;for(int i=0;i<10;i++){float mid=(lo+hi)*.5f;if(RegionQueries.ProjectileClear(terrain,position,position+direction*distance*mid,radius))lo=mid;else hi=mid;}wall=hi;
            }
            var filter=new ContactFilter2D();filter.SetLayerMask(hitLayers);filter.useTriggers=true;
            int count=Physics2D.CircleCast(position,radius,direction,filter,sweepHits,distance);Collider2D first=SelectSweptRecipient(count,out float nearest);
            if(first!=null&&nearest<=wall*distance){HandleHit(first);if(spent)return;}
            if(wall<=1){StopAtTerrain();return;}
        }
        Vector2 velocity = direction * speed;

        if (rb != null)
            rb.linearVelocity = velocity;
        else
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
    }

    bool CanContinue()
    {
        if(hadOwner&&(sourceActor==null||!EnemyDamage.Alive(EnemyDamage.Resolve(sourceActor))||!EnemyDamage.Ready(sourceActor)||!CombatSessionBinding.AllowsActions(sourceActor)))return false;
        if(launchTerrain!=null&&!object.ReferenceEquals(launchTerrain,DiggableTerrainService.Active))return false;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool regionRequired=GameSceneCatalog.IsExpedition(scene)||GameSceneCatalog.IsCamp(scene);
        return DiggableTerrainService.Active!=null?RegionQueries.CombatReady:!regionRequired;
    }

    Collider2D SelectSweptRecipient(int count,out float nearest)
    {
        Collider2D first=null;nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var hit=sweepHits[i];var collider=hit.collider;
            if(collider==null||IsOwnerCollider(collider)||contacts.Contains(collider.GetInstanceID()))continue;
            var receiver=EnemyDamage.Resolve(collider.gameObject);
            if(receiver==null||recipientContacts.Contains(receiver.GetInstanceID())||!EnemyDamage.Eligible(sourceActor,receiver))continue;
            if(hit.distance<nearest){nearest=hit.distance;first=collider;}
        }
        return first;
    }

    public void Launch(Vector2 launchDirection)
    {
        if (launchDirection.sqrMagnitude > 0.001f)
            direction = launchDirection.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void Launch(Vector2 launchDirection, Transform owner)
    {
        LaunchCount++;
        hadOwner=owner!=null;
        sourceActor=EnemyDamage.Resolve(owner!=null?owner.gameObject:null)?.gameObject;
        SessionRules=sourceActor!=null?sourceActor.GetComponent<CombatSessionBinding>()?.Rules:null;
        ownerRoot=sourceActor!=null?sourceActor.transform:null;
        launchTerrain=sourceActor!=null?ActorTerrainBinding.Resolve(sourceActor):DiggableTerrainService.Active;
        IgnoreOwnerCollisions();
        Launch(launchDirection);
    }

    void IgnoreOwnerCollisions()
    {
        if (ownerRoot == null)
            return;

        Collider2D[] projectileColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D projectileCollider in projectileColliders)
        {
            if (projectileCollider == null)
                continue;

            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    void HandleHit(Collider2D other)
    {
        if (other == null)
            return;

        if(spent)return;
        if(!CanContinue()){StopAtTerrain();return;}
        if(RegionQueries.TerrainCollider(other)){StopAtTerrain();return;}
        if (IsOwnerCollider(other))
            return;

        if (!IsInHitLayers(other.gameObject.layer))
            return;

        if(!contacts.Add(other.GetInstanceID()))return;
        var receiver=EnemyDamage.Resolve(other.gameObject);
        if(receiver==null||recipientContacts.Contains(receiver.GetInstanceID()))return;
        GameObject source=sourceActor;
        var result=EnemyDamage.TryApply(receiver.gameObject,new EnemyDamageRequest(damage,"",source));
        if(!result.Accepted)return;
        recipientContacts.Add(receiver.GetInstanceID());

        if (destroyOnHit){spent=true;Destroy(gameObject);}
    }

    void OnTriggerExit2D(Collider2D c)=>contacts.Remove(c.GetInstanceID());
    void OnCollisionExit2D(Collision2D c)=>contacts.Remove(c.collider.GetInstanceID());

    bool IsOwnerCollider(Collider2D other)
    {
        return ownerRoot != null &&
               (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot));
    }

    bool IsInHitLayers(int layer)
    {
        return (hitLayers.value & (1 << layer)) != 0;
    }

    void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        damage = Mathf.Max(0, damage);
        lifetime = Mathf.Max(0.01f, lifetime);
    }
}
