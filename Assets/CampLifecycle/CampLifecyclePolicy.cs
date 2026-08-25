namespace SporeGobbo.CampLifecycle
{
    public enum DeathDestination
    {
        SuccessionCamp,
        GameOver
    }

    public static class CampLifecyclePolicy
    {
        public static bool IsValidSurvivor(string uniqueId, bool isDead, int health, bool isLeader)
        {
            return !string.IsNullOrWhiteSpace(uniqueId) && !isDead && health > 0 && !isLeader;
        }

        public static bool IsValidLivingLeader(string uniqueId, bool isDead, int health, bool isLeader)
        {
            return !string.IsNullOrWhiteSpace(uniqueId) && !isDead && health > 0 && isLeader;
        }

        public static DeathDestination DecideDeathDestination(int validSurvivorCount)
        {
            return validSurvivorCount > 0 ? DeathDestination.SuccessionCamp : DeathDestination.GameOver;
        }

        public static bool AppliesFirstArrivalTerrain(bool isNewGameIntro)
        {
            return isNewGameIntro;
        }

        public static int CenteredFootprintOrigin(int centerCell, int footprintSize)
        {
            return centerCell - System.Math.Max(1, footprintSize) / 2;
        }

        public static bool ShouldStartHomeMilestone(int validOwnedBuddyCount, bool completed)
        {
            return !completed && validOwnedBuddyCount > 0;
        }

        public static bool CanProgressionExcavateResidentialStage(int requestedStage, int completedStage)
        {
            return requestedStage >= 1 && requestedStage <= 5 && requestedStage == completedStage + 1;
        }

        public static bool ShouldEstablishMemorial(bool hasPersistedDeath, bool lineageEnded,
            bool hasValidLivingLeader, bool memorialEstablished)
        {
            return hasPersistedDeath && !lineageEnded && hasValidLivingLeader && !memorialEstablished;
        }
    }
}
