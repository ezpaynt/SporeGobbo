using UnityEngine;

/// <summary>
/// Optional helper: add to CampScene systems if you want a save write when camp opens.
/// Not required, but useful while testing named saves.
/// </summary>
public class SaveCurrentOnCampReturn : MonoBehaviour
{
    public bool saveOnStart = true;

    void Start()
    {
        if (!saveOnStart) return;
        if (GameState.Instance == null) return;
        GameStateSaveBridge.GetOrCreate().SaveCurrentGame();
    }
}
