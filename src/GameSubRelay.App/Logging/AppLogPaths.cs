using System.IO;

namespace GameSubRelay.App.Logging;

public static class AppLogPaths
{
    public static string DefaultLogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameSubRelay",
        "logs");

    public static string DefaultLogFilePath { get; } = Path.Combine(
        DefaultLogDirectory,
        $"GameSubRelay-{DateTime.Now:yyyyMMdd}.log");
}
