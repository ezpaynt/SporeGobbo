using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile")]
    public float speed = 5f;
    public int damage = 12;
    public float lifetime = 4f;
    public LayerMask hitLayers;
    public bool destroyOnHit = true;

    private Rigidbody2D rb;
    private Vector2 direction = Vector2.right;
    private Transform ownerRoot;

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
        Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    void FixedUpdate()
    {
        Vector2 velocity = direction * speed;

        if (rb != null)
            rb.linearVelocity = velocity;
        else
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
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
        ownerRoot = owner != null ? owner.root : null;
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

        if (IsOwnerCollider(other))
            return;

        if (!IsInHitLayers(other.gameObject.layer))
            return;

        other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        if (destroyOnHit)
            Destroy(gameObject);
    }

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
