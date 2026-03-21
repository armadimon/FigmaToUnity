namespace UnityToFigma.Editor.Utils
{
    /// <summary>
    /// Fixed log lines for token-related flows. Never append, interpolate, or format in a secret value.
    /// </summary>
    public static class FigmaTokenLogMessages
    {
        public const string PersonalAccessTokenSavedToPreferences =
            "UnityToFigma: Figma Personal Access Token was saved to Editor preferences (value not logged).";

        /// <summary>Returns a confirmation string that never includes the token.</summary>
        public static string GetPersonalAccessTokenSavedMessage() => PersonalAccessTokenSavedToPreferences;
    }
}
