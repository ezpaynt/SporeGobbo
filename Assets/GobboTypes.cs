public enum BuddyType
{
    Baby,
    Fast,
    Fat,
    Scavenger,
    Tank,
    Thrower,
    Strong,
    Fungal,
    Explosive
}

public enum GobboAgeStage
{
    Baby,
    Young,
    Stage1,
    Stage2,
    Stage3,
    Stage4,
    NeglectedElder
}

public enum BuddyGrowthChoiceType
{
    None = 0,
    Evolution = 1,
    StatCard = 2,
    Trait = 3,
    Mutation = 4
}

public enum GobboCardContext
{
    RunLevelUp,
    EvolutionChoice,
    CampShop,
    FightPitReward,
    ShadyDeal,
    BuddyStatGrowth
}

public enum GobboAnimationState
{
    Idle,
    Walk,
    Attack,
    Dig,
    Dash,
    Hurt,
    Death,
    Sleep,
    Dance,
    Hide,
    Roar
}
