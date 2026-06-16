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
    public int selectedFrameIndex;
    public int selectedFrameCount;
    public bool usedActionFallback;
    public bool usedDirectionFallback;
    public bool actionSetWasNull;
    public bool actionSetWasEmpty;
}

[System.Serializable]
public class DirectionalSpriteSet
{
    [Header("Frame 1 / Still Sprite")]
    public Sprite front;
    public Sprite frontLeft;
    public Sprite left;
    public Sprite frontRight;
    public Sprite right;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    [Header("Extra Loop Frames")]
    public Sprite[] frontFrames;
    public Sprite[] frontLeftFrames;
    public Sprite[] leftFrames;
    public Sprite[] frontRightFrames;
    public Sprite[] rightFrames;
    public Sprite[] backFrames;
    public Sprite[] backLeftFrames;
    public Sprite[] backRightFrames;

    public bool HasAnySprite()
    {
        return HasAnyFrame(front, frontFrames) || HasAnyFrame(frontLeft, frontLeftFrames) ||
               HasAnyFrame(left, leftFrames) || HasAnyFrame(frontRight, frontRightFrames) ||
               HasAnyFrame(right, rightFrames) || HasAnyFrame(back, backFrames) ||
               HasAnyFrame(backLeft, backLeftFrames) || HasAnyFrame(backRight, backRightFrames);
    }

    public Sprite PickForDirection(Vector2 direction)
    {
        return PickForDirectionDetailed(direction, 0).selectedSprite;
    }

    public GobboVisualPickResult PickForDirectionDetailed(Vector2 direction, int frameIndex = 0)
    {
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;

        direction.Normalize();

        int sector = GetEightWaySector(direction);
        GobboVisualDirectionSlot requestedSlot = SectorToSlot(sector);
        Sprite exact = PickExactSector(sector, frameIndex, out int exactFrameCount);

        if (exact != null)
        {
            return new GobboVisualPickResult
            {
                requestedDirectionSlot = requestedSlot,
                selectedDirectionSlot = requestedSlot,
                selectedSprite = exact,
                selectedFrameIndex = NormalizeFrameIndex(frameIndex, exactFrameCount),
                selectedFrameCount = exactFrameCount,
                usedDirectionFallback = false
            };
        }

        GobboVisualDirectionSlot fallbackSlot;
        int fallbackFrameCount;
        Sprite fallback = PickFallbackForSector(sector, frameIndex, out fallbackSlot, out fallbackFrameCount);

        return new GobboVisualPickResult
        {
            requestedDirectionSlot = requestedSlot,
            selectedDirectionSlot = fallbackSlot,
            selectedSprite = fallback,
            selectedFrameIndex = NormalizeFrameIndex(frameIndex, fallbackFrameCount),
            selectedFrameCount = fallbackFrameCount,
            usedDirectionFallback = fallback != null
        };
    }

