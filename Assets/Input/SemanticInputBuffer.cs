using System;
using System.Collections.Generic;

namespace SporeGobbo.Input
{
    public enum BufferedInputAction
    {
        PrimaryAttack,
        Dash
    }

    /// <summary>Small timestamp-based intent buffer. It deliberately supports only approved actions.</summary>
    public sealed class SemanticInputBuffer
    {
        private readonly Dictionary<BufferedInputAction, double> expiresAt = new();

        public void Record(BufferedInputAction action, double currentTime, double durationSeconds)
        {
            if (durationSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));

            expiresAt[action] = currentTime + durationSeconds;
        }

        public bool IsBuffered(BufferedInputAction action, double currentTime)
        {
            if (!expiresAt.TryGetValue(action, out double expiry))
                return false;

            if (currentTime <= expiry)
                return true;

            expiresAt.Remove(action);
            return false;
        }

        public bool Consume(BufferedInputAction action, double currentTime)
        {
            if (!IsBuffered(action, currentTime))
                return false;

            expiresAt.Remove(action);
            return true;
        }

        public void Clear()
        {
            expiresAt.Clear();
        }
    }
}
