using DynamicWebAutomation.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace DynamicWebAutomation.Core;

/// <summary>
/// Starts Chrome with Selenium Manager, avoiding a manually pinned driver path.
/// </summary>
public static class BrowserFactory
{
    public static IWebDriver Create(AutomationSettings settings)
    {
        ChromeOptions options = new();
        if (settings.Headless)
        {
            options.AddArgument("--headless=new");
        }

        options.AddArgument("--window-size=1280,900");
        IWebDriver driver = new ChromeDriver(options);
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(settings.Timeouts.PageLoadSeconds);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        return driver;
    }
}
