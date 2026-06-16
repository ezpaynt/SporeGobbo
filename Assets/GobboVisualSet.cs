using System.Text;
using UnityEngine;

public enum GobboVisualDirectionSlot
{
    Front,
    FrontRight,
    Right,
    BackRight,
    Back,
    BackLeft,
    Left,
    FrontLeft
}

public class GobboVisualPickResult
{
    public GobboAnimationState requestedState;
    public GobboAnimationState resolvedState;
    public GobboVisualDirectionSlot requestedDirectionSlot;
    public GobboVisualDirectionSlot selectedDirectionSlot;
    public Sprite selectedSprite;
    public bool usedActionFallback;
    public bool usedDirectionFallback;
    public bool actionSetWasNull;
    public bool actionSetWasEmpty;
}

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
        return PickForDirectionDetailed(direction).selectedSprite;
    }

    public GobboVisualPickResult PickForDirectionDetailed(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        direction.Normalize();

        int sector = GetEightWaySector(direction);
        GobboVisualDirectionSlot requestedSlot = SectorToSlot(sector);
        Sprite exact = PickExactSector(sector);

        if (exact != null)
        {
            return new GobboVisualPickResult
            {
                requestedDirectionSlot = requestedSlot,
                selectedDirectionSlot = requestedSlot,
                selectedSprite = exact,
                usedDirectionFallback = false
            };
        }

        GobboVisualDirectionSlot fallbackSlot;
        Sprite fallback = PickFallbackForSector(sector, out fallbackSlot);

        return new GobboVisualPickResult
        {
            requestedDirectionSlot = requestedSlot,
            selectedDirectionSlot = fallbackSlot,
            selectedSprite = fallback,
            usedDirectionFallback = fallback != null
        };
    }

    public Sprite PickFirstAvailable(out GobboVisualDirectionSlot selectedSlot)
    {
        return FirstAvailable(out selectedSlot,
            SlotSprite(GobboVisualDirectionSlot.Front, front),
            SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
            SlotSprite(GobboVisualDirectionSlot.Left, left),
            SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
            SlotSprite(GobboVisualDirectionSlot.Right, right),
            SlotSprite(GobboVisualDirectionSlot.Back, back),
            SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
            SlotSprite(GobboVisualDirectionSlot.BackRight, backRight));
    }

    public string GetAvailableSlotSummary()
    {
        if (!HasAnySprite())
            return "none";

        StringBuilder builder = new StringBuilder();
        AppendSlot(builder, "Front", front);
        AppendSlot(builder, "FrontLeft", frontLeft);
        AppendSlot(builder, "Left", left);
        AppendSlot(builder, "FrontRight", frontRight);
        AppendSlot(builder, "Right", right);
        AppendSlot(builder, "Back", back);
        AppendSlot(builder, "BackLeft", backLeft);
        AppendSlot(builder, "BackRight", backRight);
        return builder.ToString();
    }

    static void AppendSlot(StringBuilder builder, string label, Sprite sprite)
    {
        if (sprite == null)
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(label).Append("=").Append(sprite.name);
    }

    int GetEightWaySector(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    GobboVisualDirectionSlot SectorToSlot(int sector)
    {
        switch (sector)
        {
            case 0: return GobboVisualDirectionSlot.Front;
            case 1: return GobboVisualDirectionSlot.FrontRight;
            case 2: return GobboVisualDirectionSlot.Right;
            case 3: return GobboVisualDirectionSlot.BackRight;
            case 4: return GobboVisualDirectionSlot.Back;
            case 5: return GobboVisualDirectionSlot.BackLeft;
            case 6: return GobboVisualDirectionSlot.Left;
            case 7: return GobboVisualDirectionSlot.FrontLeft;
        }

        return GobboVisualDirectionSlot.Front;
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
        GobboVisualDirectionSlot unusedSlot;
        return PickFallbackForSector(sector, out unusedSlot);
    }

    Sprite PickFallbackForSector(int sector, out GobboVisualDirectionSlot selectedSlot)
    {
        switch (sector)
        {
            case 0:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight));
            case 1:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft));
            case 2:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.Left, left));
            case 3:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft));
            case 4:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight));
            case 5:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight));
            case 6:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight),
                    SlotSprite(GobboVisualDirectionSlot.Right, right));
            case 7:
                return FirstAvailable(out selectedSlot,
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft),
                    SlotSprite(GobboVisualDirectionSlot.Left, left),
                    SlotSprite(GobboVisualDirectionSlot.Front, front),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight),
                    SlotSprite(GobboVisualDirectionSlot.Back, back),
                    SlotSprite(GobboVisualDirectionSlot.Right, right),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight));
        }

        return PickFirstAvailable(out selectedSlot);
    }

    SlotSpriteEntry SlotSprite(GobboVisualDirectionSlot slot, Sprite sprite)
    {
        return new SlotSpriteEntry { slot = slot, sprite = sprite };
    }

    Sprite FirstAvailable(out GobboVisualDirectionSlot selectedSlot, params SlotSpriteEntry[] sprites)
    {
        foreach (SlotSpriteEntry entry in sprites)
        {
            if (entry.sprite != null)
            {
                selectedSlot = entry.slot;
                return entry.sprite;
            }
        }

        selectedSlot = GobboVisualDirectionSlot.Front;
        return null;
    }

    struct SlotSpriteEntry
    {
        public GobboVisualDirectionSlot slot;
        public Sprite sprite;
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
        GobboAnimationState resolvedState;
        bool usedFallback;
        bool wasNull;
        bool wasEmpty;
        return GetSpritesDetailed(state, out resolvedState, out usedFallback, out wasNull, out wasEmpty);
    }

    public GobboVisualPickResult PickSpriteDetailed(GobboAnimationState state, Vector2 direction)
    {
        GobboAnimationState resolvedState;
        bool usedFallback;
        bool wasNull;
        bool wasEmpty;
        DirectionalSpriteSet sprites = GetSpritesDetailed(state, out resolvedState, out usedFallback, out wasNull, out wasEmpty);

        GobboVisualPickResult result = sprites != null
            ? sprites.PickForDirectionDetailed(direction)
            : new GobboVisualPickResult { selectedSprite = null };

        result.requestedState = state;
        result.resolvedState = resolvedState;
        result.usedActionFallback = usedFallback;
        result.actionSetWasNull = wasNull;
        result.actionSetWasEmpty = wasEmpty;
        return result;
    }

    public Sprite PickFirstAvailableSprite(out GobboAnimationState resolvedState, out GobboVisualDirectionSlot resolvedDirection)
    {
        DirectionalSpriteSet sprites = FirstNonEmptySet(out resolvedState);
        if (sprites == null)
        {
            resolvedDirection = GobboVisualDirectionSlot.Front;
            return null;
        }

        return sprites.PickFirstAvailable(out resolvedDirection);
    }

    public string GetAvailableSpriteSummary()
    {
        StringBuilder builder = new StringBuilder();
        AppendSpriteSetSummary(builder, "Idle", idle);
        AppendSpriteSetSummary(builder, "Walk", walk);
        AppendSpriteSetSummary(builder, "Attack", attack);
        AppendSpriteSetSummary(builder, "AttackReady", attackReady);
        AppendSpriteSetSummary(builder, "AttackSwing", attackSwing);
        AppendSpriteSetSummary(builder, "Dig", dig);
        AppendSpriteSetSummary(builder, "Dash", dash);
        AppendSpriteSetSummary(builder, "Hurt", hurt);
        AppendSpriteSetSummary(builder, "Grab", grab);
        AppendSpriteSetSummary(builder, "Death", death);
        AppendSpriteSetSummary(builder, "Sleep", sleep);
        AppendSpriteSetSummary(builder, "Dance", dance);
        AppendSpriteSetSummary(builder, "Hide", hide);
        AppendSpriteSetSummary(builder, "Roar", roar);
        return builder.Length > 0 ? builder.ToString() : "none";
    }

    DirectionalSpriteSet GetSpritesDetailed(GobboAnimationState state, out GobboAnimationState resolvedState, out bool usedFallback, out bool wasNull, out bool wasEmpty)
    {
        DirectionalSpriteSet chosen = idle;
        resolvedState = GobboAnimationState.Idle;

        switch (state)
        {
            case GobboAnimationState.Walk: chosen = walk; resolvedState = GobboAnimationState.Walk; break;
            case GobboAnimationState.Attack: chosen = attack; resolvedState = GobboAnimationState.Attack; break;
            case GobboAnimationState.AttackReady: chosen = attackReady; resolvedState = GobboAnimationState.AttackReady; break;
            case GobboAnimationState.AttackSwing: chosen = attackSwing; resolvedState = GobboAnimationState.AttackSwing; break;
            case GobboAnimationState.Dig: chosen = dig; resolvedState = GobboAnimationState.Dig; break;
            case GobboAnimationState.Dash: chosen = dash; resolvedState = GobboAnimationState.Dash; break;
            case GobboAnimationState.Hurt: chosen = hurt; resolvedState = GobboAnimationState.Hurt; break;
            case GobboAnimationState.Grab: chosen = grab; resolvedState = GobboAnimationState.Grab; break;
            case GobboAnimationState.Death: chosen = death; resolvedState = GobboAnimationState.Death; break;
            case GobboAnimationState.Sleep: chosen = sleep; resolvedState = GobboAnimationState.Sleep; break;
            case GobboAnimationState.Dance: chosen = dance; resolvedState = GobboAnimationState.Dance; break;
            case GobboAnimationState.Hide: chosen = hide; resolvedState = GobboAnimationState.Hide; break;
            case GobboAnimationState.Roar: chosen = roar; resolvedState = GobboAnimationState.Roar; break;
        }

        wasNull = chosen == null;
        wasEmpty = chosen != null && !chosen.HasAnySprite();

        if (chosen != null && chosen.HasAnySprite())
        {
            usedFallback = false;
            return chosen;
        }

        usedFallback = true;
        return GetFallbackSprites(state, out resolvedState);
    }

    DirectionalSpriteSet GetFallbackSprites(GobboAnimationState state)
    {
        GobboAnimationState unusedState;
        return GetFallbackSprites(state, out unusedState);
    }

    DirectionalSpriteSet GetFallbackSprites(GobboAnimationState state, out GobboAnimationState resolvedState)
    {
        resolvedState = GobboAnimationState.Idle;

        switch (state)
        {
            case GobboAnimationState.AttackReady:
            case GobboAnimationState.AttackSwing:
                if (attack != null && attack.HasAnySprite()) { resolvedState = GobboAnimationState.Attack; return attack; }
                break;
            case GobboAnimationState.Grab:
            case GobboAnimationState.Walk:
            case GobboAnimationState.Dig:
            case GobboAnimationState.Dash:
                if (walk != null && walk.HasAnySprite()) { resolvedState = GobboAnimationState.Walk; return walk; }
                break;
            case GobboAnimationState.Attack:
            case GobboAnimationState.Hurt:
            case GobboAnimationState.Death:
            case GobboAnimationState.Sleep:
            case GobboAnimationState.Dance:
            case GobboAnimationState.Hide:
            case GobboAnimationState.Roar:
                if (idle != null && idle.HasAnySprite()) { resolvedState = GobboAnimationState.Idle; return idle; }
                break;
        }

        return FirstNonEmptySet(out resolvedState) ?? idle;
    }

    DirectionalSpriteSet FirstNonEmptySet(out GobboAnimationState resolvedState)
    {
        if (idle != null && idle.HasAnySprite()) { resolvedState = GobboAnimationState.Idle; return idle; }
        if (walk != null && walk.HasAnySprite()) { resolvedState = GobboAnimationState.Walk; return walk; }
        if (attack != null && attack.HasAnySprite()) { resolvedState = GobboAnimationState.Attack; return attack; }
        if (attackReady != null && attackReady.HasAnySprite()) { resolvedState = GobboAnimationState.AttackReady; return attackReady; }
        if (attackSwing != null && attackSwing.HasAnySprite()) { resolvedState = GobboAnimationState.AttackSwing; return attackSwing; }
        if (dig != null && dig.HasAnySprite()) { resolvedState = GobboAnimationState.Dig; return dig; }
        if (dash != null && dash.HasAnySprite()) { resolvedState = GobboAnimationState.Dash; return dash; }
        if (hurt != null && hurt.HasAnySprite()) { resolvedState = GobboAnimationState.Hurt; return hurt; }
        if (grab != null && grab.HasAnySprite()) { resolvedState = GobboAnimationState.Grab; return grab; }
        if (death != null && death.HasAnySprite()) { resolvedState = GobboAnimationState.Death; return death; }
        if (sleep != null && sleep.HasAnySprite()) { resolvedState = GobboAnimationState.Sleep; return sleep; }
        if (dance != null && dance.HasAnySprite()) { resolvedState = GobboAnimationState.Dance; return dance; }
        if (hide != null && hide.HasAnySprite()) { resolvedState = GobboAnimationState.Hide; return hide; }
        if (roar != null && roar.HasAnySprite()) { resolvedState = GobboAnimationState.Roar; return roar; }

        resolvedState = GobboAnimationState.Idle;
        return null;
    }

    static void AppendSpriteSetSummary(StringBuilder builder, string label, DirectionalSpriteSet sprites)
    {
        if (sprites == null || !sprites.HasAnySprite())
            return;

        if (builder.Length > 0)
            builder.Append(" | ");

        builder.Append(label).Append(": ").Append(sprites.GetAvailableSlotSummary());
    }
}
