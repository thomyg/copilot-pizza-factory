using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.AI.ContentSafety;
using Azure.Core;

namespace PizzaFactory.Safety.ContentSafety;

/// <summary>
/// Cloud content guard backed by Azure AI Content Safety (moderation categories) plus Prompt Shields
/// (jailbreak / injection detection). Key-less auth via a <see cref="TokenCredential"/>.
/// Fails closed: any API error blocks rather than letting unverified content through.
/// </summary>
public sealed class AzureContentSafetyGuard : IContentGuard
{
    private static readonly HttpClient Http = new();
    private readonly ContentSafetyClient _client;
    private readonly TokenCredential _credential;
    private readonly AzureContentSafetyOptions _options;

    public AzureContentSafetyGuard(AzureContentSafetyOptions options, TokenCredential credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        _options = options;
        _credential = credential;
        _client = new ContentSafetyClient(new Uri(options.Endpoint), credential);
    }

    public async Task<GuardResult> InspectAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return GuardResult.Allow();
        }

        try
        {
            if (await IsPromptAttackAsync(text, cancellationToken))
            {
                return GuardResult.Block(GuardCategory.PromptInjection, "Prompt Shields detected an attack.");
            }

            var response = await _client.AnalyzeTextAsync(new AnalyzeTextOptions(text), cancellationToken);
            foreach (var category in response.Value.CategoriesAnalysis)
            {
                if ((category.Severity ?? 0) >= _options.SeverityThreshold)
                {
                    return GuardResult.Block(
                        GuardCategory.Offensive, $"Content Safety: {category.Category} severity {category.Severity}.");
                }
            }

            return GuardResult.Allow();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail closed: if we can't verify safety, don't let it through.
            return GuardResult.Block(GuardCategory.None, "Content Safety unavailable — blocked (fail-closed).");
        }
    }

    private async Task<bool> IsPromptAttackAsync(string text, CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Endpoint.TrimEnd('/')}/contentsafety/text:shieldPrompt?api-version={_options.ApiVersion}");
        request.Headers.Authorization = new("Bearer", token.Token);
        request.Content = JsonContent.Create(new ShieldPromptRequest(text, []));

        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ShieldPromptResponse>(cancellationToken);
        return result?.UserPromptAnalysis?.AttackDetected ?? false;
    }

    private sealed record ShieldPromptRequest(
        [property: JsonPropertyName("userPrompt")] string UserPrompt,
        [property: JsonPropertyName("documents")] string[] Documents);

    private sealed record ShieldPromptResponse(
        [property: JsonPropertyName("userPromptAnalysis")] PromptAnalysis? UserPromptAnalysis);

    private sealed record PromptAnalysis(
        [property: JsonPropertyName("attackDetected")] bool AttackDetected);
}
