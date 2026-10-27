using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class IntroCampTransition : MonoBehaviour
{
    public string campSceneName = "CampScene";
    public CanvasGroup fadeOverlay;
    public AudioSource rumbleSource;
    public ParticleSystem dustEffect;
    public CameraFollow cameraFollow;
    public float shakeStrength = 0.18f;
    public float rumbleSeconds = 0.7f;
    public float fadeSeconds = 0.8f;
    bool triggered;

    void Awake()
    {
        if (cameraFollow == null)
            cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!SampleSceneModeController.IsIntroMode || triggered || !other.CompareTag("Player")) return;
        BeginArrival(other.GetComponent<GobboController>());
    }

    public void BeginArrival(GobboController player)
    {
        if(triggered||player==null||!SampleSceneModeController.IsIntroMode)return;
        triggered=true;var intro=GetComponent<IntroRegion>();if(intro!=null){if(!intro.Sequence.PlugCleared){triggered=false;return;}intro.PrepareArrival();GameState.Instance.SavePlayer(player);}
        StartCoroutine(Transition(player));
    }

    IEnumerator Transition(GobboController player)
    {
        if (player != null) player.enabled = false;
        Rigidbody2D body = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (body != null) body.linearVelocity = Vector2.zero;
        if (rumbleSource != null) rumbleSource.Play();
        if (dustEffect != null) dustEffect.Play();
        Vector3 originalCameraOffset = cameraFollow != null ? cameraFollow.offset : Vector3.zero;

        float total = Mathf.Max(rumbleSeconds, fadeSeconds);
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += GetComponent<IntroRegion>()!=null?Time.deltaTime:Time.unscaledDeltaTime;
            if (fadeOverlay != null) fadeOverlay.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds));
            if (cameraFollow != null && elapsed < rumbleSeconds)
                cameraFollow.offset = originalCameraOffset + (Vector3)(Random.insideUnitCircle * shakeStrength);
            yield return null;
        }

        if (cameraFollow != null) cameraFollow.offset = originalCameraOffset;

        CampArrivalContext.SetPending(CampArrivalMode.NewGameIntro);
        SporePauseService.ResetAll();
        SceneManager.LoadScene(GameSceneCatalog.Resolve(campSceneName));
    }
}
