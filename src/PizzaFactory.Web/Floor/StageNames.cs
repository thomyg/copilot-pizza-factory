using PizzaFactory.Safety;

namespace PizzaFactory.Web.Floor;

/// <summary>
/// Turns the name a guest gave Giuseppe "for the big screen" into something safe to project.
/// The audience types it, so it goes through the same bouncer as every chat message, keeps to
/// plain name characters, and is cut at a word so it fits a banner. Anything refused becomes
/// "a guest": the order still lands on stage, just without a name.
/// </summary>
public sealed class StageNames(IContentGuard guard)
{
    public const int MaxLength = 20;
    public const string Fallback = "a guest";

    public async Task<string> ForStageAsync(string? raw, CancellationToken cancellationToken = default)
    {
        var name = string.Join(' ', (raw ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length == 0
            || !name.All(c => char.IsLetter(c) || c is ' ' or '-' or '\'' or '.' or '&')
            || !(await guard.InspectAsync(name, cancellationToken)).Allowed)
        {
            return Fallback;
        }

        if (name.Length <= MaxLength)
        {
            return name;
        }

        // keep whole words: "Maximiliane Konstantina von …" becomes "Maximiliane"
        var kept = "";
        foreach (var word in name.Split(' '))
        {
            var next = kept.Length == 0 ? word : $"{kept} {word}";
            if (next.Length > MaxLength)
            {
                break;
            }

            kept = next;
        }

        return kept.Length > 0 ? kept : name[..MaxLength];
    }
}
