using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;

namespace PizzaFactory.Factory;

/// <summary>A human-decision event raised when the factory needs attention (e.g. a topping running out).</summary>
public sealed record Escalation(Ingredient Ingredient, int Grams, string Message, DateTimeOffset At);

/// <summary>
/// Where escalations go. Default sink logs; later sinks push to the Window dashboard (SignalR) and
/// to Teams/Copilot (M365). Abstracted so the crisis logic doesn't care about the channel.
/// </summary>
public interface IEscalationSink
{
    Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default);
}

/// <summary>Default escalation sink — logs a warning. Swapped/composed with Window + Teams sinks later.</summary>
public sealed class LoggingEscalationSink(ILogger<LoggingEscalationSink> logger) : IEscalationSink
{
    public Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("ESCALATION: {Message} ({Ingredient} at {Grams}g)",
            escalation.Message, escalation.Ingredient, escalation.Grams);
        return Task.CompletedTask;
    }
}
