using UnityEngine;

public interface ITerrainMovementReserve { float TerrainMovementReserve {get;} }

public static class TileMover
{

    /// <summary>Actor-position coordinates stay unchanged. Physical offset follows the real collider;
    /// ReserveHalf preserves the existing origin-centred voluntary exclusion setting.</summary>
    public readonly struct ActorFootprint
    {
        public readonly Vector2 Offset,PhysicalHalf,ReserveHalf;
        public ActorFootprint(Vector2 offset,Vector2 physical,Vector2 reserve){Offset=offset;PhysicalHalf=physical;ReserveHalf=Vector2.Max(physical,reserve);}
        public Vector2 BroadHalf=>Vector2.Max(ReserveHalf,new Vector2(Mathf.Abs(Offset.x),Mathf.Abs(Offset.y))+PhysicalHalf);
        public bool Same(ActorFootprint b)=>(Offset-b.Offset).sqrMagnitude<.000001f&&(PhysicalHalf-b.PhysicalHalf).sqrMagnitude<.000001f&&(ReserveHalf-b.ReserveHalf).sqrMagnitude<.000001f;
        bool RedundantPhysical=>Offset.sqrMagnitude<.0000000001f;
        public bool Occupy(IDiggableTerrain terrain,Vector2 origin)=>
            PhysicalHalf.x>0&&PhysicalHalf.y>0&&(RedundantPhysical||CanOccupyBox(terrain,origin+Offset,PhysicalHalf))&&CanOccupyBox(terrain,origin,ReserveHalf);
        public bool Traverse(IDiggableTerrain terrain,Vector2 from,Vector2 to)
        {
            if(PhysicalHalf.x<=0||PhysicalHalf.y<=0)return false;
            // Identical centred footprints retain the original single-query hot path.
            if(RedundantPhysical&&(PhysicalHalf-ReserveHalf).sqrMagnitude<.0000000001f)return CanTraverseBox(terrain,from,to,PhysicalHalf);
            bool reserveClear=CanOccupyBox(terrain,from,ReserveHalf);
            if((!RedundantPhysical||!reserveClear)&&!CanTraverseBox(terrain,from+Offset,to+Offset,PhysicalHalf))return false;
            return CanTraverseBox(terrain,from,to,ReserveHalf,!reserveClear);
        }
    }
    public static ActorFootprint TerrainFootprint(Rigidbody2D body,Vector2 requestedReserve)
    {
        var binding=body!=null?body.GetComponent<ActorTerrainBinding>():null;
        var collider=binding!=null&&binding.PhysicalTerrainCollider!=null?binding.PhysicalTerrainCollider:body!=null?body.GetComponent<Collider2D>():null;
        float radius=binding!=null?binding.TerrainReserveRadius:0;
        var reserve=Vector2.Max(requestedReserve,Vector2.one*radius);
        if(collider!=null&&(!collider.enabled||collider.isTrigger))return default;
        if(collider==null)
            return new ActorFootprint(Vector2.zero,reserve,reserve); // explicit box-only legacy contract fixtures
        if(!(collider is BoxCollider2D)&&!(collider is CircleCollider2D))throw new System.InvalidOperationException("Unsupported creature terrain footprint: "+collider.GetType().Name);
        var half=(Vector2)collider.bounds.extents;
        return new ActorFootprint((Vector2)collider.bounds.center-body.position,half,reserve);
    }
    public static void RequirePhysicalTerrainBody(Rigidbody2D body)
    {
        if(body==null)throw new System.InvalidOperationException("Physical terrain actor lacks body");int count=0;
        foreach(var c in body.GetComponentsInChildren<Collider2D>(true))if(c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.attachedRigidbody==body)
        {count++;if(c.gameObject!=body.gameObject||!(c is BoxCollider2D)&&!(c is CircleCollider2D))throw new System.InvalidOperationException("Unsupported creature physical collider ownership/shape: "+body.name);}
        if(count!=1)throw new System.InvalidOperationException("Creature terrain contract requires one supported physical collider: "+body.name);
    }
    public static bool PhysicalBoxesOverlap(Rigidbody2D a,Vector2 ap,Rigidbody2D b,Vector2 bp)
    {
        var aa=TerrainFootprint(a,Vector2.zero);var bb=TerrainFootprint(b,Vector2.zero);var delta=(ap+aa.Offset)-(bp+bb.Offset);var sum=aa.PhysicalHalf+bb.PhysicalHalf;
        return Mathf.Abs(delta.x)<=sum.x&&Mathf.Abs(delta.y)<=sum.y;
    }
    public static bool CanOccupyActor(IDiggableTerrain terrain,Rigidbody2D body,Vector2 origin,float reserve=0)=>TerrainFootprint(body,Vector2.one*Mathf.Max(0,reserve)).Occupy(terrain,origin);
    public static bool CanTraverseActor(IDiggableTerrain terrain,Rigidbody2D body,Vector2 from,Vector2 to,float reserve=0)=>TerrainFootprint(body,Vector2.one*Mathf.Max(0,reserve)).Traverse(terrain,from,to);

