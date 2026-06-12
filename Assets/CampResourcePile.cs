using System.Text;
using TMPro;
using UnityEngine;

[System.Serializable]
public class CampResourcePileVisual
{
    public CampResourceType resourceType = CampResourceType.Mushrooms;
    public string displayName = "";
    public SpriteRenderer spriteRenderer;
    public Sprite[] stageSprites = new Sprite[5];
    public int[] thresholds = new int[5] { 0, 5, 15, 30, 60 };
    public bool hideRendererWhenNoSprite = true;

    public int GetStageIndex(int amount)
    {
        int stage = 0;
        if (thresholds != null && thresholds.Length > 0)
        {
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (amount >= thresholds[i]) stage = i;
            }
        }

        int spriteCount = stageSprites != null ? stageSprites.Length : 0;
        if (spriteCount <= 0) return 0;
        return Mathf.Clamp(stage, 0, spriteCount - 1);
    }

    public string GetLabel()
    {
        return string.IsNullOrWhiteSpace(displayName) ? CampResourceService.GetDisplayName(resourceType) : displayName.Trim();
    }
}

[RequireComponent(typeof(Collider2D))]
public class CampResourcePile : MonoBehaviour
{
    [Header("Resource Visuals")]
    public CampResourcePileVisual[] resourceVisuals = new CampResourcePileVisual[0];

    [Header("Popup")]
    public GameObject popupPanel;
    public TMP_Text popupText;
    public string popupHeader = "";
    public bool useCampMessageFallback = true;
    public float popupRefreshSeconds = 0.35f;

    [Header("Player Detection")]
    public string playerTag = "Player";
    public bool warnIfColliderIsNotTrigger = true;

    private bool playerInRange;
    private float popupTimer;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null) trigger.isTrigger = true;
    }

    void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (warnIfColliderIsNotTrigger && trigger != null && !trigger.isTrigger)
            Debug.LogWarning(name + " has CampResourcePile but its Collider2D is not set as Trigger.", this);

        HidePopup();
    }

    void OnEnable()
    {
        CampResourceService.ResourcesChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        CampResourceService.ResourcesChanged -= Refresh;
        HidePopup();
        playerInRange = false;
    }

    void Update()
    {
        if (!playerInRange) return;

        popupTimer -= Time.deltaTime;
        if (popupTimer <= 0f)
            ShowPopup();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = true;
        ShowPopup();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = false;
        HidePopup();
    }

    public void Refresh()
    {
        ApplyResourceSprites();
        if (playerInRange) ShowPopup();
    }

    void ApplyResourceSprites()
    {
        if (resourceVisuals == null) return;

        foreach (CampResourcePileVisual visual in resourceVisuals)
        {
            if (visual == null || visual.spriteRenderer == null) continue;

            int amount = CampResourceService.GetAmount(GameState.Instance, visual.resourceType);
            int stage = visual.GetStageIndex(amount);
            Sprite sprite = visual.stageSprites != null && visual.stageSprites.Length > 0 ? visual.stageSprites[stage] : null;
            visual.spriteRenderer.sprite = sprite;
            if (visual.hideRendererWhenNoSprite) visual.spriteRenderer.enabled = sprite != null;
        }
    }

    void ShowPopup()
    {
        string message = BuildPopupText();
        popupTimer = Mathf.Max(0.05f, popupRefreshSeconds);

        if (popupPanel != null && popupText != null)
        {
            popupText.text = message;
            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling();
            return;
        }

        if (useCampMessageFallback)
            CampMessageUI.Show(message);
    }

    void HidePopup()
    {
        popupTimer = 0f;
        if (popupPanel != null) popupPanel.SetActive(false);
        else if (useCampMessageFallback && CampMessageUI.Instance != null) CampMessageUI.Instance.Hide();
    }

    string BuildPopupText()
    {
        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(popupHeader))
            builder.AppendLine(popupHeader.Trim());

        if (resourceVisuals != null)
        {
            foreach (CampResourcePileVisual visual in resourceVisuals)
            {
                if (visual == null) continue;
                int amount = CampResourceService.GetAmount(GameState.Instance, visual.resourceType);
                builder.AppendLine(visual.GetLabel() + ": " + amount);
            }
        }

        return builder.ToString().TrimEnd();
    }

    bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (other.GetComponentInParent<GobboController>() != null) return true;
        return !string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag);
    }
}
