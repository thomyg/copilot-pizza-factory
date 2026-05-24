using System.Text.RegularExpressions;

namespace PizzaFactory.Safety;

/// <summary>
/// Offline, dependency-free content guard for local dev and tests — the "Bouncer".
/// Catches obvious abuse: profanity, prompt-injection attempts, and personal data.
/// Deliberately conservative; the production guard is <c>AzureContentSafetyGuard</c>
/// (Azure AI Content Safety + Prompt Shields), which plugs in behind <see cref="IContentGuard"/>.
/// </summary>
public sealed partial class HeuristicContentGuard : IContentGuard
{
    private const int MaxLength = 280;

    // Illustrative profanity list — Azure AI Content Safety does the real hate/sexual/violence/self-harm work.
    private static readonly string[] Profanity =
        ["fuck", "shit", "asshole", "bitch", "bastard"];

    // Common prompt-injection / jailbreak phrasings.
    private static readonly string[] InjectionPhrases =
    [
        "ignore previous", "ignore all previous", "disregard previous",
        "ignore your instructions", "disregard your instructions", "forget your instructions",
        "system prompt", "reveal your instructions", "developer mode", "jailbreak",
        "you are now", "pretend you are", "act as if", "override your",
    ];

    public Task<GuardResult> InspectAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(GuardResult.Allow());
        }

        if (text.Length > MaxLength)
        {
            return Task.FromResult(
                GuardResult.Block(GuardCategory.PromptInjection, $"Input exceeds {MaxLength} characters."));
        }

        var lower = text.ToLowerInvariant();

        foreach (var phrase in InjectionPhrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                return Task.FromResult(
                    GuardResult.Block(GuardCategory.PromptInjection, "Looks like a prompt-injection attempt."));
            }
        }

        foreach (var word in Profanity)
        {
            if (Regex.IsMatch(lower, $@"\b{Regex.Escape(word)}\b"))
            {
                return Task.FromResult(
                    GuardResult.Block(GuardCategory.Offensive, "Contains offensive language."));
            }
        }

        if (EmailPattern().IsMatch(text) || PhonePattern().IsMatch(text))
        {
            return Task.FromResult(
                GuardResult.Block(GuardCategory.PersonalData, "Looks like it contains personal data."));
        }

        return Task.FromResult(GuardResult.Allow());
    }

    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b\+?\d[\d\s\-]{7,}\d\b")]
    private static partial Regex PhonePattern();
}
