using UnityEngine;

public class CampCameraLockedLayer : MonoBehaviour
{
    [Header("References")]
    public Camera gameplayCamera;
    public SpriteRenderer spriteRenderer;
    public bool useMainCameraIfMissing = true;

    [Header("Screen Lock")]
    public Vector2 positionOffset;
    public bool keepCurrentZ = true;
    public float zPosition;

    [Header("Rendering")]
    public int sortingOrder = 300;

    [Header("Optional Drift")]
    public Vector2 driftAmount;
    public float driftSpeed = 0.05f;

    void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        zPosition = transform.position.z;
    }

    void Awake()
    {
        CacheReferences();
        ApplyRendererSettings();
    }

    void OnEnable()
    {
        CacheReferences();
        ApplyRendererSettings();
        FollowCamera();
    }

    void LateUpdate()
    {
        CacheReferences();
        ApplyRendererSettings();
        FollowCamera();
    }

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (keepCurrentZ)
            zPosition = transform.position.z;

        ApplyRendererSettings();
    }

    void CacheReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (gameplayCamera == null && useMainCameraIfMissing)
            gameplayCamera = Camera.main;
    }

    void FollowCamera()
    {
        if (gameplayCamera == null)
            return;

        Vector2 drift = Vector2.zero;
        if (Application.isPlaying && driftAmount != Vector2.zero && driftSpeed > 0f)
        {
            drift = new Vector2(
                Mathf.Sin(Time.time * driftSpeed) * driftAmount.x,
                Mathf.Cos(Time.time * driftSpeed * 0.83f) * driftAmount.y);
        }

        Vector3 cameraPosition = gameplayCamera.transform.position;
        transform.position = new Vector3(
            cameraPosition.x + positionOffset.x + drift.x,
            cameraPosition.y + positionOffset.y + drift.y,
            keepCurrentZ ? zPosition : transform.position.z);
    }

    void ApplyRendererSettings()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = sortingOrder;
    }
}
