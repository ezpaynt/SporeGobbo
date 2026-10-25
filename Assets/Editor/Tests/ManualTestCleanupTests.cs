#if UNITY_EDITOR
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class ManualTestCleanupTests
{
    [Test]
    public void PausedGameplayRejectsCameraTestZoom()
    {
        Assert.That(CameraFollow.ShouldHandleTestZoom(true, true, false), Is.True);
        Assert.That(CameraFollow.ShouldHandleTestZoom(true, true, true), Is.False);
    }

    [Test]
    public void CampUsesSharedPauseMenuControllerUnderCanvas()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        PauseMenuController controller = Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        CampSceneController camp = Object.FindAnyObjectByType<CampSceneController>(FindObjectsInactive.Include);
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller, Is.InstanceOf<ISporePauseScreen>());
        Assert.That(camp, Is.Not.InstanceOf<ISporePauseScreen>());
        Assert.That(controller.pausePanel, Is.Not.Null);
        Assert.That(controller.pausePanel.GetComponentInParent<Canvas>(true), Is.Not.Null,
            "Camp PausePanel must live under a rendering Canvas.");
    }

    [Test]
    public void ReturnPortalHasNoActiveLegacyPlaceholderPrompt()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReturnPortal.prefab");
        Assert.That(prefab, Is.Not.Null);
        Transform canvas = prefab.transform.Find("ReturnPromptCanvas");
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.gameObject.activeSelf, Is.False);
        TMP_Text text = canvas.GetComponentInChildren<TMP_Text>(true);
        Assert.That(text, Is.Not.Null);
        Assert.That(text.text, Is.Empty);
    }
}
#endif
