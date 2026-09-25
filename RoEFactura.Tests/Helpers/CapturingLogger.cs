using Microsoft.Extensions.Logging;

namespace RoEFactura.Tests.Helpers;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> that records every formatted message and exception text, for tests
/// that assert specific content (XML bodies, tokens, PII) is never logged. This is WP-C's own copy for
/// <c>RoEFactura.Tests.Services</c> -- WP-A owns a separate <c>CapturingLogger</c> under
/// <c>RoEFactura.Tests/Services/Api/</c> for its own HTTP client tests; they are intentionally not shared.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(formatter(state, exception));
        if (exception != null)
            _entries.Add(exception.ToString());
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
