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
    Idle = 0,
    Walk = 1,
    Attack = 2,
    Dig = 3,
    Dash = 4,
    Hurt = 5,
    Death = 6,
    Sleep = 7,
    Dance = 8,
    Hide = 9,
    Roar = 10,
    AttackReady = 11,
    AttackSwing = 12,
    Grab = 13
}
