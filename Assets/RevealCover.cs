using UnityEngine;

public class RevealCover : MonoBehaviour
{
    public enum RevealType
    {
        Camp,
        Tunnel
    }

    [Header("Reveal")]
    public RevealType revealType;
    public int revealId;

    [Header("Digging")]
    public int health = 3;

    private bool revealed = false;

    public void Dig(int power)
    {
        if (revealed)
            return;

        health -= power;

        if (health <= 0)
            Reveal();
    }

    void Reveal()
    {
        if (revealed)
            return;

        revealed = true;

        if (MapGenerator.Instance != null)
        {
            switch (revealType)
            {
                case RevealType.Camp:
                    MapGenerator.Instance.RevealCamp(revealId);
                    break;

                case RevealType.Tunnel:
                    MapGenerator.Instance.RevealTunnel(revealId);
                    break;
            }
        }

        Destroy(gameObject);
    }
}