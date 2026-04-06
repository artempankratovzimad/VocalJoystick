using System;
using System.IO;
using VocalJoystick.Core.Interfaces;

namespace VocalJoystick.Infrastructure.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly object _sync = new();

    public FileLogger(IAppStorageLocation storageLocation)
    {
        var fileName = $"log-{DateTime.UtcNow:yyyyMMdd}.txt";
        var folder = storageLocation.LogsFolder;
        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, fileName);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var payload = exception is null ? message : $"{message}: {exception}";
        Write("ERROR", payload);
    }

    public void LogInfo(string message) => Write("INFO", message);

    public void LogDebug(string message) => Write("DEBUG", message);

    public void LogWarning(string message) => Write("WARN", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        lock (_sync)
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
    }
}
