namespace Bonvi.Application.Processes.Commands;

/// <summary>What the process writes accept as free text.</summary>
internal static class ProcessFreeText
{
    /// <summary>
    /// PostgreSQL stores no NUL character, neither in <c>text</c> (22021) nor in <c>jsonb</c> (22P05). <c>"a\u0000b"</c>
    /// is valid JSON, so unchecked it passes every length rule and comes out of the save as a 500.
    /// </summary>
    public static bool HasNul(string? text) => text?.Contains('\0', StringComparison.Ordinal) == true;
}
