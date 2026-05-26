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

        if (openNamingScreen)
        {
            BuddyChoiceScreen screen = Object.FindAnyObjectByType<BuddyChoiceScreen>(FindObjectsInactive.Include);

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

        BuddyData data = CreateBabyData(chosenName);
        bool activeInRun = false;

        if (GameState.Instance != null)
        {
            GameState.Instance.AddOrUpdateBuddy(data, true);
            BuddyData saved = GameState.Instance.FindBuddy(data.uniqueId);
            if (saved != null)
                data = saved;

            activeInRun = data != null && GameState.Instance.activeSquadIds != null && GameState.Instance.activeSquadIds.Contains(data.uniqueId);
            GameState.Instance.RegisterBuddyFound(data);
        }
        else
        {
            BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();
            if (roster != null)
            {
                data = roster.CreateNewBuddy(BuddyType.Baby, chosenName);
                activeInRun = data != null && data.isInActiveSquad;
            }
            else
            {
                Debug.LogWarning("No GameState or BuddyRoster found. Baby hatched but cannot be saved.");
            }
        }

        GobboController player = Object.FindAnyObjectByType<GobboController>();

        if (player != null && data != null && activeInRun)
        {
            player.SpawnBuddy(data, transform.position);
        }
        else if (data != null && !activeInRun)
        {
            Debug.Log(data.buddyName + " joined the camp reserve.");
        }
        else if (player == null)
        {
            Debug.LogWarning("No GobboController found. Buddy was saved but not spawned into this run.");
        }

        Destroy(gameObject);
    }

    BuddyData CreateBabyData(string chosenName)
    {
        BuddyData buddy = new BuddyData();
        BuddyProgression.PrepareNewBaby(buddy);
        buddy.buddyName = string.IsNullOrWhiteSpace(chosenName) ? GetRandomBuddyName() : chosenName.Trim();
        return buddy;
    }

    string GetRandomBuddyName()
    {
        string[] names = { "Grub", "Pip", "Mug", "Bunk", "Snorp", "Wim", "Grot", "Nub", "Boil", "Lump" };
        return names[Random.Range(0, names.Length)];
    }
}
