namespace PizzaFactory.Giuseppe.WorkContext;

/// <summary>
/// The slice of workplace context Giuseppe needs to cater a meeting: when it is, who's coming,
/// and anything the kitchen should know. <see cref="Source"/> says where it came from
/// ("work-iq" = live Microsoft 365 context, "rehearsal" = deterministic demo data) so the
/// demo can show its provenance honestly.
/// </summary>
public sealed record MeetingContext(
    string Subject,
    DateTimeOffset Start,
    IReadOnlyList<string> Attendees,
    string? DietaryNotes,
    string Source);
