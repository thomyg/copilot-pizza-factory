using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace PizzaFactory.Giuseppe.Tools;

/// <summary>How to reach an MCP server and (optionally) authenticate against it.</summary>
public sealed record McpToolSourceOptions
{
    /// <summary>Display name used in logs and the MCP handshake, e.g. "factory" or "work-iq".</summary>
    public required string Name { get; init; }

    /// <summary>HTTP endpoint of a remote MCP server (Streamable HTTP). Mutually exclusive with <see cref="Command"/>.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>Local command to spawn as a stdio MCP server (e.g. the `workiq` CLI). Wins over <see cref="Endpoint"/> when set.</summary>
    public string? Command { get; init; }

    /// <summary>Arguments for <see cref="Command"/>, e.g. ["mcp"].</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Optional bearer-token factory for HTTP servers behind Entra (key-less; e.g. DefaultAzureCredential).</summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; init; }
}

/// <summary>
/// Connects to an MCP server (our factory, or Microsoft's Work IQ) and surfaces its tools as
/// <see cref="AITool"/>s for Giuseppe's function-calling loop. Connection is lazy and cached;
/// any failure logs a warning and degrades to the optional fallback source (or no tools) —
/// a dead integration must never kill the conversation, especially not on stage.
/// </summary>
public sealed class McpToolSource(
    McpToolSourceOptions options,
    IGiuseppeToolSource? fallback = null,
    ILogger<McpToolSource>? logger = null) : IGiuseppeToolSource, IAsyncDisposable
{
    private readonly ILogger _logger = logger ?? NullLogger<McpToolSource>.Instance;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await GetOrConnectAsync(cancellationToken);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return [.. tools];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MCP tool source '{Source}' unavailable — degrading gracefully", options.Name);
            await ResetAsync();

            return fallback is null
                ? []
                : await fallback.GetToolsAsync(cancellationToken);
        }
    }

    private async Task<McpClient> GetOrConnectAsync(CancellationToken cancellationToken)
    {
        if (_client is { } connected)
        {
            return connected;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _client ??= await McpClient.CreateAsync(
                await CreateTransportAsync(cancellationToken),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IClientTransport> CreateTransportAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Command))
        {
            return new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = options.Name,
                Command = options.Command,
                Arguments = [.. options.Arguments],
            });
        }

        if (options.Endpoint is null)
        {
            throw new InvalidOperationException(
                $"MCP tool source '{options.Name}' has neither an Endpoint nor a Command configured.");
        }

        Dictionary<string, string>? headers = null;
        if (options.BearerTokenProvider is { } tokenProvider &&
            await tokenProvider(cancellationToken) is { Length: > 0 } token)
        {
            headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
        }

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = options.Name,
            Endpoint = options.Endpoint,
            AdditionalHeaders = headers,
        });
    }

    private async ValueTask ResetAsync()
    {
        var stale = Interlocked.Exchange(ref _client, null);
        if (stale is not null)
        {
            try
            {
                await stale.DisposeAsync();
            }
            catch
            {
                // Disposing an already-broken session may throw; the session is gone either way.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _gate.Dispose();
    }
}
