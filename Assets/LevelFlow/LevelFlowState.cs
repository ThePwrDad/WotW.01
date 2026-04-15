public static class LevelFlowState
{
    public static bool IsPauseBlocked { get; private set; }

    public static void SetPauseBlocked(bool blocked)
    {
        IsPauseBlocked = blocked;
    }
}
