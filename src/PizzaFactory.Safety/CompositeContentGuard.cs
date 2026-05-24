namespace PizzaFactory.Safety;

/// <summary>
/// Runs guards in order and returns the first block — cheap local checks first (length, PII, obvious
/// injection), then the cloud guard for nuanced moderation. Defense in depth.
/// </summary>
public sealed class CompositeContentGuard(IReadOnlyList<IContentGuard> guards) : IContentGuard
{
    public async Task<GuardResult> InspectAsync(string? text, CancellationToken cancellationToken = default)
    {
        foreach (var guard in guards)
        {
            var result = await guard.InspectAsync(text, cancellationToken);
            if (!result.Allowed)
            {
                return result;
            }
        }

        return GuardResult.Allow();
    }
}
