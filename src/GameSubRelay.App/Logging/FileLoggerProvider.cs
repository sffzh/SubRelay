using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.App.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly string _filePath;
    private bool _disposed;

    public FileLoggerProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Log file path is required.", nameof(filePath));
        }

        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, WriteLine));
    }

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private void WriteLine(string line)
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Action<string> _writeLine;

        public FileLogger(string categoryName, Action<string> writeLine)
        {
            _categoryName = categoryName;
            _writeLine = writeLine;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {message}";
            _writeLine(line);

            if (exception is not null)
            {
                _writeLine(exception.ToString());
            }
        }
    }
}
