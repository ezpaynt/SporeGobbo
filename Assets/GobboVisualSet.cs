using UnityEngine;

[System.Serializable]
public class DirectionalSpriteSet
{
    public Sprite front;
    public Sprite frontLeft;
    public Sprite left;
    public Sprite frontRight;
    public Sprite right;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    public bool HasAnySprite()
    {
        return front != null || frontLeft != null || left != null || frontRight != null || right != null ||
               back != null || backLeft != null || backRight != null;
    }

    public Sprite PickForDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        direction.Normalize();

        int sector = GetEightWaySector(direction);
        Sprite exact = PickExactSector(sector);
        if (exact != null) return exact;

        return PickFallbackForSector(sector);
    }

    int GetEightWaySector(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    Sprite PickExactSector(int sector)
    {
        switch (sector)
        {
            case 0: return front;
            case 1: return frontRight;
            case 2: return right;
            case 3: return backRight;
            case 4: return back;
            case 5: return backLeft;
            case 6: return left;
            case 7: return frontLeft;
        }

        return null;
    }

    Sprite PickFallbackForSector(int sector)
    {
        switch (sector)
        {
            case 0:
                return FirstAvailable(front, frontLeft, frontRight, left, right, back, backLeft, backRight);
            case 1:
                return FirstAvailable(frontRight, right, front, backRight, frontLeft, back, left, backLeft);
            case 2:
                return FirstAvailable(right, frontRight, backRight, front, back, frontLeft, backLeft, left);
            case 3:
                return FirstAvailable(backRight, right, back, frontRight, backLeft, front, left, frontLeft);
            case 4:
                return FirstAvailable(back, backLeft, backRight, left, right, front, frontLeft, frontRight);
            case 5:
                return FirstAvailable(backLeft, left, back, frontLeft, backRight, front, right, frontRight);
            case 6:
                return FirstAvailable(left, frontLeft, backLeft, front, back, frontRight, backRight, right);
            case 7:
                return FirstAvailable(frontLeft, left, front, backLeft, frontRight, back, right, backRight);
        }

        return FirstAvailable(front, frontLeft, frontRight, left, right, back, backLeft, backRight);
    }

    Sprite FirstAvailable(params Sprite[] sprites)
    {
        foreach (Sprite sprite in sprites)
            if (sprite != null) return sprite;

        return null;
    }
}

[System.Serializable]
public class GobboVisualSet
{
    public string visualSetId = "baby";
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;

    [Header("Core Animation Sprite Slots")]
    public DirectionalSpriteSet idle = new DirectionalSpriteSet();
    public DirectionalSpriteSet walk = new DirectionalSpriteSet();
    public DirectionalSpriteSet attack = new DirectionalSpriteSet();
    public DirectionalSpriteSet attackReady = new DirectionalSpriteSet();
    public DirectionalSpriteSet attackSwing = new DirectionalSpriteSet();
    public DirectionalSpriteSet dig = new DirectionalSpriteSet();
    public DirectionalSpriteSet dash = new DirectionalSpriteSet();
    public DirectionalSpriteSet hurt = new DirectionalSpriteSet();
    public DirectionalSpriteSet grab = new DirectionalSpriteSet();
    public DirectionalSpriteSet death = new DirectionalSpriteSet();

    [Header("Future / Flavor Sprite Slots")]
    public DirectionalSpriteSet sleep = new DirectionalSpriteSet();
    public DirectionalSpriteSet dance = new DirectionalSpriteSet();
    public DirectionalSpriteSet hide = new DirectionalSpriteSet();
    public DirectionalSpriteSet roar = new DirectionalSpriteSet();

    public DirectionalSpriteSet GetSprites(GobboAnimationState state)
    {
        DirectionalSpriteSet chosen = idle;

        switch (state)
        {
            case GobboAnimationState.Walk: chosen = walk; break;
            case GobboAnimationState.Attack: chosen = attack; break;
            case GobboAnimationState.AttackReady: chosen = attackReady; break;
            case GobboAnimationState.AttackSwing: chosen = attackSwing; break;
            case GobboAnimationState.Dig: chosen = dig; break;
            case GobboAnimationState.Dash: chosen = dash; break;
            case GobboAnimationState.Hurt: chosen = hurt; break;
            case GobboAnimationState.Grab: chosen = grab; break;
            case GobboAnimationState.Death: chosen = death; break;
            case GobboAnimationState.Sleep: chosen = sleep; break;
            case GobboAnimationState.Dance: chosen = dance; break;
            case GobboAnimationState.Hide: chosen = hide; break;
            case GobboAnimationState.Roar: chosen = roar; break;
        }

        if (chosen != null && chosen.HasAnySprite())
            return chosen;

        return GetFallbackSprites(state);
    }

    DirectionalSpriteSet GetFallbackSprites(GobboAnimationState state)
    {
        switch (state)
        {
            case GobboAnimationState.AttackReady:
            case GobboAnimationState.AttackSwing:
                if (attack != null && attack.HasAnySprite()) return attack;
                break;
            case GobboAnimationState.Grab:
                if (walk != null && walk.HasAnySprite()) return walk;
                break;
            case GobboAnimationState.Walk:
            case GobboAnimationState.Dig:
            case GobboAnimationState.Dash:
                if (walk != null && walk.HasAnySprite()) return walk;
                break;
            case GobboAnimationState.Hurt:
            case GobboAnimationState.Death:
                if (idle != null && idle.HasAnySprite()) return idle;
                break;
        }

        if (idle != null && idle.HasAnySprite()) return idle;
        if (walk != null && walk.HasAnySprite()) return walk;
        if (attack != null && attack.HasAnySprite()) return attack;
        return idle;
    }
}
