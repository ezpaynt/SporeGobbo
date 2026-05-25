using UnityEngine;

// Legacy placeholder kept so old serialized references do not become Missing Script.
// Current upgrades should use GobboCard and GobboCardDatabase instead.
[System.Serializable]
public class GobboUpgrade
{
    public string upgradeName;
    public string description;

    public bool CanAppear(GobboController gobbo)
    {
        return false;
    }

    public void Apply(GobboController gobbo)
    {
        Debug.LogWarning("GobboUpgrade is legacy. Use GobboCard instead: " + upgradeName);
    }
}
