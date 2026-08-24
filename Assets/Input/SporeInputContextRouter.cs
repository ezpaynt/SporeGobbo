using System;

namespace SporeGobbo.Input
{
    /// <summary>
    /// Authoritative representation of the current input recipient. Routing policy remains
    /// explicit so context behavior is not inferred from enum ordering.
    /// </summary>
    public sealed class SporeInputContextRouter
    {
        public SporeInputContextRouter(SporeInputContext initialContext = SporeInputContext.Gameplay)
        {
            Current = initialContext;
        }

        public SporeInputContext Current { get; private set; }

        public event Action<SporeInputContext, SporeInputContext> Changed;

        public bool SetContext(SporeInputContext next)
        {
            if (next == Current)
                return false;

            SporeInputContext previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
            return true;
        }

        public SporeInputAvailability GetAvailability()
        {
            return GetAvailability(Current);
        }

        public static SporeInputAvailability GetAvailability(SporeInputContext context)
        {
            switch (context)
            {
                case SporeInputContext.Gameplay:
                    return new SporeInputAvailability(
                        gameplayMap: true,
                        gameplayWorldActions: true,
                        move: true,
                        aim: true,
                        commandWheel: true,
                        pause: true,
                        wheelMap: false,
                        uiMap: false);

                case SporeInputContext.Wheel:
                    return new SporeInputAvailability(
                        gameplayMap: true,
                        gameplayWorldActions: false,
                        move: true,
                        aim: false,
                        commandWheel: true,
                        pause: true,
                        wheelMap: true,
                        uiMap: false);

                case SporeInputContext.Modal:
                    return new SporeInputAvailability(
                        gameplayMap: false,
                        gameplayWorldActions: false,
                        move: false,
                        aim: false,
                        commandWheel: false,
                        pause: false,
                        wheelMap: false,
                        uiMap: true);

                case SporeInputContext.Pause:
                    return new SporeInputAvailability(
                        gameplayMap: true,
                        gameplayWorldActions: false,
                        move: false,
                        aim: false,
                        commandWheel: false,
                        pause: true,
                        wheelMap: false,
                        uiMap: true);

                default:
                    throw new ArgumentOutOfRangeException(nameof(context), context, null);
            }
        }
    }

    public readonly struct SporeInputAvailability
    {
        public SporeInputAvailability(
            bool gameplayMap,
            bool gameplayWorldActions,
            bool move,
            bool aim,
            bool commandWheel,
            bool pause,
            bool wheelMap,
            bool uiMap)
        {
            GameplayMap = gameplayMap;
            GameplayWorldActions = gameplayWorldActions;
            Move = move;
            Aim = aim;
            CommandWheel = commandWheel;
            Pause = pause;
            WheelMap = wheelMap;
            UiMap = uiMap;
        }

        public bool GameplayMap { get; }
        public bool GameplayWorldActions { get; }
        public bool Move { get; }
        public bool Aim { get; }
        public bool CommandWheel { get; }
        public bool Pause { get; }
        public bool WheelMap { get; }
        public bool UiMap { get; }
    }
}
