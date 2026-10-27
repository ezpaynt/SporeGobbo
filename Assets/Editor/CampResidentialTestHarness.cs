#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FakeRunReturnTestWindow : EditorWindow
{
    int participantCount, deathCount, newBuddyCount;
    bool acknowledgeSaveRisk;

    [MenuItem("Tools/Spore Gobbo/Camp/Fake Run Return Test")]
    static void Open() => GetWindow<FakeRunReturnTestWindow>("Fake Run Return Test");

    void OnGUI()
    {
        EditorGUILayout.LabelField("FAKE COMPLETED RUN", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Authors Buddy facts for one completed run, then enters the real ReturnedFromRun report and Camp arrival flow.", MessageType.Info);
        EditorGUILayout.HelpBox("The production return flow saves the selected play save.", MessageType.Warning);
        acknowledgeSaveRisk = EditorGUILayout.ToggleLeft("I understand this may autosave over the selected play save", acknowledgeSaveRisk);
        participantCount = Mathf.Max(0, EditorGUILayout.IntField("Existing Buddies who went", participantCount));
        deathCount = Mathf.Max(0, EditorGUILayout.IntField("Existing Buddies who died", deathCount));
        newBuddyCount = Mathf.Max(0, EditorGUILayout.IntField("New Buddies acquired", newBuddyCount));
        EditorGUILayout.LabelField("Existing Buddies returned alive", Mathf.Max(0, participantCount - deathCount).ToString());
        FakeCompletedRunInput input = new FakeCompletedRunInput(participantCount, deathCount, newBuddyCount);
        bool available = IsAvailable(input, out string reason);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(reason, available ? MessageType.None : MessageType.Warning);
        using (new EditorGUI.DisabledScope(!acknowledgeSaveRisk || !available))
            if (GUILayout.Button("Simulate Completed Run + Return to Camp", GUILayout.Height(34))) SimulateAndReturn(input);
        if (EditorApplication.isPlaying) Repaint();
    }

    static bool IsAvailable(FakeCompletedRunInput input, out string reason)
    {
        if (!EditorApplication.isPlaying) { reason = "Enter Play Mode in CampScene."; return false; }
        if (!GameSceneCatalog.IsCamp(SceneManager.GetActiveScene())) { reason = "Open CampScene."; return false; }
        if (!V1RuntimeProfile.DevelopmentToolsEnabled) { reason = "Enable the isolated V1 development-tools profile."; return false; }
        return FakeCompletedRunBuilder.Validate(GameState.Instance, input, out reason);
    }

    static void SimulateAndReturn(FakeCompletedRunInput input)
    {
        if (!IsAvailable(input, out string reason)) { Debug.LogWarning("[Fake Run Return] " + reason); return; }
        FakeCompletedRunResult result = null;
        bool started = RunReturnService.ReturnSimulatedCampOutcome(() =>
            result = FakeCompletedRunBuilder.Apply(GameState.Instance, input));
        if (!started)
        {
            Debug.LogError("[Fake Run Return] Production return refused the valid fake result. State was left intact for diagnosis.");
            return;
        }
        Debug.Log("[Fake Run Return] Production return started | participated=" + result.ParticipantIds.Count +
                  " died=" + result.DeathIds.Count + " new=" + result.NewBuddyIds.Count + ".");
    }
}
#endif
