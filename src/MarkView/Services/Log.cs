using System.IO;

namespace MarkView.Services;

/// <summary>Minimal file logger: one file per day in %AppData%\MarkView\logs.</summary>
public static class Log
{
    private static readonly Lock Gate = new();

    public static void Error(Exception exception, string context) => Write("ERROR", $"{context}{Environment.NewLine}{exception}");

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDir);
                var file = Path.Combine(AppPaths.LogsDir, $"markview-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must never crash the app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
