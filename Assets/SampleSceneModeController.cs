using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class SampleSceneModeController : MonoBehaviour
{
    [Header("Editor Preview")]
    public SampleSceneMode editorPreviewMode = SampleSceneMode.NormalRun;

    [Header("Profiles")]
    public RunProfile introProfile;
    public RunProfile normalRunProfile;

    [Header("Scene References")]
    public MapGenerator mapGenerator;
    public RunContentSpawner contentSpawner;
    public RunSquadSpawner squadSpawner;
    public IntroCampTransition introTransition;

    public static SampleSceneMode CurrentMode { get; private set; } = SampleSceneMode.NormalRun;
    public static bool IsIntroMode => CurrentMode == SampleSceneMode.Intro;

    void Awake()
    {
        CurrentMode = SampleSceneModeContext.ConsumeOrDefault();
        ApplyConfiguration(CurrentMode);
    }

    public void ConfigureForEditorPreview()
    {
        CurrentMode = editorPreviewMode;
        ApplyConfiguration(CurrentMode);
    }

    void ApplyConfiguration(SampleSceneMode mode)
    {
        ResolveReferences();

        bool intro = mode == SampleSceneMode.Intro;
        RunProfile profile = intro ? introProfile : normalRunProfile;
        if (mapGenerator == null || profile == null)
        {
            Debug.LogError("SampleSceneModeController cannot configure SampleScene because its MapGenerator or selected profile is missing.", this);
            enabled = false;
            return;
        }

        mapGenerator.useProfilesByRunNumber = false;
        mapGenerator.selectedProfile = profile;
        mapGenerator.generateRunContent = !intro;
        mapGenerator.requireRunExit = !intro;

        if (contentSpawner != null) contentSpawner.enabled = !intro;
        if (squadSpawner != null) squadSpawner.enabled = !intro;
        if (introTransition != null) introTransition.gameObject.SetActive(false);

        Debug.Log("SAMPLE SCENE MODE | mode=" + mode + " profile=" + profile.name, this);
    }

    System.Collections.IEnumerator Start()
    {
        if (!IsIntroMode || mapGenerator == null || introTransition == null) yield break;

        float elapsed = 0f;
        while (mapGenerator.Data == null && elapsed < 5f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (mapGenerator.Data == null || !mapGenerator.HasTerminalPocket)
        {
            Debug.LogError("Intro mode generated without a valid terminal pocket. Transition remains disabled.", this);
            yield break;
        }

        Vector3 position = mapGenerator.TerminalPocketWorldPosition;
        position.z = introTransition.transform.position.z;
        introTransition.transform.position = position;
        introTransition.gameObject.SetActive(true);
    }

    void ResolveReferences()
    {
        if (mapGenerator == null) mapGenerator = Object.FindAnyObjectByType<MapGenerator>(FindObjectsInactive.Include);
        if (contentSpawner == null) contentSpawner = Object.FindAnyObjectByType<RunContentSpawner>(FindObjectsInactive.Include);
        if (squadSpawner == null) squadSpawner = Object.FindAnyObjectByType<RunSquadSpawner>(FindObjectsInactive.Include);
        if (introTransition == null) introTransition = Object.FindAnyObjectByType<IntroCampTransition>(FindObjectsInactive.Include);
    }
}
