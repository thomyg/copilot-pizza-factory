namespace PizzaFactory.Giuseppe.WorkContext;

/// <summary>
/// Seam over "look up a meeting in the user's workplace". Implementations: live Work IQ
/// (Microsoft 365 context, preview-labeled) or the rehearsal stand-in — the demo must never
/// die on stage, so a fallback implementation always exists.
/// </summary>
public interface IWorkContext
{
    /// <summary>Finds the best-matching upcoming meeting for a natural-language query, or null.</summary>
    Task<MeetingContext?> FindMeetingAsync(string query, CancellationToken cancellationToken = default);
}
