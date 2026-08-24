using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SporeGobbo.Input.Tests
{
    public sealed class SporeBindingServiceTests
    {
        InputActionAsset asset;
        InputAction gameplayDig, gameplayDash, uiSubmit;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap gameplay = new("Gameplay"); asset.AddActionMap(gameplay);
            gameplayDig = gameplay.AddAction("Dig", InputActionType.Button); gameplayDig.expectedControlType = "Button";
            gameplayDig.AddBinding("<Keyboard>/leftShift", groups: "KeyboardMouse"); gameplayDig.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
            gameplayDash = gameplay.AddAction("Dash", InputActionType.Button); gameplayDash.expectedControlType = "Button";
            gameplayDash.AddBinding("<Keyboard>/space", groups: "KeyboardMouse"); gameplayDash.AddBinding("<Gamepad>/buttonWest", groups: "Gamepad");
            InputActionMap ui = new("UI"); asset.AddActionMap(ui); uiSubmit = ui.AddAction("Submit", InputActionType.Button);
            uiSubmit.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(asset);

        [Test]
        public void OverrideSerializesAndLoads()
        {
            gameplayDig.ApplyBindingOverride(0, "<Keyboard>/g"); string json = asset.SaveBindingOverridesAsJson();
            gameplayDig.RemoveAllBindingOverrides(); asset.LoadBindingOverridesFromJson(json);
            Assert.That(gameplayDig.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/g"));
        }

        [Test]
        public void MissingJsonLeavesDefaults()
        {
            using var service = new SporeBindingService(asset, false); service.LoadJson("");
            Assert.That(gameplayDig.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/leftShift"));
        }

        [Test]
        public void InvalidJsonFailsSafelyBeforeApplying()
        {
            using var service = new SporeBindingService(asset, false);
            Assert.Throws<System.FormatException>(() => service.LoadJson("not-json"));
            Assert.That(gameplayDig.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/leftShift"));
        }

        [Test]
        public void ResetRestoresDefaultDisplay()
        {
            using var service = new SporeBindingService(asset, false); gameplayDig.ApplyBindingOverride(0, "<Keyboard>/g");
            service.ResetAll(); Assert.That(service.GetDisplay("Gameplay", "Dig", BindingScheme.KeyboardMouse), Is.EqualTo("Left Shift"));
        }

        [Test]
        public void KeyboardDisplayChangesAfterOverride()
        {
            using var service = new SporeBindingService(asset, false); gameplayDig.ApplyBindingOverride(0, "<Keyboard>/g");
            Assert.That(service.GetDisplay("Gameplay", "Dig", BindingScheme.KeyboardMouse), Is.EqualTo("G"));
        }

        [Test]
        public void GamepadDisplayChangesAfterOverride()
        {
            using var service = new SporeBindingService(asset, false); gameplayDig.ApplyBindingOverride(1, "<Gamepad>/buttonNorth");
            Assert.That(service.GetDisplay("Gameplay", "Dig", BindingScheme.Gamepad), Is.EqualTo("Y"));
        }

        [Test]
        public void SameGameplayControlIsConflict()
        {
            Assert.That(SporeBindingRules.IsSameContextConflict(gameplayDig, gameplayDash,
                "<Gamepad>/buttonSouth", "<Gamepad>/buttonSouth"), Is.True);
        }

        [Test]
        public void SameActionCompositePartsCanConflict()
        {
            Assert.That(SporeBindingRules.IsSameContextConflict(gameplayDig, gameplayDig,
                "<Keyboard>/leftShift", "<Keyboard>/leftShift"), Is.True);
        }

        [Test]
        public void CrossContextReuseIsNotConflict()
        {
            Assert.That(SporeBindingRules.IsSameContextConflict(gameplayDig, uiSubmit,
                "<Gamepad>/buttonSouth", "<Gamepad>/buttonSouth"), Is.False);
        }

        [TestCase(BindingScheme.KeyboardMouse, "<Keyboard>/g", true)]
        [TestCase(BindingScheme.KeyboardMouse, "<Mouse>/leftButton", true)]
        [TestCase(BindingScheme.KeyboardMouse, "<Gamepad>/buttonSouth", false)]
        [TestCase(BindingScheme.Gamepad, "<Gamepad>/leftTrigger", true)]
        [TestCase(BindingScheme.Gamepad, "<Keyboard>/g", false)]
        public void SchemeFiltering(BindingScheme scheme, string path, bool expected) =>
            Assert.That(SporeBindingRules.IsControlAllowed(scheme, path), Is.EqualTo(expected));

        [Test]
        public void CancelWithoutOperationLeavesBindingUnchanged()
        {
            using var service = new SporeBindingService(asset, false); string before = gameplayDig.bindings[0].effectivePath;
            service.CancelRebind(); Assert.That(gameplayDig.bindings[0].effectivePath, Is.EqualTo(before));
        }
    }
}
