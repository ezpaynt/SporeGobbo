using UnityEngine;

public class CollectibleShiny : MonoBehaviour
{
    public int amount = 1;
    public float pickupRange = 1.1f;
    public KeyCode pickupKey = KeyCode.E;

    void Update()
    {
        if (!Input.GetKeyDown(pickupKey))
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        if (Vector2.Distance(transform.position, player.transform.position) > pickupRange)
            return;

        if (GameState.Instance != null)
            CampResourceService.Add(GameState.Instance, CampResourceType.Shinies, amount, false);

        Debug.Log("Picked up shiny!");
        Destroy(gameObject);
    }
}
