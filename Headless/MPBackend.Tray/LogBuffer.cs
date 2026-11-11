using System.Collections.Concurrent;
using System.Reflection;
using log4net.Appender;
using log4net.Core;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Logging;

namespace MPBackend.Tray;

public sealed record LogLine(DateTime Time, string Level, string Source, string Message)
{
    public override string ToString() => $"{Time:HH:mm:ss} {Level,-5} {Source} - {Message}";
}

/// <summary>
/// Last N log lines for the status window, fed by the backend's log4net output, the ASP.NET Core logger and
/// the app itself. The backend's rolling file log is untouched.
/// </summary>
public static class LogBuffer
{
    public const int Capacity = 500;

    private static readonly ConcurrentQueue<LogLine> lines = new();

    public static event Action<LogLine>? Added;

    public static void Add(LogLine line)
    {
        lines.Enqueue(line);
        while (lines.Count > Capacity)
            lines.TryDequeue(out _);
        Added?.Invoke(line);
    }

    public static void Add(string level, string source, string message) =>
        Add(new LogLine(DateTime.Now, level, source, message));

    public static LogLine[] Snapshot() => lines.ToArray();

    internal static string ShortName(string? logger)
    {
        if (string.IsNullOrEmpty(logger))
            return "";
        var dot = logger.LastIndexOf('.');
        return dot < 0 ? logger : logger.Substring(dot + 1);
    }
}

/// <summary>log4net appender added to the root logger after the backend configured logging.</summary>
public sealed class BufferAppender : AppenderSkeleton
{
    private const string AppenderName = "TrayBuffer";

    public static void Attach()
    {
        var hierarchy = (Hierarchy)log4net.LogManager.GetRepository(Assembly.GetEntryAssembly() ?? typeof(BufferAppender).Assembly);
        if (hierarchy.Root.GetAppender(AppenderName) != null)
            return;
        var appender = new BufferAppender { Name = AppenderName };
        appender.ActivateOptions();
        hierarchy.Root.AddAppender(appender);
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
        var message = loggingEvent.RenderedMessage ?? "";
        if (loggingEvent.ExceptionObject != null)
            message += " - " + loggingEvent.ExceptionObject.Message;
        LogBuffer.Add(new LogLine(loggingEvent.TimeStamp, loggingEvent.Level?.Name ?? "INFO",
            LogBuffer.ShortName(loggingEvent.LoggerName), message));
    }
}

/// <summary>Microsoft.Extensions.Logging provider for the Kestrel / ASP.NET Core side.</summary>
public sealed class BufferLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BufferLogger(LogBuffer.ShortName(categoryName));

    public void Dispose()
    {
    }

    private sealed class BufferLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var message = formatter(state, exception);
            if (exception != null)
                message += " - " + exception.Message;
            LogBuffer.Add(new LogLine(DateTime.Now, Level(logLevel), category, message));
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace or LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => level.ToString().ToUpperInvariant()
        };
    }
}