    public static void Move(Rigidbody2D rb, Vector2 desiredVelocity, float bodyRadius)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using var diagonalMotorObservation=new Stage64DDiagonalEvidence.MotorScope(rb,desiredVelocity,bodyRadius);
#endif
        using (new ActorTerrainBinding.Scope("SG59.TileMover.Move", rb.gameObject.GetComponent<ActorTerrainBinding>() != null))
        {
        var actorBinding=rb.GetComponent<ActorTerrainBinding>();
        if(actorBinding!=null&&(actorBinding.Terrain==null||(actorBinding.Ready!=null&&!actorBinding.Ready()))){rb.linearVelocity=Vector2.zero;return;}
        IDiggableTerrain terrain = ActorTerrainBinding.Resolve(rb.gameObject);
        if (terrain == null)
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        if(actorBinding?.Movement!=null&&!actorBinding.Movement.Applying)desiredVelocity=actorBinding.Movement.ConstrainExternal(actorBinding,desiredVelocity,bodyRadius);
        var footprint = TerrainFootprint(rb,Vector2.one*bodyRadius);
        Vector2 clearanceExtents = GetMapClearanceExtents(rb, bodyRadius);
        Vector2 nextPos = rb.position + desiredVelocity * Time.fixedDeltaTime;

        if ((rb.GetComponent<ActorTerrainBinding>() != null ? footprint.Traverse(terrain, rb.position, nextPos) : IsTerrainPositionClearForBox(terrain, nextPos, clearanceExtents)))
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        Vector2 xVel = new Vector2(desiredVelocity.x, 0f);
        Vector2 xPos = rb.position + xVel * Time.fixedDeltaTime;

        if ((rb.GetComponent<ActorTerrainBinding>() != null ? footprint.Traverse(terrain, rb.position, xPos) : IsTerrainPositionClearForBox(terrain, xPos, clearanceExtents)))
        {
            rb.linearVelocity = xVel;
            return;
        }

        Vector2 yVel = new Vector2(0f, desiredVelocity.y);
        Vector2 yPos = rb.position + yVel * Time.fixedDeltaTime;

        if ((rb.GetComponent<ActorTerrainBinding>() != null ? footprint.Traverse(terrain, rb.position, yPos) : IsTerrainPositionClearForBox(terrain, yPos, clearanceExtents)))
        {
            rb.linearVelocity = yVel;
            return;
        }

        rb.linearVelocity = Vector2.zero;
    
        }
}

    public static void KeepOutOfWalls(Rigidbody2D rb, float bodyRadius)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using var diagonalKeepOutObservation=new Stage64DDiagonalEvidence.MotorScope(rb,Vector2.zero,bodyRadius,true);
