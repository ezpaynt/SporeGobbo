using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SporeGobbo.Input.Tests
{
    public sealed class SporeInputActionContractTests
    {
        [Test]
        public void ContractContainsOnlySupportedMapsAndSchemes()
        {
            InputActionAsset actions = LoadContract();

            CollectionAssert.AreEquivalent(
                new[] { "Gameplay", "UI", "Wheel", "Debug" },
                actions.actionMaps.Select(map => map.name));
            CollectionAssert.AreEquivalent(
                new[] { "KeyboardMouse", "Gamepad" },
                actions.controlSchemes.Select(scheme => scheme.name));
        }

        [TestCase("Gameplay/PrimaryAttack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger")]
        [TestCase("Gameplay/SecondaryAbility", "<Mouse>/rightButton", "<Gamepad>/leftTrigger")]
        [TestCase("Gameplay/Dig", "<Keyboard>/leftShift", "<Gamepad>/buttonSouth")]
        [TestCase("Gameplay/Dash", "<Keyboard>/space", "<Gamepad>/buttonWest")]
        [TestCase("Gameplay/Interact", "<Keyboard>/f", "<Gamepad>/buttonNorth")]
        [TestCase("Gameplay/CommandWheel", "<Keyboard>/c", "<Gamepad>/leftShoulder")]
        [TestCase("Gameplay/PlantSpore", "<Keyboard>/q", "<Gamepad>/dpad/up")]
        [TestCase("Gameplay/Pause", "<Keyboard>/escape", "<Gamepad>/start")]
        public void GameplayActionHasAgreedBindings(string actionPath, string keyboardPath, string gamepadPath)
        {
            InputActionAsset actions = LoadContract();
            var action = actions.FindAction(actionPath, true);
            string[] paths = action.bindings.Select(binding => binding.path).ToArray();

            CollectionAssert.Contains(paths, keyboardPath);
            CollectionAssert.Contains(paths, gamepadPath);
        }

        [Test]
        public void InteractHasNoGlobalHoldInteraction()
        {
            InputActionAsset actions = LoadContract();
            var interact = actions.FindAction("Gameplay/Interact", true);

            Assert.That(interact.interactions, Does.Not.Contain("Hold").IgnoreCase);
            Assert.That(interact.bindings.All(binding =>
                string.IsNullOrEmpty(binding.interactions) ||
                !binding.interactions.Contains("Hold")), Is.True);
        }

        [Test]
        public void DebugMapContainsOnlyKeyboardMouseTestZoom()
        {
            InputActionAsset actions = LoadContract();
            InputActionMap debug = actions.FindActionMap("Debug", true);
            CollectionAssert.AreEqual(new[] { "TestZoom" }, debug.actions.Select(action => action.name));
            InputAction zoom = debug.FindAction("TestZoom", true);
            Assert.That(zoom.bindings.Count, Is.EqualTo(1));
            Assert.That(zoom.bindings[0].path, Is.EqualTo("<Mouse>/scroll"));
            Assert.That(zoom.bindings[0].groups, Is.EqualTo("KeyboardMouse"));
            Assert.That(zoom.bindings.Any(binding => binding.path.Contains("Gamepad")), Is.False);
        }

        [Test]
        public void ControlSchemesRequireTheirIntendedDevices()
        {
            InputActionAsset actions = LoadContract();
            InputControlScheme keyboardMouse = actions.controlSchemes.First(scheme => scheme.name == "KeyboardMouse");
            InputControlScheme gamepad = actions.controlSchemes.First(scheme => scheme.name == "Gamepad");
            CollectionAssert.AreEquivalent(new[] { "<Keyboard>", "<Mouse>" },
                keyboardMouse.deviceRequirements.Select(requirement => requirement.controlPath));
            CollectionAssert.AreEqual(new[] { "<Gamepad>" },
                gamepad.deviceRequirements.Select(requirement => requirement.controlPath));
        }

        private static InputActionAsset LoadContract()
        {
            InputActionAsset asset = Resources.FindObjectsOfTypeAll<InputActionAsset>()
                .FirstOrDefault(candidate => candidate.name == "InputSystem_Actions");
            if (asset != null)
                return asset;

            return UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");
        }
    }
}
