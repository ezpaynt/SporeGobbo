using UnityEngine;

public class SporeGrow : MonoBehaviour
{
    public float growTime = 5f;

    [Header("Buddy Spawn")]
    public GameObject buddyPrefab;
    public bool openNamingScreen = true;

    private float timer = 0f;
    private bool hasGrown = false;
    private bool hatchFinished = false;

    void Update()
    {
        if (hasGrown) return;

        timer += Time.deltaTime;
        if (timer >= growTime) FinishGrowing();
    }

    void FinishGrowing()
    {
        hasGrown = true;

        // Spores hatch Baby gobbos. Type/class is chosen later at camp after XP.
        if (openNamingScreen)
        {
            BuddyChoiceScreen screen = UnityEngine.Object.FindAnyObjectByType<BuddyChoiceScreen>(FindObjectsInactive.Include);
            if (screen != null)
            {
                screen.OpenForSpore(this);
                return;
            }

            Debug.LogWarning("No BuddyChoiceScreen found. Hatching baby with random/default name.");
        }

        CompleteHatch(BuddyType.Baby, "");
    }

    public void CompleteHatch(BuddyType ignoredType, string chosenName)
    {
        if (hatchFinished) return;
        hatchFinished = true;
        Time.timeScale = 1f;

        GameState state = GameState.Instance;
        if (state == null)
        {
            Debug.LogError("No GameState found for new gobbo.");
            Destroy(gameObject);
            return;
        }

        GobboUnitSaveData data = CreateNewBabyGobbo(chosenName);

        bool preferActive = state.activeSquadIds == null || state.activeSquadIds.Count < Mathf.Max(1, state.maxActiveSquad);
        state.AddGobbo(data, preferActive);
        state.RepairRosterState();

        bool isActive = state.activeSquadIds != null && state.activeSquadIds.Contains(data.uniqueId);

        GobboController player = UnityEngine.Object.FindAnyObjectByType<GobboController>();
        if (player != null && isActive)
        {
            player.SpawnGobboUnit(data, transform.position);
        }
        else if (!isActive)
        {
            Debug.Log(data.displayName + " joined the camp reserve.");
        }
        else
        {
            Debug.LogWarning("No GobboController found. Gobbo was saved to roster but not spawned into run.");
        }

        state.RegisterGobboFound(data);
        Destroy(gameObject);
    }

    private GobboUnitSaveData CreateNewBabyGobbo(string chosenName)
    {
        GobboUnitSaveData data = new GobboUnitSaveData();
        data.isLeader = false;
        data.gobboType = BuddyType.Baby;
        data.ageStage = GobboAgeStage.Baby;
        data.displayName = string.IsNullOrWhiteSpace(chosenName) ? "Baby" : chosenName.Trim();
        BuddyProgression.PrepareNewBaby(data);
        data.EnsureRuntimeDefaults();
        return data;
    }
}
