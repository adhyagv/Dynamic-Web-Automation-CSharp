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
        AutomationSettings settings;
        try
        {
            settings = AutomationSettings.Load(args);
        }
        catch (ConfigurationException exception)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            return 2;
        }

        IWebDriver? driver = null;
        try
        {
            Console.WriteLine($"Target URL: {settings.TargetUrl}");
            Console.WriteLine($"Chrome mode: {(settings.Headless ? "headless" : "headed")}");
            driver = BrowserFactory.Create(settings);
            driver.Navigate().GoToUrl(settings.TargetUrl);

            ReportWriter reportWriter = new();
            PracticeFormAutomation automation = new(driver, settings, reportWriter);
            IReadOnlyList<FieldResult> results = automation.Run();
            RunSummary summary = new(results, settings.FailWhenStateOptionMissing);
            reportWriter.WriteHtmlReport(settings.TargetUrl, results, summary);
            ConsoleReporter.PrintSummary(summary);

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
            return 1;
        }
        finally
        {
            driver?.Quit();
        }
    }
}
