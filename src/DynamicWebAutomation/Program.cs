using DynamicWebAutomation.Automation;
using DynamicWebAutomation.Configuration;
using DynamicWebAutomation.Core;
using DynamicWebAutomation.Models;
using DynamicWebAutomation.Reporting;
using OpenQA.Selenium;

/// <summary>
/// Starts the configured browser run and always closes Chrome afterward.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        ReportWriter reportWriter = new();
        AutomationSettings settings;
        try
        {
            settings = AutomationSettings.Load(args);
        }
        catch (ConfigurationException exception)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            reportWriter.WriteJsonReport(
                ReadArgumentValue(args, "--url") ?? "",
                [],
                2,
                executionError: $"ConfigurationException: {exception.Message}",
                requestedPath: ReadArgumentValue(args, "--results-json"));
            return 2;
        }

        IWebDriver? driver = null;
        IReadOnlyList<FieldResult> results = [];
        try
        {
            Console.WriteLine($"Target URL: {settings.TargetUrl}");
            Console.WriteLine($"Chrome mode: {(settings.Headless ? "headless" : "headed")}");
            driver = BrowserFactory.Create(settings);
            driver.Navigate().GoToUrl(settings.TargetUrl);

            PracticeFormAutomation automation = new(driver, settings, reportWriter);
            results = automation.Run();
            RunSummary summary = new(results, settings.FailWhenStateOptionMissing);
            reportWriter.WriteHtmlReport(settings.TargetUrl, results, summary);
            ConsoleReporter.PrintSummary(summary);
            reportWriter.WriteJsonReport(
                settings.TargetUrl,
                results,
                summary.ExitCode,
                settings.FailWhenStateOptionMissing,
                requestedPath: settings.ResultsJsonPath);

            if (settings.PauseSeconds > 0)
            {
                Console.WriteLine($"Keeping Chrome open for {settings.PauseSeconds} second(s)...");
                Thread.Sleep(TimeSpan.FromSeconds(settings.PauseSeconds));
            }

            return summary.ExitCode;
        }
        catch (WebDriverException exception)
        {
            Console.Error.WriteLine($"Browser error: {exception.Message}");
            reportWriter.WriteJsonReport(
                settings.TargetUrl,
                results,
                1,
                settings.FailWhenStateOptionMissing,
                $"WebDriverException: {exception.Message}",
                settings.ResultsJsonPath);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected execution error: {exception.GetType().Name}: {exception.Message}");
            reportWriter.WriteJsonReport(
                settings.TargetUrl,
                results,
                1,
                settings.FailWhenStateOptionMissing,
                $"{exception.GetType().Name}: {exception.Message}",
                settings.ResultsJsonPath);
            return 1;
        }
        finally
        {
            try
            {
                driver?.Quit();
            }
            catch (WebDriverException exception)
            {
                Console.Error.WriteLine($"Chrome cleanup warning: {exception.Message}");
            }
        }
    }

    private static string? ReadArgumentValue(string[] args, string name)
    {
        for (int index = 0; index + 1 < args.Length; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
