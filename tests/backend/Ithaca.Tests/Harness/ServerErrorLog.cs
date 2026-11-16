using System.Collections.Concurrent;
using Ithaca.Api.Errors;
using Microsoft.Extensions.Logging;

namespace Ithaca.Tests.Harness;

/// <summary>
/// Keeps every exception that reached <see cref="ProblemDetailsExceptionHandler"/> (the cause behind a 500), so a test can
/// put it in its failure message instead of just "500". Other categories are ignored: EF logs every handled write conflict
/// at Error too, which would bury the one that matters.
/// </summary>
public sealed class ServerErrorLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _errors = new();

    public IReadOnlyCollection<string> Errors => _errors;

    /// <summary>The logged errors as one block for an assertion message; empty when there are none.</summary>
    public string Describe() => _errors.IsEmpty ? string.Empty : "\nServer errors:\n" + string.Join("\n---\n", _errors);

    private static readonly string HandlerCategory = typeof(ProblemDetailsExceptionHandler).FullName!;

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(ServerErrorLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error && category == HandlerCategory;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) && exception is not null)
            {
                log._errors.Enqueue($"{category}: {formatter(state, exception)}\n{exception}");
            }
        }
    }
}
