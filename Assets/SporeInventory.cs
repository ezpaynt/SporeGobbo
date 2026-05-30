using UnityEngine;
using TMPro;

public class SporeInventory : MonoBehaviour
{
    public int spores = 0;
    public TextMeshProUGUI sporeText;

    void Start() => UpdateUI();

    public void AddSpore(int amount)
    {
        if (amount <= 0) return;
        spores += amount;
        if (GameState.Instance != null)
        {
            GameState.Instance.GetLeader().spores = spores;
            GameState.Instance.RegisterSporesGained(amount);
        }
        UpdateUI();
    }

    public bool UseSpore()
    {
        if (spores <= 0)
        {
            Debug.Log("No spores to plant!");
            return false;
        }

        spores--;
        if (GameState.Instance != null)
            GameState.Instance.GetLeader().spores = spores;
        UpdateUI();
        return true;
    }

    public void UpdateUI()
    {
        if (sporeText != null) sporeText.text = "Spores: " + spores;
    }
}
