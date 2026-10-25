using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryEventPresentation : MonoBehaviour
{
    private Camera cameraComponent;
    private CameraFollow cameraFollow;
    private Transform previousTarget;
    private Vector3 previousOffset;
    private float previousSize;
    private GameObject overlay;
    private Image dim;
    private EventConversationView conversation;
    private Coroutine transition;
    private bool captured;

    public Button ContinueButton => conversation != null ? conversation.ContinueButton : null;
    public GameObject ModalRoot => overlay;

    public bool Prepare(string speaker, string line, Action continued)
    {
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        cameraComponent = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : null;
        Canvas canvas = ResolvePresentationCanvas();
        if (cameraComponent == null || cameraFollow == null || canvas == null) return false;

        overlay = new GameObject("Story Event Presentation", typeof(RectTransform));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform root = (RectTransform)overlay.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(root, false);
        RectTransform dimRect = (RectTransform)dimObject.transform;
        dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
        dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        conversation = new GameObject("Conversation", typeof(RectTransform)).AddComponent<EventConversationView>();
        conversation.transform.SetParent(root, false);
        conversation.Build(speaker, line, continued);
        conversation.Group.alpha = 0f;
        return true;
    }

    static Canvas ResolvePresentationCanvas()
    {
        PauseMenuController pause = FindAnyObjectByType<PauseMenuController>();
        if (pause != null && pause.pausePanel != null)
        {
            Canvas pauseCanvas = pause.pausePanel.GetComponentInParent<Canvas>(true);
            if (pauseCanvas != null) return pauseCanvas;
        }

        Canvas fallback = null;
        foreach (Canvas candidate in FindObjectsByType<Canvas>())
        {
            if (candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            CanvasGroup group = candidate.GetComponent<CanvasGroup>();
            if (group != null && (!group.interactable || group.alpha <= 0.01f)) continue;
            if (fallback == null || candidate.sortingOrder < fallback.sortingOrder) fallback = candidate;
        }
        return fallback;
    }

    public void Begin(Transform focus, Vector3 focusOffset, float zoomMultiplier, float seconds)
    {
        previousTarget = cameraFollow.target;
        previousOffset = cameraFollow.offset;
        previousSize = cameraComponent.orthographicSize;
        captured = true;
        cameraFollow.target = focus;
        cameraFollow.offset = focusOffset;
        transition = StartCoroutine(FadeIn(Mathf.Max(0.05f, seconds), Mathf.Clamp(zoomMultiplier, 0.5f, 1f)));
    }

    public void Restore(float seconds, Action restored)
    {
        if (transition != null) StopCoroutine(transition);
        if (captured) { cameraFollow.target = previousTarget; cameraFollow.offset = previousOffset; }
        transition = StartCoroutine(FadeOut(Mathf.Max(0.05f, seconds), restored));
    }

    IEnumerator FadeIn(float seconds, float zoomMultiplier)
    {
        float startSize = previousSize;
        float targetSize = previousSize * zoomMultiplier;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / seconds);
            cameraComponent.orthographicSize = Mathf.Lerp(startSize, targetSize, p);
            dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.38f, p));
            conversation.Group.alpha = p;
            yield return null;
        }
        cameraComponent.orthographicSize = targetSize;
        dim.color = new Color(0f, 0f, 0f, 0.38f);
        conversation.Group.alpha = 1f;
        UiFocusUtility.Select(conversation.ContinueButton);
    }

    IEnumerator FadeOut(float seconds, Action restored)
    {
        float startSize = cameraComponent != null ? cameraComponent.orthographicSize : previousSize;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / seconds);
            if (cameraComponent != null) cameraComponent.orthographicSize = Mathf.Lerp(startSize, previousSize, p);
            if (dim != null) dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.38f, 0f, p));
            if (conversation != null) conversation.Group.alpha = 1f - p;
            yield return null;
        }
        RestoreImmediately();
        restored?.Invoke();
    }

    public void RestoreImmediately()
    {
        if (captured)
        {
            if (cameraFollow != null) { cameraFollow.target = previousTarget; cameraFollow.offset = previousOffset; }
            if (cameraComponent != null) cameraComponent.orthographicSize = previousSize;
        }
        captured = false;
        if (overlay != null)
        {
            if (Application.isPlaying) Destroy(overlay);
            else DestroyImmediate(overlay);
        }
    }

    void OnDestroy() => RestoreImmediately();
}