    public Sprite PickFirstAvailable(out GobboVisualDirectionSlot selectedSlot)
    {
        int unusedFrameCount;
        return FirstAvailable(out selectedSlot, out unusedFrameCount, 0,
            SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
            SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
            SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
            SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
            SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
            SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
            SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
            SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames));
    }

    public int GetFrameCountForDirection(Vector2 direction)
    {
        GobboVisualPickResult result = PickForDirectionDetailed(direction, 0);
        return Mathf.Max(1, result.selectedFrameCount);
    }

    public string GetAvailableSlotSummary()
    {
        if (!HasAnySprite())
            return "none";

        StringBuilder builder = new StringBuilder();
        AppendSlot(builder, "Front", front, frontFrames);
        AppendSlot(builder, "FrontLeft", frontLeft, frontLeftFrames);
        AppendSlot(builder, "Left", left, leftFrames);
        AppendSlot(builder, "FrontRight", frontRight, frontRightFrames);
        AppendSlot(builder, "Right", right, rightFrames);
        AppendSlot(builder, "Back", back, backFrames);
        AppendSlot(builder, "BackLeft", backLeft, backLeftFrames);
        AppendSlot(builder, "BackRight", backRight, backRightFrames);
        return builder.ToString();
    }

    static void AppendSlot(StringBuilder builder, string label, Sprite primary, Sprite[] extraFrames)
    {
        int frameCount = CountFrames(primary, extraFrames);
        if (frameCount <= 0)
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(label).Append("=").Append(FirstFrameName(primary, extraFrames));
        if (frameCount > 1)
            builder.Append(" x").Append(frameCount);
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

    Sprite PickExactSector(int sector, int frameIndex, out int frameCount)
    {
        switch (sector)
        {
            case 0: return PickFrame(front, frontFrames, frameIndex, out frameCount);
            case 1: return PickFrame(frontRight, frontRightFrames, frameIndex, out frameCount);
            case 2: return PickFrame(right, rightFrames, frameIndex, out frameCount);
            case 3: return PickFrame(backRight, backRightFrames, frameIndex, out frameCount);
            case 4: return PickFrame(back, backFrames, frameIndex, out frameCount);
            case 5: return PickFrame(backLeft, backLeftFrames, frameIndex, out frameCount);
            case 6: return PickFrame(left, leftFrames, frameIndex, out frameCount);
            case 7: return PickFrame(frontLeft, frontLeftFrames, frameIndex, out frameCount);
        }

        frameCount = 0;
        return null;
    }

    Sprite PickFallbackForSector(int sector, int frameIndex, out GobboVisualDirectionSlot selectedSlot, out int frameCount)
    {
        switch (sector)
        {
            case 0:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames));
            case 1:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames));
            case 2:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames));
            case 3:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames));
            case 4:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames));
            case 5:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames));
            case 6:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames));
            case 7:
                return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
                    SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
                    SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
                    SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
                    SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
                    SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
                    SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames));
        }

        return PickFirstAvailableWithCount(out selectedSlot, out frameCount, frameIndex);
    }

    Sprite PickFirstAvailableWithCount(out GobboVisualDirectionSlot selectedSlot, out int frameCount, int frameIndex)
    {
        return FirstAvailable(out selectedSlot, out frameCount, frameIndex,
            SlotSprite(GobboVisualDirectionSlot.Front, front, frontFrames),
            SlotSprite(GobboVisualDirectionSlot.FrontLeft, frontLeft, frontLeftFrames),
            SlotSprite(GobboVisualDirectionSlot.Left, left, leftFrames),
            SlotSprite(GobboVisualDirectionSlot.FrontRight, frontRight, frontRightFrames),
            SlotSprite(GobboVisualDirectionSlot.Right, right, rightFrames),
            SlotSprite(GobboVisualDirectionSlot.Back, back, backFrames),
            SlotSprite(GobboVisualDirectionSlot.BackLeft, backLeft, backLeftFrames),
            SlotSprite(GobboVisualDirectionSlot.BackRight, backRight, backRightFrames));
    }

    SlotSpriteEntry SlotSprite(GobboVisualDirectionSlot slot, Sprite primary, Sprite[] extraFrames)
    {
        return new SlotSpriteEntry { slot = slot, primary = primary, extraFrames = extraFrames };
    }

    Sprite FirstAvailable(out GobboVisualDirectionSlot selectedSlot, out int frameCount, int frameIndex, params SlotSpriteEntry[] sprites)
    {
        foreach (SlotSpriteEntry entry in sprites)
        {
            Sprite sprite = PickFrame(entry.primary, entry.extraFrames, frameIndex, out frameCount);
            if (sprite != null)
            {
                selectedSlot = entry.slot;
                return sprite;
            }
        }

        selectedSlot = GobboVisualDirectionSlot.Front;
        frameCount = 0;
        return null;
    }

    static bool HasAnyFrame(Sprite primary, Sprite[] extraFrames)
    {
        return CountFrames(primary, extraFrames) > 0;
    }

    static int CountFrames(Sprite primary, Sprite[] extraFrames)
    {
        int count = primary != null ? 1 : 0;

        if (extraFrames != null)
        {
            for (int i = 0; i < extraFrames.Length; i++)
            {
                if (extraFrames[i] != null)
                    count++;
            }
        }

        return count;
    }

    static Sprite PickFrame(Sprite primary, Sprite[] extraFrames, int frameIndex, out int frameCount)
    {
        frameCount = CountFrames(primary, extraFrames);
        if (frameCount <= 0)
            return null;

        int targetFrame = NormalizeFrameIndex(frameIndex, frameCount);
        int currentFrame = 0;

        if (primary != null)
        {
            if (targetFrame == currentFrame)
                return primary;

            currentFrame++;
        }

        if (extraFrames != null)
        {
            for (int i = 0; i < extraFrames.Length; i++)
            {
                if (extraFrames[i] == null)
                    continue;

                if (targetFrame == currentFrame)
                    return extraFrames[i];

                currentFrame++;
            }
        }

        return primary != null ? primary : FirstExtraFrame(extraFrames);
    }

    static int NormalizeFrameIndex(int frameIndex, int frameCount)
    {
        if (frameCount <= 1)
            return 0;

        int normalized = frameIndex % frameCount;
        return normalized < 0 ? normalized + frameCount : normalized;
    }

    static Sprite FirstExtraFrame(Sprite[] extraFrames)
    {
        if (extraFrames == null)
            return null;

        for (int i = 0; i < extraFrames.Length; i++)
        {
            if (extraFrames[i] != null)
                return extraFrames[i];
        }

        return null;
    }

    static string FirstFrameName(Sprite primary, Sprite[] extraFrames)
    {
        Sprite first = primary != null ? primary : FirstExtraFrame(extraFrames);
        return first != null ? first.name : "NULL";
    }

    struct SlotSpriteEntry
    {
        public GobboVisualDirectionSlot slot;
        public Sprite primary;
        public Sprite[] extraFrames;
    }
}

[System.Serializable]
public class GobboVisualSet
{
    public string visualSetId = "baby";
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;

    [Header("Loop Timing")]
    [Min(0.02f)] public float defaultFrameDuration = 0.2f;
    [Min(0.02f)] public float idleFrameDuration = 0.45f;
    [Min(0.02f)] public float walkFrameDuration = 0.18f;

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

    public GobboVisualPickResult PickSpriteDetailed(GobboAnimationState state, Vector2 direction, int frameIndex = 0)
    {
        GobboAnimationState resolvedState;
        bool usedFallback;
        bool wasNull;
        bool wasEmpty;
        DirectionalSpriteSet sprites = GetSpritesDetailed(state, out resolvedState, out usedFallback, out wasNull, out wasEmpty);

        GobboVisualPickResult result = sprites != null
            ? sprites.PickForDirectionDetailed(direction, frameIndex)
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

    public float GetFrameDuration(GobboAnimationState state)
    {
        switch (state)
        {
            case GobboAnimationState.Idle:
                return Mathf.Max(0.02f, idleFrameDuration);
            case GobboAnimationState.Walk:
                return Mathf.Max(0.02f, walkFrameDuration);
        }

        return Mathf.Max(0.02f, defaultFrameDuration);
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
