using NUnit.Framework;
using UnityEngine;

namespace SporeGobbo.Input.Tests
{
    public sealed class CorePlayerControlMathTests
    {
        [Test]
        public void MovingDashUsesNormalizedMovement()
        {
            Vector2 result = CorePlayerControlMath.ResolveDashDirection(
                new Vector2(0.7f, 0.7f),
                Vector2.left,
                Vector2.down);

            Assert.That(result.x, Is.EqualTo(0.7071f).Within(0.001f));
            Assert.That(result.y, Is.EqualTo(0.7071f).Within(0.001f));
        }

        [Test]
        public void StationaryDashUsesAim()
        {
            Vector2 result = CorePlayerControlMath.ResolveDashDirection(
                Vector2.zero,
                Vector2.left,
                Vector2.down);

            Assert.That(result, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void TinyAnalogMovementDoesNotOverrideAim()
        {
            Vector2 result = CorePlayerControlMath.ResolveDashDirection(
                new Vector2(0.05f, 0f),
                Vector2.up,
                Vector2.down);

            Assert.That(result, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void MissingMovementAndAimUsesFallback()
        {
            Vector2 result = CorePlayerControlMath.ResolveDashDirection(
                Vector2.zero,
                Vector2.zero,
                Vector2.right);

            Assert.That(result, Is.EqualTo(Vector2.right));
        }

        [TestCase(1f, 0.7f)]
        [TestCase(1.2f, 0.5833333f)]
        [TestCase(0.8f, 0.875f)]
        public void AttackSpeedScalesGameplayInterval(float speed, float expected)
        {
            float result = CorePlayerControlMath.GetEffectiveAttackInterval(0.7f, speed);
            Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void InvalidAttackSpeedClampsSafely()
        {
            float result = CorePlayerControlMath.GetEffectiveAttackInterval(0.7f, 0f);
            Assert.That(result, Is.EqualTo(70f).Within(0.001f));
        }
    }
}
