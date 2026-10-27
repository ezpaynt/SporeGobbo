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
    private SettlementCampRegion projectedRegion;
    private RectTransform popupRect;
    private Canvas popupCanvas;

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

        if(Application.isPlaying&&GameSceneCatalog.DevelopmentCandidate)CreateProjectedCaption();
        HidePopup();
    }

    void CreateProjectedCaption()
    {
        // The serialized legacy reference is the shared CampMessageUI canvas. Never move/hide that canvas for a pile.
        var font=popupText!=null?popupText.font:TMP_Settings.defaultFontAsset;
        var canvasObject=new GameObject("Resource caption canvas (view only)",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));canvasObject.transform.SetParent(transform,false);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
        var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
        var card=new GameObject("Projected resource counts",typeof(RectTransform),typeof(UnityEngine.UI.Image));card.transform.SetParent(canvasObject.transform,false);var image=card.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.02f,.03f,.025f,.72f);image.raycastTarget=false;
        var label=new GameObject("Actual inventory quantities",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(card.transform,false);var text=label.GetComponent<TextMeshProUGUI>();if(font!=null)text.font=font;text.color=Color.white;text.raycastTarget=false;
        popupPanel=card;popupText=text;
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

    void LateUpdate()
    {
        if(!GameSceneCatalog.DevelopmentCandidate||popupPanel==null||!popupPanel.activeSelf)return;
        if(Time.timeScale<=0||SporeGobbo.Input.SporeInputReader.Instance==null||SporeGobbo.Input.SporeInputReader.Instance.Context!=SporeGobbo.Input.SporeInputContext.Gameplay){popupPanel.SetActive(false);return;}
        if(projectedRegion==null)projectedRegion=Object.FindAnyObjectByType<SettlementCampRegion>();
        if(projectedRegion==null||!projectedRegion.Ready)return;
        if(popupRect==null){popupRect=popupPanel.GetComponent<RectTransform>();popupCanvas=popupPanel.GetComponentInParent<Canvas>();popupRect.sizeDelta=new Vector2(240,70);popupRect.localScale=Vector3.one;if(popupText!=null){popupText.fontSize=22;popupText.enableAutoSizing=false;popupText.margin=Vector4.zero;popupText.alignment=TextAlignmentOptions.MidlineLeft;var textRect=popupText.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(8,4);textRect.offsetMax=new Vector2(-8,-4);textRect.localScale=Vector3.one;}}
        var parent=popupRect.parent as RectTransform;if(parent==null||popupCanvas==null)return;
        Vector2 screen=projectedRegion.RegionCamera.WorldToScreenPoint(projectedRegion.Render(projectedRegion.World(projectedRegion.Definition.Marker("storage").Anchor)));screen.y+=55f*Screen.height/1080f;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,popupCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:popupCanvas.worldCamera,out var local)){popupRect.anchorMin=popupRect.anchorMax=parent.pivot;popupRect.anchoredPosition=local;}
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
        if(GameSceneCatalog.DevelopmentCandidate&&(Time.timeScale<=0||SporeGobbo.Input.SporeInputReader.Instance==null||SporeGobbo.Input.SporeInputReader.Instance.Context!=SporeGobbo.Input.SporeInputContext.Gameplay)){if(popupPanel!=null)popupPanel.SetActive(false);return;}
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
