using UnityEngine;

[System.Serializable]
public class DirectionalSpriteSet
{
    public Sprite front;
    public Sprite frontLeft;
    public Sprite frontRight;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    public bool HasAnySprite()
    {
        return front != null || frontLeft != null || frontRight != null || back != null || backLeft != null || backRight != null;
    }

    public Sprite PickForDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        direction.Normalize();

        if (direction.y > 0.35f)
        {
            if (direction.x < -0.35f && backLeft != null) return backLeft;
            if (direction.x > 0.35f && backRight != null) return backRight;
            if (back != null) return back;
        }

        if (direction.x < -0.35f && frontLeft != null) return frontLeft;
        if (direction.x > 0.35f && frontRight != null) return frontRight;
        if (front != null) return front;

        return front ?? frontLeft ?? frontRight ?? back ?? backLeft ?? backRight;
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
    public DirectionalSpriteSet dig = new DirectionalSpriteSet();
    public DirectionalSpriteSet dash = new DirectionalSpriteSet();
    public DirectionalSpriteSet hurt = new DirectionalSpriteSet();
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
            case GobboAnimationState.Dig: chosen = dig; break;
            case GobboAnimationState.Dash: chosen = dash; break;
            case GobboAnimationState.Hurt: chosen = hurt; break;
            case GobboAnimationState.Death: chosen = death; break;
            case GobboAnimationState.Sleep: chosen = sleep; break;
            case GobboAnimationState.Dance: chosen = dance; break;
            case GobboAnimationState.Hide: chosen = hide; break;
            case GobboAnimationState.Roar: chosen = roar; break;
        }

        if (chosen != null && chosen.HasAnySprite())
            return chosen;

        return idle;
    }
}
