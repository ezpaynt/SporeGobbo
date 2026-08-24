using NUnit.Framework;

namespace SporeGobbo.Input.Tests
{
    public sealed class SemanticInputBufferTests
    {
        [Test]
        public void BufferedIntentExpiresAndCannotBeConsumed()
        {
            var buffer = new SemanticInputBuffer();
            buffer.Record(BufferedInputAction.Dash, 10d, 0.12d);

            Assert.That(buffer.IsBuffered(BufferedInputAction.Dash, 10.1d), Is.True);
            Assert.That(buffer.Consume(BufferedInputAction.Dash, 10.13d), Is.False);
        }

        [Test]
        public void ConsumeSucceedsOnlyOnce()
        {
            var buffer = new SemanticInputBuffer();
            buffer.Record(BufferedInputAction.PrimaryAttack, 2d, 0.12d);

            Assert.That(buffer.Consume(BufferedInputAction.PrimaryAttack, 2.05d), Is.True);
            Assert.That(buffer.Consume(BufferedInputAction.PrimaryAttack, 2.06d), Is.False);
        }

        [Test]
        public void ClearRemovesAllBufferedIntent()
        {
            var buffer = new SemanticInputBuffer();
            buffer.Record(BufferedInputAction.PrimaryAttack, 1d, 0.12d);
            buffer.Record(BufferedInputAction.Dash, 1d, 0.12d);

            buffer.Clear();

            Assert.That(buffer.IsBuffered(BufferedInputAction.PrimaryAttack, 1.01d), Is.False);
            Assert.That(buffer.IsBuffered(BufferedInputAction.Dash, 1.01d), Is.False);
        }
    }
}
