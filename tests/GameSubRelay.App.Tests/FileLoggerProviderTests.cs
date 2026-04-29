using GameSubRelay.App.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameSubRelay.App.Tests;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void File_logger_writes_messages_to_configured_file()
    {
        var logFile = Path.Combine(Path.GetTempPath(), $"GameSubRelay-{Guid.NewGuid():N}.log");
        using var provider = new FileLoggerProvider(logFile);
        var logger = provider.CreateLogger("TestCategory");

        logger.LogInformation("hello {Value}", "world");

        var text = File.ReadAllText(logFile);
        Assert.Contains("TestCategory", text);
        Assert.Contains("hello world", text);
    }
}
