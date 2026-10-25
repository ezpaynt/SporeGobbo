using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using SporeGobbo.Geometry.Preview.Editor;

namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class PlayableLifecycleTests
    {
        [UnityTest] public IEnumerator PreviewEntersPlayAndRespondsToKeyboardWithoutProductionTerrain()
        {
            EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var live=Object.FindAnyObjectByType<PlayableCavePreview>();
            Assert.That(live.Session,Is.Not.Null);
            Vector2 initial=live.Position;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var mouse=InputSystem.AddDevice<Mouse>();
            Vector3 screen=live.GetComponent<CaveGeometryPreview>().previewCamera.WorldToScreenPoint(new Vector3(initial.x,-initial.y+4,0));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));
            // Batch Editor focus does not reliably deliver queued virtual input on its own player loop.
            // Process the virtual device state explicitly, then exercise the same input tick as Update.
            InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();
            Assert.That(keyboard.wKey.isPressed,Is.True);
            for(int i=0;i<20;i++)live.TickInput(.02f);
            Assert.That(Vector2.Distance(initial,live.Position),Is.GreaterThan(.01f));
            Assert.That(PreviewMotion.Fits(live.Session.Terrain,live.Position,live.footprint),Is.True);
            int before=live.Session.DigCount;
            live.Predict(new Vector2(0,0));live.DigNow();
            Assert.That(live.Session.DigCount,Is.GreaterThan(before));
            foreach(var component in Object.FindObjectsByType<MonoBehaviour>())
                Assert.That(component.GetType().Name,Is.Not.EqualTo("MapGenerator"));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            yield return new ExitPlayMode();
        }
    }
}
