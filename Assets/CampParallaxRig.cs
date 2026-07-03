using UnityEngine;

public class CampParallaxRig : MonoBehaviour
{
    [Header("Camera")]
    public Camera gameplayCamera;
    public bool useMainCameraIfMissing = true;

    [Header("Layers")]
    public CampParallaxLayer[] layers;

    private Vector3 initialCameraPosition;
    private bool initialized;

    void Reset()
    {
        RefreshLayersFromChildren();
    }

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    void LateUpdate()
    {
        if (!initialized)
            Initialize();

        if (gameplayCamera == null)
            return;

        Vector3 cameraDelta = gameplayCamera.transform.position - initialCameraPosition;
        if (layers == null)
            return;

        foreach (CampParallaxLayer layer in layers)
        {
            if (layer != null)
                layer.ApplyParallax(cameraDelta);
        }
    }

    public void Initialize()
    {
        if (gameplayCamera == null && useMainCameraIfMissing)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
            return;

        if (layers == null || layers.Length == 0)
            RefreshLayersFromChildren();

        initialCameraPosition = gameplayCamera.transform.position;
        initialized = true;

        if (layers == null)
            return;

        foreach (CampParallaxLayer layer in layers)
        {
            if (layer != null)
                layer.Initialize();
        }
    }

    [ContextMenu("Refresh Layers From Children")]
    public void RefreshLayersFromChildren()
    {
        layers = GetComponentsInChildren<CampParallaxLayer>(true);
    }
}
