using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveOnApplicationQuit : MonoBehaviour
{
    public string campSceneName = "CampScene";
    public bool saveOnlyInCamp = true;
    public bool logDebugMessages = true;

    private void OnApplicationQuit()
    {
        TrySave("application quit");
    }

    public void TrySave(string reason = "manual")
    {
        if (saveOnlyInCamp && SceneManager.GetActiveScene().name != campSceneName)
        {
            if (logDebugMessages) Debug.Log("[SaveOnApplicationQuit] Skipping save during " + reason + " because active scene is not camp.");
            return;
        }

        if (GameState.Instance == null)
        {
            if (logDebugMessages) Debug.Log("[SaveOnApplicationQuit] No GameState. Nothing to save.");
            return;
        }

        SporeSaveManager.SaveCurrentSlotFromGameState();
        if (logDebugMessages) Debug.Log("[SaveOnApplicationQuit] Saved current camp state for " + reason + ".");
    }
}
