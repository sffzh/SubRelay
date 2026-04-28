namespace GameSubRelay.Core.Configuration;

public sealed record AppSettingsValidationIssue(string Path, string Message);

public sealed record AppSettingsValidationResult(IReadOnlyList<AppSettingsValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class AppSettingsValidationException : InvalidOperationException
{
    public AppSettingsValidationException(AppSettingsValidationResult validationResult)
        : base("App settings are invalid.")
    {
        ValidationResult = validationResult;
    }

    public AppSettingsValidationResult ValidationResult { get; }
}
