namespace PizzaFactory.Safety;

/// <summary>Why a piece of input was blocked. Mirrors the categories a real moderator cares about.</summary>
public enum GuardCategory
{
    None,
    Offensive,
    PromptInjection,
    PersonalData,
}

/// <summary>The verdict of inspecting a piece of public input before it reaches Giuseppe or the screen.</summary>
public sealed record GuardResult(bool Allowed, GuardCategory Category, string Reason)
{
    public static GuardResult Allow() => new(true, GuardCategory.None, "ok");

    public static GuardResult Block(GuardCategory category, string reason) => new(false, category, reason);
}

/// <summary>
/// Inspects untrusted public input (an order, an overridden display name) before the factory acts on it.
/// Implementations: a local heuristic (offline, for dev/tests) and an Azure AI Content Safety + Prompt
/// Shields adapter (cloud). Callers depend only on this interface.
/// </summary>
public interface IContentGuard
{
    Task<GuardResult> InspectAsync(string? text, CancellationToken cancellationToken = default);
}
