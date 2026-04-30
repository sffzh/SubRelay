using System.IO;

namespace GameSubRelay.App.Logging;

public static class AppLogPaths
{
    public static string DefaultLogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SubRelay",
        "logs");

    public static string DefaultLogFilePath { get; } = Path.Combine(
        DefaultLogDirectory,
        $"SubRelay-{DateTime.Now:yyyyMMdd}.log");
}
