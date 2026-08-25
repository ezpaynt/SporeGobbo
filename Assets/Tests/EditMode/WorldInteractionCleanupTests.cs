using NUnit.Framework;
using UnityEngine;

public class WorldInteractionCleanupTests
{
    [Test]
    public void SharedRuntimePrompt_IsLoadableAndContainsTextPresenter()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/CampInteractionPrompt");

        Assert.That(prefab, Is.Not.Null);
        bool hasTextPresenter = false;
        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            if (component != null && component.GetType().FullName == "TMPro.TextMeshProUGUI")
            {
                hasTextPresenter = true;
                break;
            }
        }
        Assert.That(hasTextPresenter, Is.True);
    }
}
