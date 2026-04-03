using System;

namespace VocalJoystick.Core.Interfaces;

public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}
