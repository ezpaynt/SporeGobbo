using UnityEngine;

public class ExitTrigger : MonoBehaviour
{
    public string sceneToLoad = "CampScene";
    public bool saveRunBeforeLeaving = true;
    public bool saveSlotAfterRunCommit = true;

    private bool used = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        used = true;
        RunReturnService.ReturnToCamp(
            sceneToLoad,
            saveRunBeforeLeaving,
            saveSlotAfterRunCommit,
            "boss exit portal");
    }

}
