using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MPBackend.App;

/// <summary>
/// Minimal file logger for the app side (Blazor render errors, MAUI, dev aids). The backend itself logs through
/// log4net to <c>logs/mpbackend.log</c> in the same directory.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string path;
    private readonly object sync = new();
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new();

    public FileLoggerProvider(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public ILogger CreateLogger(string categoryName) => loggers.GetOrAdd(categoryName, c => new FileLogger(this, c));

    private void Write(string line)
    {
        lock (sync)
            File.AppendAllText(path, line + Environment.NewLine);
    }

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider provider;
        private readonly string category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            this.provider = provider;
            this.category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {logLevel,-11} {category} - {formatter(state, exception)}";
            if (exception != null)
                line += Environment.NewLine + exception;
            provider.Write(line);
        }
    }
}
