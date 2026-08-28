using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RequestSpark.Web.Models.ViewModels;

public class OpenApiUploadViewModel
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Comma-separated user tags</summary>
    public string TagsString { get; set; } = string.Empty;

    /// <summary>Override the default base URL extracted from the spec</summary>
    public string? DefaultBaseUrl { get; set; }

    [Required(ErrorMessage = "Please select an OpenAPI JSON file")]
    public IFormFile? SpecFile { get; set; }

    public List<string> GetTags() =>
        TagsString?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        ?? new List<string>();
}

public class OpenApiDetailsViewModel
{
    public OpenApiSpec Spec { get; set; } = new();
    public OpenApiStructure Structure { get; set; } = new();
}

/// <summary>Request body sent by the Test runner UI to execute a single endpoint</summary>
public class OpenApiEndpointExecuteRequest
{
    [Description("Base URL selected for the stored OpenAPI specification.")]
    [Required]
    [Url]
    [DefaultValue("https://api.example.com")]
    public string BaseUrl { get; set; } = string.Empty;

    [Description("OpenAPI path template to execute.")]
    [Required]
    [DefaultValue("/api/status")]
    public string Path { get; set; } = string.Empty;

    [Description("HTTP method to execute.")]
    [Required]
    [RegularExpression("GET|POST|PUT|PATCH|DELETE", ErrorMessage = "Use GET, POST, PUT, PATCH, or DELETE.")]
    [DefaultValue("GET")]
    public string Method { get; set; } = "GET";

    /// <summary>Path parameters already substituted into Path — kept separately for logging</summary>
    [Description("Path parameter values keyed by OpenAPI parameter name.")]
    public Dictionary<string, string> PathParams { get; set; } = new();

    [Description("Query string values keyed by OpenAPI parameter name.")]
    public Dictionary<string, string> QueryParams { get; set; } = new();

    [Description("Request headers to send with the outbound test call.")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [Description("Optional JSON body to send for methods that accept content.")]
    public string? Body { get; set; }

    // Auth
    [Description("Authentication mode for this request: none, bearer, apikey, or basic.")]
    [DefaultValue("none")]
    [RegularExpression("none|bearer|apikey|basic", ErrorMessage = "Use none, bearer, apikey, or basic.")]
    public string AuthType { get; set; } = "none"; // none | bearer | apikey | basic

    [Description("Bearer token value when authType is bearer. Leave blank in saved examples.")]
    public string? BearerToken { get; set; }

    [Description("Header name when authType is apikey.")]
    [DefaultValue("X-API-Key")]
    public string? ApiKeyHeader { get; set; }

    [Description("API key value when authType is apikey. Leave blank in saved examples.")]
    public string? ApiKeyValue { get; set; }

    [Description("Basic authentication username when authType is basic.")]
    public string? BasicUsername { get; set; }

    [Description("Basic authentication password when authType is basic. Leave blank in saved examples.")]
    public string? BasicPassword { get; set; }
}

/// <summary>Response returned from the execute API</summary>
public class OpenApiEndpointExecuteResult
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
/// Parsed endpoint details for a stored OpenAPI specification.
/// </summary>
public class OpenApiEndpointsResponse
{
    /// <summary>Available server URLs from the stored specification.</summary>
    [Description("Available server URLs from the stored OpenAPI specification.")]
    public List<string> Servers { get; set; } = new();

    /// <summary>Security schemes declared by the stored specification.</summary>
    [Description("Security schemes declared by the stored OpenAPI specification.")]
    public Dictionary<string, string> SecuritySchemes { get; set; } = new();

    /// <summary>Parsed endpoints from the stored specification.</summary>
    [Description("Parsed endpoints from the stored OpenAPI specification.")]
    public List<OpenApiEndpointInfo> Endpoints { get; set; } = new();
}

