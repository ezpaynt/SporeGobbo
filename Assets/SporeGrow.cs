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
        if (hasGrown)
            return;

        timer += Time.deltaTime;

        if (timer >= growTime)
            FinishGrowing();
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
        if (hatchFinished)
            return;

        hatchFinished = true;
        Time.timeScale = 1f;

        BuddyRoster roster = UnityEngine.Object.FindAnyObjectByType<BuddyRoster>();

        if (roster == null)
        {
            Debug.LogError("No BuddyRoster found.");
            Destroy(gameObject);
            return;
        }

        // CreateNewBuddy already handles active vs reserve based on maxActiveSquad.
        // Do not destroy spores just because the active squad is full.
        BuddyData data = roster.CreateNewBuddy(BuddyType.Baby, chosenName);

        GobboController player = UnityEngine.Object.FindAnyObjectByType<GobboController>();

        if (player != null && data != null && data.isInActiveSquad)
        {
            player.SpawnBuddy(data, transform.position);
        }
        else if (data != null && !data.isInActiveSquad)
        {
            Debug.Log(data.buddyName + " joined the camp reserve.");
        }
        else if (player == null)
        {
            Debug.LogWarning("No GobboController found. Buddy was saved to roster but not spawned into run.");
        }

        if (GameState.Instance != null)
            GameState.Instance.RegisterBuddyFound(data);

        Destroy(gameObject);
    }
}
