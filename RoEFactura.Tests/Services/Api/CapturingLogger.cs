using Microsoft.Extensions.Logging;

namespace RoEFactura.Tests.Services.Api;

/// <summary>
/// An <see cref="ILogger{T}"/> that stores every formatted message (plus exception text, if any) in
/// memory instead of writing anywhere. Used to assert on logging hygiene (no PII, no bodies).
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        if (exception != null)
            message += " | " + exception;

        Messages.Add(message);
    }
}
