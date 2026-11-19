namespace AmlGuard.Plugins.Domain
{
    // Dataverse increases the execution depth every time a plug-in triggers another operation, and stops
    // a chain at depth 8. This guard stops earlier. Depth 1 is the user's own action and depth 2 is
    // automation triggered by it (for example a flow that tidies names), both of which must still be
    // screened. A deeper chain is almost certainly a loop, so the plug-in steps aside.
    public static class ExecutionDepthGuard
    {
        public const int MaxDepth = 2;

        public static bool IsAllowed(int depth)
        {
            return depth <= MaxDepth;
        }
    }
}
