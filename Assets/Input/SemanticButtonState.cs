namespace SporeGobbo.Input
{
    /// <summary>Frame-local lifecycle for one semantic button action.</summary>
    public readonly struct SemanticButtonState
    {
        public SemanticButtonState(bool startedThisFrame, bool isHeld, bool releasedThisFrame)
        {
            StartedThisFrame = startedThisFrame;
            IsHeld = isHeld;
            ReleasedThisFrame = releasedThisFrame;
        }

        public bool StartedThisFrame { get; }
        public bool IsHeld { get; }
        public bool ReleasedThisFrame { get; }

        public static SemanticButtonState None => default;
    }
}
