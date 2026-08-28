using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RequestSpark.Web.Models;

/// <summary>
/// Request to start a new execution.
/// </summary>
public class StartExecutionRequest
{
    /// <summary>
    /// Configuration ID to execute.
    /// </summary>
    [Description("Identifier of the saved RequestSpark configuration to execute.")]
    [Required]
    [MinLength(1)]
    [DefaultValue("sample-configuration-id")]
    public string ConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// User who initiated the execution.
    /// </summary>
    [Description("Display name of the user or system that started the execution.")]
    [DefaultValue("API")]
    [MaxLength(120)]
    public string? ExecutedBy { get; set; }
}

/// <summary>
/// Request to cancel an execution.
/// </summary>
public class CancelExecutionRequest
{
    /// <summary>
    /// User who cancelled the execution.
    /// </summary>
    [Description("Display name of the user or system that cancelled the execution.")]
    [DefaultValue("API")]
    [MaxLength(120)]
    public string? CancelledBy { get; set; }
}

/// <summary>
/// Response returned after a cancellation request is accepted.
/// </summary>
public class CancelExecutionResponse
{
    /// <summary>
    /// Human-readable cancellation result.
    /// </summary>
    [Description("Human-readable cancellation result.")]
    [DefaultValue("Execution cancelled successfully")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Cancelled execution identifier.
    /// </summary>
    [Description("Identifier of the execution that was cancelled.")]
    [DefaultValue("execution-id")]
    public string ExecutionId { get; set; } = string.Empty;
}

/// <summary>
/// Export result information.
/// </summary>
public class ExportResult
{
    /// <summary>
    /// Execution ID.
    /// </summary>
    [Description("Identifier of the execution whose results were exported.")]
    [DefaultValue("execution-id")]
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Export format.
    /// </summary>
    [Description("Export format used for the generated file.")]
    [DefaultValue("csv")]
    [RegularExpression("csv|json", ErrorMessage = "Supported export formats are csv and json.")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Full file path.
    /// </summary>
    [Description("Server-side file path for the exported results.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File name.
    /// </summary>
    [Description("Generated export file name.")]
    [DefaultValue("execution_results.csv")]
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for testing a single endpoint.
/// </summary>
public class SingleRequestTestRequest
{
    [Description("Base URL of the target API to test.")]
    [Required]
    [Url]
    [DefaultValue("https://ui.makeboldspark.com")]
    public string BaseUrl { get; set; } = string.Empty;

    [Description("Relative request path under the base URL.")]
    [Required]
    [DefaultValue("/api/status")]
    public string Path { get; set; } = string.Empty;

    [Description("HTTP method to send.")]
    [Required]
    [RegularExpression("GET|POST|PUT|PATCH|DELETE", ErrorMessage = "Use GET, POST, PUT, PATCH, or DELETE.")]
    [DefaultValue("GET")]
    public string Method { get; set; } = "GET";

    [Description("Optional JSON request body for methods that accept content.")]
    public string? Body { get; set; }

    [Description("Optional bearer token. Leave blank for unauthenticated sample calls.")]
    public string? BearerToken { get; set; }

    [Description("Optional request headers to send with the test request.")]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Result of a single test request.
/// </summary>
public class SingleRequestResult
{
    [Description("Whether the outbound request completed with a successful HTTP status code.")]
    public bool Success { get; set; }

    [Description("Numeric response status code. A value of 0 indicates a connection failure before an HTTP response was received.")]
    [Range(0, 599)]
    public int StatusCode { get; set; }

    [Description("Reason phrase or synthetic status text for the test result.")]
    public string StatusText { get; set; } = string.Empty;

    [Description("Raw response body returned by the target API.")]
    public string? ResponseBody { get; set; }

    [Description("Flattened response headers returned by the target API.")]
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    [Description("Elapsed request time in milliseconds.")]
    [Range(0, long.MaxValue)]
    public long ResponseTimeMs { get; set; }

    [Description("Fully resolved URL that was requested.")]
    public string RequestUrl { get; set; } = string.Empty;

    [Description("Error message when the request could not be completed.")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A single parsed row from the execution CSV results file.
/// CSV columns: Verb,Instance,LastRunDate,Duration,Request,ResultCode,SessionId,StatusDescription,Success,UserName,Content
/// </summary>
public class ExecutionResultRow
{
    [Description("HTTP method recorded for the executed request.")]
    public string Verb { get; set; } = string.Empty;

    [Description("Target instance name used for the request.")]
    public string Instance { get; set; } = string.Empty;

    [Description("Timestamp recorded in the CSV results file.")]
    public string LastRunDate { get; set; } = string.Empty;

    [Description("Request duration in milliseconds.")]
    [Range(0, long.MaxValue)]
    public long Duration { get; set; }

    [Description("Executed request path or URL.")]
    public string Request { get; set; } = string.Empty;

    [Description("HTTP result code recorded for the request.")]
    public string ResultCode { get; set; } = string.Empty;

    [Description("HTTP status description recorded for the request.")]
    public string StatusDescription { get; set; } = string.Empty;

    [Description("Whether this row represents a successful response.")]
    public bool Success { get; set; }

    [Description("User name associated with the request execution.")]
    public string UserName { get; set; } = string.Empty;

    public static ExecutionResultRow? ParseCsvLine(string line)
    {
        var parts = line.Split(',');
        if (parts.Length < 10) return null;

        return new ExecutionResultRow
        {
            Verb = parts[0].Trim(),
            Instance = parts[1].Trim(),
            LastRunDate = parts[2].Trim(),
            Duration = long.TryParse(parts[3].Trim(), out var duration) ? duration : 0,
            Request = parts[4].Trim(),
            ResultCode = parts[5].Trim(),
            StatusDescription = parts[7].Trim(),
            Success = bool.TryParse(parts[8].Trim(), out var success) && success,
            UserName = parts[9].Trim()
        };
    }
}
