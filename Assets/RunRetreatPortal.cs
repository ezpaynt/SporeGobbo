using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RunRetreatPortal : MonoBehaviour, ICampInteractable
{
    [Header("Return")]
    public string campSceneName = "CampScene";
    public bool saveRunBeforeLeaving = true;
    public bool saveSlotAfterRunCommit = true;

    [Header("Interaction")]
    public float promptRange = 1.15f;
    public string interactPrompt = "Retreat to Camp";

    [Header("Prompt")]
    public GameObject promptObject;
    public TMP_Text promptText;
    public string promptDisplayText = "E - Retreat to Camp";

    private Transform player;

    void Awake()
    {
        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

        HidePrompt();
    }

    void Update()
    {
        FindPlayerIfMissing();

        bool playerIsNear = player != null &&
                            Vector2.Distance(transform.position, player.position) <= Mathf.Max(0.1f, promptRange);

        if (playerIsNear)
            ShowPrompt();
        else
            HidePrompt();
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }

    public void Interact(GobboController playerController)
    {
        if (playerController == null)
            return;

        float distance = Vector2.Distance(transform.position, playerController.transform.position);
        if (distance > Mathf.Max(promptRange, playerController.interactRange))
            return;

        HidePrompt();
        RunReturnService.ReturnToCamp(
            campSceneName,
            saveRunBeforeLeaving,
            saveSlotAfterRunCommit,
            "spawn retreat portal");
    }

    void FindPlayerIfMissing()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    void ShowPrompt()
    {
        if (promptText != null)
            promptText.text = promptDisplayText;

        if (promptObject != null)
            promptObject.SetActive(true);
        else if (promptText != null)
            promptText.gameObject.SetActive(true);
    }

    void HidePrompt()
    {
        if (promptObject != null)
            promptObject.SetActive(false);
        else if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        HidePrompt();
    }
}
