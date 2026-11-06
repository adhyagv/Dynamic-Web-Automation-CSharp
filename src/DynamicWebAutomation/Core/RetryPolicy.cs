using OpenQA.Selenium;

namespace DynamicWebAutomation.Core;

/// <summary>
/// Retries only stale-element operations, because a replaced DOM node can be
/// safely re-detected while unrelated errors must remain visible.
/// </summary>
public static class RetryPolicy
{
    public static T Execute<T>(Func<T> operation, int retryCount)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (StaleElementReferenceException exception) when (attempt < retryCount)
            {
                Console.WriteLine($"Stale element; re-detecting before retry {attempt + 1}/{retryCount}: {exception.Message}");
            }
        }
    }
}
