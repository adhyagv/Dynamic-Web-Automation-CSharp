using System.Net;
using System.Text;
using DynamicWebAutomation.Models;
using OpenQA.Selenium;

namespace DynamicWebAutomation.Reporting;

/// <summary>
/// Captures a screenshot for failed/ambiguous fields and writes the evidence
/// into one readable HTML report.
/// </summary>
public sealed class ReportWriter
{
    private readonly string reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "reports");

    public void CaptureFailureScreenshot(IWebDriver driver, FieldResult result)
    {
        Directory.CreateDirectory(reportsDirectory);
        string safeName = string.Concat(result.FieldName.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        string path = Path.Combine(reportsDirectory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{safeName}.png");

        try
        {
            ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(path);
            result.ScreenshotPath = path;
            Console.WriteLine($"Screenshot saved: {path}");
        }
        catch (WebDriverException exception)
        {
            result.ScreenshotError = exception.Message;
            Console.Error.WriteLine($"Screenshot ERROR for {result.FieldName}: {exception.Message}");
        }
    }

    public string WriteHtmlReport(string targetUrl, IReadOnlyList<FieldResult> results, RunSummary summary)
    {
        Directory.CreateDirectory(reportsDirectory);
        string path = Path.Combine(reportsDirectory, $"report-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.html");
        StringBuilder html = new();
        html.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>CloudQA automation report</title>");
        html.AppendLine("<style>body{font:15px Arial,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#222}table{width:100%;border-collapse:collapse}th,td{border:1px solid #bbb;padding:.6rem;text-align:left;vertical-align:top}th{background:#eee}.PASS{color:#176b2c}.FAIL,.AMBIGUOUS{color:#a12020}.SKIPPED_OPTION_NOT_AVAILABLE{color:#775b00}code{overflow-wrap:anywhere}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<h1>CloudQA automation report</h1>");
        html.AppendLine($"<p><strong>Target:</strong> {Encode(targetUrl)}</p>");
        html.AppendLine($"<p><strong>Outcome:</strong> {Encode(summary.FinalMessage)}</p>");
        html.AppendLine("<table><thead><tr><th>Field</th><th>Status and message</th><th>Chosen/top element and score</th><th>Runner-up score</th><th>Reason</th><th>Screenshot</th></tr></thead><tbody>");
        foreach (FieldResult result in results)
        {
            string status = result.Status switch
            {
                FieldStatus.Pass => "PASS",
                FieldStatus.Fail => "FAIL",
                FieldStatus.Ambiguous => "AMBIGUOUS",
                FieldStatus.SkippedOptionNotAvailable => "SKIPPED_OPTION_NOT_AVAILABLE",
                _ => result.Status.ToString()
            };
            string element = result.ElementDescription is null
                ? "No candidate selected"
                : $"{result.ElementDescription} (score {result.ChosenScore?.ToString() ?? "n/a"})";
            string screenshot = result.ScreenshotPath is not null
                ? $"<a href=\"{Encode(Path.GetFileName(result.ScreenshotPath))}\">View screenshot</a>"
                : Encode(result.ScreenshotError ?? "Not required");

            html.Append("<tr>");
            html.Append($"<td>{Encode(result.FieldName)}</td>");
            html.Append($"<td class=\"{Encode(status)}\">{Encode(result.Message)}");
            if (result.AvailableOptions.Count > 0)
            {
                html.Append($"<br><strong>Available options:</strong> {Encode(string.Join(", ", result.AvailableOptions))}");
            }

            html.Append("</td>");
            html.Append($"<td><code>{Encode(element)}</code></td>");
            html.Append($"<td>{result.RunnerUpScore?.ToString() ?? "n/a"}</td>");
            html.Append($"<td>{Encode(result.ChoiceReason ?? "No choice made.")}</td>");
            html.Append($"<td>{screenshot}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table></body></html>");
        File.WriteAllText(path, html.ToString(), Encoding.UTF8);
        Console.WriteLine($"HTML report saved: {path}");
        return path;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
