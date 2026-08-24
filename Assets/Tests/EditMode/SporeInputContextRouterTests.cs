using NUnit.Framework;

namespace SporeGobbo.Input.Tests
{
    public sealed class SporeInputContextRouterTests
    {
        [Test]
        public void WheelKeepsMovementWheelButtonAndPauseButDisablesGameplayAimAndWorldActions()
        {
            SporeInputAvailability availability =
                SporeInputContextRouter.GetAvailability(SporeInputContext.Wheel);

            Assert.That(availability.GameplayMap, Is.True);
            Assert.That(availability.Move, Is.True);
            Assert.That(availability.Aim, Is.False);
            Assert.That(availability.CommandWheel, Is.True);
            Assert.That(availability.Pause, Is.True);
            Assert.That(availability.WheelMap, Is.True);
            Assert.That(availability.GameplayWorldActions, Is.False);
            Assert.That(availability.UiMap, Is.False);
        }

        [Test]
        public void ModalRoutesToUiWithoutPauseOrGameplay()
        {
            SporeInputAvailability availability =
                SporeInputContextRouter.GetAvailability(SporeInputContext.Modal);

            Assert.That(availability.UiMap, Is.True);
            Assert.That(availability.GameplayMap, Is.False);
            Assert.That(availability.Pause, Is.False);
            Assert.That(availability.GameplayWorldActions, Is.False);
        }

        [Test]
        public void PauseKeepsOnlyPauseRouteFromGameplayMapAndEnablesUi()
        {
            SporeInputAvailability availability =
                SporeInputContextRouter.GetAvailability(SporeInputContext.Pause);

            Assert.That(availability.GameplayMap, Is.True);
            Assert.That(availability.Pause, Is.True);
            Assert.That(availability.UiMap, Is.True);
            Assert.That(availability.Move, Is.False);
            Assert.That(availability.Aim, Is.False);
            Assert.That(availability.CommandWheel, Is.False);
            Assert.That(availability.GameplayWorldActions, Is.False);
        }

        [Test]
        public void ChangingContextRaisesOneExplicitTransition()
        {
            var router = new SporeInputContextRouter();
            int transitions = 0;
            SporeInputContext observedPrevious = default;
            SporeInputContext observedNext = default;
            router.Changed += (previous, next) =>
            {
                transitions++;
                observedPrevious = previous;
                observedNext = next;
            };

            Assert.That(router.SetContext(SporeInputContext.Wheel), Is.True);
            Assert.That(router.SetContext(SporeInputContext.Wheel), Is.False);
            Assert.That(transitions, Is.EqualTo(1));
            Assert.That(observedPrevious, Is.EqualTo(SporeInputContext.Gameplay));
            Assert.That(observedNext, Is.EqualTo(SporeInputContext.Wheel));
        }
    }
}
