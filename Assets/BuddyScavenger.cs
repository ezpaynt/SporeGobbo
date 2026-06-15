using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BuddyScavenger : MonoBehaviour
{
    [Header("Brain")]
    public bool brainAllowsMovement = false;
    public float scanRange = 4f;
    public float pickupRange = 0.45f;

    [Header("Loyalty")]
    public float maxDistanceFromPlayer = 5f;
    public float abandonFoodDistance = 6f;

    [Header("Layers")]
    public LayerMask foodLayer;

    [Header("Pathing")]
    public float bodyRadius = 0.28f;
    public float repathTime = 0.35f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    private Rigidbody2D rb;
    private BuddyUnit unit;
    private GobboVisualController visualController;
    private BuddyDirectionalSprite directionalSprite;
    private Transform player;
    private Transform targetFood;
    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        unit = GetComponent<BuddyUnit>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        FindPlayerIfMissing();
    }

    void Update()
    {
        if (!CanScavenge())
        {
            DropFoodTarget();
            return;
        }

        FindPlayerIfMissing();
        if (player == null) return;

        float distanceFromPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceFromPlayer > abandonFoodDistance)
        {
            DropFoodTarget();
            return;
        }

        if (targetFood == null && distanceFromPlayer <= maxDistanceFromPlayer)
            FindFood();

        if (targetFood != null && !IsFoodStillValid(targetFood))
            DropFoodTarget();
    }

    void FixedUpdate()
    {
        if (!brainAllowsMovement) return;
        if (!CanScavenge() || targetFood == null || player == null) return;

        float distanceFromPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceFromPlayer > abandonFoodDistance)
        {
            DropFoodTarget();
            return;
        }

        float distanceToFood = Vector2.Distance(transform.position, targetFood.position);
        if (distanceToFood > pickupRange)
        {
            Vector2 moveDir = GetPathDirection(targetFood.position);
            TileMover.Move(rb, moveDir * unit.unitData.moveSpeed, bodyRadius);
            FaceDirection(moveDir, GobboAnimationState.Walk);
        }
        else
        {
            PickUpFood();
        }
    }

    public bool WantsControl()
    {
        return CanScavenge() && targetFood != null && IsFoodStillValid(targetFood);
    }

    bool CanScavenge()
    {
        return unit != null && unit.unitData != null && unit.unitData.collectsFood;
    }

    void FindFood()
    {
        if (foodLayer.value == 0) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRange, foodLayer);
        float closest = Mathf.Infinity;
        Transform closestFood = null;

        foreach (Collider2D hit in hits)
        {
            FoodItem food = hit.GetComponent<FoodItem>();
            if (food == null) continue;
            if (!CanReachFood(hit.transform)) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < closest)
            {
                closest = dist;
                closestFood = hit.transform;
            }
        }

        targetFood = closestFood;
        ClearPath();
    }

    bool IsFoodStillValid(Transform food)
    {
        if (food == null || !food.gameObject.activeInHierarchy) return false;
        if (player != null && Vector2.Distance(food.position, player.position) > abandonFoodDistance + 2f) return false;
        return CanReachFood(food);
    }

    bool CanReachFood(Transform food)
    {
        if (food == null) return false;
        if (MapPathfinder.HasLineOfWalkableSight(rb.position, food.position)) return true;

        List<Vector2> testPath;
        return MapPathfinder.TryFindPath(rb.position, food.position, out testPath) && testPath.Count > 0;
    }

    Vector2 GetPathDirection(Vector2 targetPos)
    {
        if (MapPathfinder.HasLineOfWalkableSight(rb.position, targetPos))
        {
            ClearPath();
            return (targetPos - rb.position).normalized;
        }

        repathTimer -= Time.fixedDeltaTime;
        bool needsNewPath = currentPath.Count == 0 ||
                            pathIndex >= currentPath.Count ||
                            repathTimer <= 0f ||
                            Vector2.Distance(targetPos, lastPathGoal) > repathDistance;

        if (needsNewPath) RebuildPath(targetPos);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count) return Vector2.zero;

        Vector2 waypoint = currentPath[pathIndex];
        if (Vector2.Distance(rb.position, waypoint) <= waypointReachDistance)
        {
            pathIndex++;
            if (pathIndex >= currentPath.Count) return Vector2.zero;
            waypoint = currentPath[pathIndex];
        }

        return (waypoint - rb.position).normalized;
    }

    void RebuildPath(Vector2 targetPos)
    {
        repathTimer = repathTime;
        lastPathGoal = targetPos;

        if (MapPathfinder.TryFindPath(rb.position, targetPos, out List<Vector2> newPath))
        {
            currentPath.Clear();
            currentPath.AddRange(newPath);
            pathIndex = 0;
        }
        else
        {
            ClearPath();
        }
    }

    void PickUpFood()
    {
        if (targetFood == null) return;

        FaceDirection((Vector2)targetFood.position - rb.position, GobboAnimationState.Grab);

        FoodItem food = targetFood.GetComponent<FoodItem>();
        GobboController playerGobbo = Object.FindAnyObjectByType<GobboController>();
        if (food != null && playerGobbo != null)
            food.Eat(playerGobbo);

        DropFoodTarget();
    }

    void FaceDirection(Vector2 direction, GobboAnimationState state)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (visualController != null)
        {
            visualController.SetAnimationState(state);
            visualController.SetDirection(direction);
            return;
        }

        if (directionalSprite != null)
            directionalSprite.SetDirection(direction);
    }

    void DropFoodTarget()
    {
        targetFood = null;
        ClearPath();
    }

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    void FindPlayerIfMissing()
    {
        if (player != null) return;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }
}
