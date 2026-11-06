using System.Text.Json;

namespace DynamicWebAutomation.Configuration;

/// <summary>
/// Loads and validates settings before Chrome starts, so configuration errors
/// are reported without appearing as test failures.
/// </summary>
public sealed class AutomationSettings
{
    private const string TargetUrlEnvironmentVariable = "CLOUDQA_TARGET_URL";

    public string TargetUrl { get; set; } = "";
    public FieldSettings Fields { get; set; } = new();
    public TimeoutSettings Timeouts { get; set; } = new();
    public ScoringSettings Scoring { get; set; } = new();
    public int RetryCount { get; set; } = 2;
    public bool Headless { get; set; }
    public bool FailWhenStateOptionMissing { get; set; }
    public int PauseSeconds { get; private set; }
    public string? ResultsJsonPath { get; private set; }

    /// <summary>
    /// Resolves URL precedence and command-line browser options, then validates
    /// values that would otherwise make the run behave unpredictably.
    /// </summary>
    public static AutomationSettings Load(string[] args)
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            throw new ConfigurationException($"Settings file was not found: {settingsPath}");
        }

        AutomationSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<AutomationSettings>(
                File.ReadAllText(settingsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ConfigurationException("appsettings.json did not contain settings.");
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException($"Could not read appsettings.json: {exception.Message}");
        }
        catch (IOException exception)
        {
            throw new ConfigurationException($"Could not access appsettings.json: {exception.Message}");
        }

        string? argumentUrl = null;
        bool headedOverride = false;
        bool headlessOverride = false;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--url":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ConfigurationException("--url requires a URL value.");
                    }

                    argumentUrl = args[++index];
                    break;
                case "--headed":
                    headedOverride = true;
                    break;
                case "--headless":
                    headlessOverride = true;
                    break;
                case "--pause":
                    settings.PauseSeconds = 3;
                    if (index + 1 < args.Length && int.TryParse(args[index + 1], out int pause))
                    {
                        settings.PauseSeconds = pause;
                        index++;
                    }

                    break;
                case "--results-json":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ConfigurationException("--results-json requires a file path.");
                    }

                    settings.ResultsJsonPath = args[++index];
                    break;
                default:
                    throw new ConfigurationException($"Unknown argument: {args[index]}");
            }
        }

        if (headedOverride && headlessOverride)
        {
            throw new ConfigurationException("Choose only one of --headed or --headless.");
        }

        if (headlessOverride)
        {
            settings.Headless = true;
        }
        else if (headedOverride)
        {
            settings.Headless = false;
        }

        if (settings.PauseSeconds < 0)
        {
            throw new ConfigurationException("--pause seconds cannot be negative.");
        }

        string? environmentUrl = Environment.GetEnvironmentVariable(TargetUrlEnvironmentVariable);
        settings.TargetUrl = !string.IsNullOrWhiteSpace(argumentUrl)
            ? argumentUrl
            : !string.IsNullOrWhiteSpace(environmentUrl)
                ? environmentUrl
                : settings.TargetUrl;

        Validate(settings);
        return settings;
    }

    private static void Validate(AutomationSettings settings)
    {
        if (!Uri.TryCreate(settings.TargetUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeFile))
        {
            throw new ConfigurationException(
                "TargetUrl must be an absolute http, https, or file:// URL.");
        }

        if (string.IsNullOrWhiteSpace(settings.Fields.FirstName) ||
            string.IsNullOrWhiteSpace(settings.Fields.Gender) ||
            string.IsNullOrWhiteSpace(settings.Fields.State))
        {
            throw new ConfigurationException("Fields.FirstName, Fields.Gender, and Fields.State must be set.");
        }

        if (settings.Timeouts.PageLoadSeconds <= 0 ||
            settings.Timeouts.ElementSeconds <= 0 ||
            settings.Timeouts.OptionsSeconds <= 0)
        {
            throw new ConfigurationException("All timeout values must be greater than zero.");
        }

        if (settings.RetryCount < 0)
        {
            throw new ConfigurationException("RetryCount cannot be negative.");
        }

        if (settings.Scoring.MinimumScore is < 0 or > 100 ||
            settings.Scoring.MinimumLead is < 0 or > 100)
        {
            throw new ConfigurationException("Scoring thresholds must be between 0 and 100.");
        }
    }
}

/// <summary>
/// Holds the expected values for the three fields this run may touch.
/// </summary>
public sealed class FieldSettings
{
    public string FirstName { get; set; } = "Adhya";
    public string Gender { get; set; } = "Female";
    public string State { get; set; } = "Karnataka";
}

/// <summary>
/// Keeps browser waits configurable in seconds for easy local adjustments.
/// </summary>
public sealed class TimeoutSettings
{
    public int PageLoadSeconds { get; set; } = 30;
    public int ElementSeconds { get; set; } = 10;
    public int OptionsSeconds { get; set; } = 10;
}

/// <summary>
/// Sets the minimum evidence strength and separation required for a safe choice.
/// </summary>
public sealed class ScoringSettings
{
    public int MinimumScore { get; set; } = 60;
    public int MinimumLead { get; set; } = 15;
}

/// <summary>
/// Identifies invalid settings or command-line options.
/// </summary>
public sealed class ConfigurationException(string message) : Exception(message)
{
}