#endif
        using (new ActorTerrainBinding.Scope("SG59.TileMover.KeepOutOfWalls", rb.gameObject.GetComponent<ActorTerrainBinding>() != null))
        {
        IDiggableTerrain terrain = ActorTerrainBinding.Resolve(rb.gameObject);
        if (terrain == null)
            return;

        Vector2 clearanceExtents = GetMapClearanceExtents(rb, bodyRadius);

        if (rb.GetComponent<ActorTerrainBinding>()!=null) return; // native physical contact owns forced displacement; never teleport a bound actor to reclaim a voluntary reserve
        if (IsTerrainPositionClearForBox(terrain, rb.position, clearanceExtents))
            return;

        Vector2Int cell =
            terrain.WorldToCell(rb.position);

        for (int r = 1; r <= 6; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    Vector2Int testCell =
                        cell + new Vector2Int(x, y);

                    Vector2 testWorld =
                        terrain.CellToWorld(testCell);

                    if (IsTerrainPositionClearForBox(terrain, testWorld, clearanceExtents))
                    {
                        rb.position = testWorld;
                        rb.linearVelocity = Vector2.zero;
                        return;
                    }
                }
            }
        }
    
        }
}

    public static float GetColliderBodyRadius(Rigidbody2D rb, float fallbackRadius)
    {
        float radius = Mathf.Max(0f, fallbackRadius);

        if (rb == null)
            return radius;

        Collider2D collider = rb.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled || collider.isTrigger)
            return radius;

        Vector2 extents = collider.bounds.extents;
        return Mathf.Max(radius, extents.x, extents.y);
    }

    public static Vector2 GetMapClearanceExtents(Rigidbody2D rb, float fallbackRadius)
    {
        Vector2 fallback = Vector2.one * Mathf.Max(0f, fallbackRadius);
        if (rb == null) return fallback;
        Collider2D collider = rb.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled || collider.isTrigger) return fallback;
        Vector2 extents = collider.bounds.extents;
        return new Vector2(Mathf.Max(fallback.x, extents.x), Mathf.Max(fallback.y, extents.y));
    }

    public static float GetMapClearanceRadius(Rigidbody2D rb, float bodyRadius)
    {
        return GetColliderBodyRadius(rb, bodyRadius);
    }

    public static bool CanOccupy(IDiggableTerrain terrain, Vector2 worldPosition, float bodyRadius)
    {
        return terrain == null || IsTerrainPositionClearForBody(terrain, worldPosition, Mathf.Max(0f, bodyRadius));
    }

    public static bool CanTraverse(IDiggableTerrain terrain, Vector2 start, Vector2 end, float bodyRadius)
    {
        if (terrain == null) return true;
        float distance = Vector2.Distance(start, end);
        float spacing = Mathf.Max(0.01f, Mathf.Min(terrain.CellSize * 0.125f,
            Mathf.Max(0.01f, bodyRadius) * 0.5f));
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        for (int step = 0; step <= steps; step++)
            if (!CanOccupy(terrain, Vector2.Lerp(start, end, step / (float)steps), bodyRadius))
                return false;
        return true;
    }

    public static bool CanOccupyBox(IDiggableTerrain terrain, Vector2 worldPosition, Vector2 halfExtents) =>
        terrain == null || IsTerrainPositionClearForBox(terrain, worldPosition,
            new Vector2(Mathf.Max(0f, halfExtents.x), Mathf.Max(0f, halfExtents.y))) &&
            (!(terrain is IExecutableTerrainClearance native)||native.TerrainPhysics!=null&&native.TerrainPhysics.CanOccupyTerrainBox(worldPosition,halfExtents));

    public static bool CanTraverseBox(IDiggableTerrain terrain,Vector2 start,Vector2 end,Vector2 halfExtents)=>CanTraverseBox(terrain,start,end,halfExtents,false);

    static bool CanTraverseBox(IDiggableTerrain terrain, Vector2 start, Vector2 end, Vector2 halfExtents, bool allowExistingLogicalContact)
    {
        if (terrain == null) return true;
        float distance = Vector2.Distance(start, end);
        float minExtent = Mathf.Max(0.01f, Mathf.Min(halfExtents.x, halfExtents.y));
        float spacing = Mathf.Max(0.01f, Mathf.Min(terrain.CellSize * 0.125f, minExtent * 0.5f));
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        if(allowExistingLogicalContact)
        {
            var low=terrain.WorldToCell(Vector2.Min(start,end)-halfExtents-Vector2.one*terrain.CellSize);
            var high=terrain.WorldToCell(Vector2.Max(start,end)+halfExtents+Vector2.one*terrain.CellSize);
            for(int y=Mathf.Min(low.y,high.y);y<=Mathf.Max(low.y,high.y);y++)for(int x=Mathf.Min(low.x,high.x);x<=Mathf.Max(low.x,high.x);x++)
            {
                var cell=new Vector2Int(x,y);if(!terrain.IsBlocked(cell))continue;var center=terrain.CellToWorld(cell);
                if(BodyOverlapsCell(start,halfExtents,center,terrain.CellSize))
                {var h=halfExtents+Vector2.one*terrain.CellSize*.5f;var rect=Rect.MinMaxRect(center.x-h.x,center.y-h.y,center.x+h.x,center.y+h.y);if(!RegionTerrainPhysics.NonDeepeningContact(rect,start,end-start))return false;}
                else if(BodySweepOverlapsCell(start,end,halfExtents,center,terrain.CellSize))return false;
            }
        }
        else for (int step = 0; step <= steps; step++)
            if (!IsTerrainPositionClearForBox(terrain, Vector2.Lerp(start, end, step / (float)steps), halfExtents)) return false;
        return !(terrain is IExecutableTerrainClearance native)||native.TerrainPhysics!=null&&native.TerrainPhysics.CanTraverseTerrainBox(start,end,halfExtents);
    }

    static bool IsTerrainPositionClearForBox(IDiggableTerrain terrain, Vector2 worldPos, Vector2 halfExtents)
    {
        float cellSize = terrain.CellSize;
        Vector2Int center = terrain.WorldToCell(worldPos);
        int rangeX = Mathf.CeilToInt((halfExtents.x + cellSize * 0.5f) / cellSize) + 1;
        int rangeY = Mathf.CeilToInt((halfExtents.y + cellSize * 0.5f) / cellSize) + 1;
        for (int x = center.x - rangeX; x <= center.x + rangeX; x++)
        for (int y = center.y - rangeY; y <= center.y + rangeY; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!terrain.IsBlocked(cell)) continue;
            Vector2 cellCenter = terrain.CellToWorld(cell);
            if (BodyOverlapsCell(worldPos, halfExtents, cellCenter, cellSize)) return false;
        }
        return true;
    }

    public static bool BodyOverlapsCell(Vector2 position, Vector2 halfExtents, Vector2 center, float cellSize) =>
        Mathf.Abs(position.x-center.x)<=halfExtents.x+cellSize*.5f &&
        Mathf.Abs(position.y-center.y)<=halfExtents.y+cellSize*.5f;

    /// <summary>Inclusive full-body corridor, using the same box/cell boundary as occupancy.
    /// Analytic segment contact covers the entire connector, including between motor samples.</summary>
    public static bool BodySweepOverlapsCell(Vector2 start,Vector2 end,Vector2 halfExtents,Vector2 center,float cellSize)
    {
        var expanded=halfExtents+Vector2.one*(cellSize*.5f);
        var relative=start-center;var delta=end-start;float enter=0,exit=1;
        return SweepAxis(relative.x,delta.x,expanded.x,ref enter,ref exit)&&
               SweepAxis(relative.y,delta.y,expanded.y,ref enter,ref exit);
    }
    static bool SweepAxis(float start,float delta,float extent,ref float enter,ref float exit)
    {
        if(delta==0)return Mathf.Abs(start)<=extent;
        float a=(-extent-start)/delta,b=(extent-start)/delta;
        enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));return enter<=exit;
    }

    private static bool IsTerrainPositionClearForBody(IDiggableTerrain terrain, Vector2 worldPos, float radius)
    {
        if (terrain == null)
            return true;
        float cellSize = terrain.CellSize;
        Vector2Int center = terrain.WorldToCell(worldPos);
        int cellRadius = Mathf.CeilToInt((radius + cellSize * 0.5f) / cellSize) + 1;

        for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
        {
            for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (terrain.IsBlocked(cell) && CircleOverlapsCell(worldPos, radius, cell, terrain))
                    return false;
            }
        }

        return true;
    }

    private static bool CircleOverlapsCell(Vector2 worldPos, float radius, Vector2Int cell, IDiggableTerrain terrain)
    {
        Vector2 cellCenter = terrain.CellToWorld(cell);
        float halfSize = terrain.CellSize * 0.5f;

        float closestX = Mathf.Clamp(worldPos.x, cellCenter.x - halfSize, cellCenter.x + halfSize);
        float closestY = Mathf.Clamp(worldPos.y, cellCenter.y - halfSize, cellCenter.y + halfSize);
        Vector2 closestPoint = new Vector2(closestX, closestY);

        return (closestPoint - worldPos).sqrMagnitude <= radius * radius;
    }
}
