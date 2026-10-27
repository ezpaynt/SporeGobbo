using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MushWormIntroRuntime : MonoBehaviour
{
    public const string MarkerKey = "mush_worm_intro";
    const string PlaceholderLine = "Placeholder line.";
    const float PresentationSeconds = 0.55f;
    MapGenerator map;
    IWorldRegion worldRegion;
    RegionStoryLocation location;
    Vector3 Cell(Vector2Int cell)=>map!=null?map.CellToWorld(cell):worldRegion.CellToWorld(cell);
    StoryEventCoordinator coordinator;
    StoryEventPresentation presentation;
    StoryEventTrigger trigger;
    Transform actor;
    Transform focus;
    bool ownsEvent;
    bool resolved;

    public static MushWormIntroRuntime Create(MapGenerator generator, PersistentTestCaveLayout.AuthoredEventLocation authoredLocation)
    {
        GameObject host = new GameObject("Mush/Worm Intro Event");
        MushWormIntroRuntime runtime = host.AddComponent<MushWormIntroRuntime>();
        runtime.Initialize(generator, authoredLocation);
        return runtime;
    }

    void Initialize(MapGenerator generator, PersistentTestCaveLayout.AuthoredEventLocation authoredLocation)
    {
        map = generator; InitializeCommon(RegionStoryLocation.Legacy(authoredLocation));
    }

    public static MushWormIntroRuntime Create(IWorldRegion region,RegionStoryLocation location){var host=new GameObject("Mush/Worm existing regional event");var runtime=host.AddComponent<MushWormIntroRuntime>();runtime.worldRegion=region;runtime.InitializeCommon(location);return runtime;}
    void InitializeCommon(RegionStoryLocation authoredLocation)
    {
        location=authoredLocation;coordinator = StoryEventCoordinator.GetOrCreate();
        actor = CreateWorldLabel("Mush/Worm Placeholder", "M + W", Cell(location.actorCell));
        focus = new GameObject("Mush/Worm Camera Focus").transform;
        focus.SetParent(transform, false); focus.position = Cell(location.focusCell);
        GameObject triggerObject = new GameObject("Mush/Worm Proximity Trigger");
        triggerObject.transform.SetParent(transform, false); triggerObject.transform.position = Cell(location.triggerCell);
        trigger = triggerObject.AddComponent<StoryEventTrigger>();
        trigger.Configure(location.triggerRadius * (map!=null?map.CellSize:worldRegion.CellSize), IsEligible, Begin);
    }

    bool IsEligible()
    {
        GameState state = GameState.Instance;
        return state != null && (map != null && map.TryGetAuthoredEventLocation(MarkerKey, out _) || worldRegion!=null&&worldRegion.LogicalReady&&location.markerKey==MarkerKey) &&
            MushWormIntroEligibility.IsEligible(state.currentRunNumber, state.storyProgress, true);
    }

    bool Begin()
    {
        if (!IsEligible() || ownsEvent || actor == null) return false;
        presentation = gameObject.AddComponent<StoryEventPresentation>();
        if (!presentation.Prepare("Mush/Worm", PlaceholderLine, Resolve))
        {
            Debug.LogError("Story event '" + StoryEventIds.MushWormIntro + "' could not prepare its camera/conversation presentation.", this);
            Destroy(presentation); presentation = null; return false;
        }
        if (!coordinator.TryBegin(StoryEventIds.MushWormIntro, this, presentation.ContinueButton, presentation.ModalRoot))
        {
            presentation.RestoreImmediately(); Destroy(presentation); presentation = null; return false;
        }
        ownsEvent = true; trigger.enabled = false;
        presentation.Begin(focus, location.cameraOffset, location.cameraZoomMultiplier, PresentationSeconds);
        return true;
    }

    void Resolve()
    {
        if (resolved || !ownsEvent) return;
        GameState state = GameState.Instance;
        if (state == null) { Debug.LogError("Story event lost GameState before resolution.", this); CancelWithoutCompletion(); return; }
        resolved = true;
        StoryEventService.Complete(state, StoryEventIds.MushWormIntro);
        StartCoroutine(Depart());
        presentation.Restore(PresentationSeconds, ReleasePresentation);
    }

    void ReleasePresentation()
    {
        if (!ownsEvent) return;
        coordinator.Finish(this); ownsEvent = false;
    }

    IEnumerator Depart()
    {
        var route = new List<Vector3>();
        if (location.departureWaypoints != null)
            foreach (Vector2Int cell in location.departureWaypoints) route.Add(Cell(cell));
        route.Add(Cell(location.despawnCell));
        foreach (Vector3 point in route)
        {
            Vector3 destination = new Vector3(point.x, point.y, actor.position.z);
            while (actor != null && (actor.position - destination).sqrMagnitude > 0.0025f)
            { actor.position = Vector3.MoveTowards(actor.position, destination, 1.6f * Time.deltaTime); yield return null; }
        }
        if (actor != null) Destroy(actor.gameObject);
        if (!ownsEvent) Destroy(gameObject);
    }

    void CancelWithoutCompletion()
    {
        if (presentation != null) presentation.RestoreImmediately();
        if (ownsEvent && coordinator != null) coordinator.Finish(this);
        ownsEvent = false;
    }

    void OnDestroy() => CancelWithoutCompletion();

    Transform CreateWorldLabel(string objectName, string text, Vector3 position)
    {
        GameObject labelObject = new GameObject(objectName);
        Transform logical=null;if(worldRegion!=null){logical=new GameObject(objectName+" logical anchor").transform;logical.SetParent(transform,false);logical.position=position;}
        labelObject.transform.SetParent(logical!=null?logical:transform, false);if(logical==null)labelObject.transform.position = new Vector3(position.x, position.y, -1f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text; label.fontSize = 64; label.characterSize = 0.08f;
        label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.color = new Color(0.82f, 0.66f, 0.28f);
        label.GetComponent<MeshRenderer>().sortingOrder = 50;
        if(logical!=null){labelObject.AddComponent<ProjectedWorldSprite>().Initialize(worldRegion,logical);return logical;}
        return labelObject.transform;
    }
}

public sealed class MushWormIntroBootstrap : MonoBehaviour
{
    static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { installed = false; SceneManager.sceneLoaded -= HandleSceneLoaded; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (installed) return;
        installed = true; SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene" || SampleSceneModeController.IsIntroMode) return;
        new GameObject("Mush/Worm Story Opportunity Bootstrap").AddComponent<MushWormIntroBootstrap>();
    }

    IEnumerator Start()
    {
        yield return null;
        if (SampleSceneModeController.IsIntroMode) { Destroy(gameObject); yield break; }
        float elapsed = 0f; MapGenerator generator = null;
        while (elapsed < 8f)
        {
            generator = MapGenerator.Instance;
            if (generator != null && generator.Data != null && generator.ActivePersistentTestLayout != null) break;
            elapsed += Time.unscaledDeltaTime; yield return null;
        }
        if (generator == null || generator.Data == null || generator.ActivePersistentTestLayout == null)
        { Destroy(gameObject); yield break; }
        bool hasMarker = generator.TryGetAuthoredEventLocation(MushWormIntroRuntime.MarkerKey, out var location);
        GameState state = GameState.Instance;
        if (state == null || !MushWormIntroEligibility.IsEligible(state.currentRunNumber, state.storyProgress, hasMarker))
        {
            if (!hasMarker && state != null && state.currentRunNumber >= 2 && !StoryEventService.IsCompleted(state, StoryEventIds.MushWormIntro))
                Debug.LogError("Story event '" + StoryEventIds.MushWormIntro + "' requires marker '" + MushWormIntroRuntime.MarkerKey +
                    "' in layout/profile '" + generator.ActivePersistentTestLayout.mapId + "'.", generator);
            Destroy(gameObject); yield break;
        }
        MushWormIntroRuntime.Create(generator, location);
        Destroy(gameObject);
    }
}
