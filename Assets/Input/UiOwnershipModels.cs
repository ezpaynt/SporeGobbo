using System.Collections.Generic;

namespace SporeGobbo.Input
{
    public sealed class PauseOwnerModel
    {
        private readonly HashSet<int> owners = new();
        public bool IsPaused => owners.Count > 0;
        public int Count => owners.Count;
        public bool Acquire(int owner) => owners.Add(owner);
        public bool Release(int owner) => owners.Remove(owner);
        public void Clear() => owners.Clear();
    }

    public enum SemanticUiRoute
    {
        None,
        OpenPause,
        ClosePause,
        PauseBack,
        CancelTopModal
    }

    public static class SemanticUiRouteDecider
    {
        public static SemanticUiRoute Decide(
            SporeInputContext context,
            bool hasModal,
            bool pausePressed,
            bool cancelPressed,
            bool pauseHasSubpage)
        {
            if (hasModal || context == SporeInputContext.Modal)
                return cancelPressed ? SemanticUiRoute.CancelTopModal : SemanticUiRoute.None;
            if (context == SporeInputContext.Pause)
            {
                if (pausePressed) return SemanticUiRoute.ClosePause;
                if (cancelPressed) return pauseHasSubpage ? SemanticUiRoute.PauseBack : SemanticUiRoute.ClosePause;
                return SemanticUiRoute.None;
            }
            if (context == SporeInputContext.Wheel)
                return pausePressed ? SemanticUiRoute.OpenPause : SemanticUiRoute.None;
            return context == SporeInputContext.Gameplay && pausePressed
                ? SemanticUiRoute.OpenPause
                : SemanticUiRoute.None;
        }
    }

    public static class ModalLifecyclePolicy
    {
        public static bool ShouldForceClear(bool isOpen, bool ownerExists, bool ownerActive) =>
            isOpen && (!ownerExists || !ownerActive);
    }

    public static class SceneInputContextPolicy
    {
        public static SporeInputContext NormalizeAfterSingleSceneLoad(bool isMainMenu) =>
            isMainMenu ? SporeInputContext.Modal : SporeInputContext.Gameplay;
    }
}
