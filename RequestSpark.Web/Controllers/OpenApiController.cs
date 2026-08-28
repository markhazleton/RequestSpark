using Microsoft.AspNetCore.Mvc;
using RequestSpark.Web.Models;
using RequestSpark.Web.Models.ViewModels;
using RequestSpark.Web.Services;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace RequestSpark.Web.Controllers;

/// <summary>
/// Controller for managing and testing OpenAPI / Swagger specifications.
/// </summary>
public class OpenApiController : Controller
{
    private readonly IOpenApiService _openApiService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OpenApiController"/>.
    /// </summary>
    /// <param name="openApiService">Service for OpenAPI specification management.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenApiController(
        IOpenApiService openApiService,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenApiController> logger)
    {
        _openApiService = openApiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── MVC: Index ────────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the list of stored OpenAPI specifications.
    /// </summary>
    /// <returns>The OpenAPI index view.</returns>
    public async Task<IActionResult> Index()
    {
        try
        {
            var specs = await _openApiService.GetAllAsync();
            return View(specs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OpenAPI specs");
            TempData["Error"] = "Failed to load OpenAPI specifications";
            return View(new List<OpenApiSpec>());
        }
    }

    // ── MVC: Upload ───────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the upload form for a new OpenAPI specification.
    /// </summary>
    /// <returns>The upload view.</returns>
    public IActionResult Upload() => View(new OpenApiUploadViewModel());

    /// <summary>
    /// Uploads and validates a new OpenAPI specification.
    /// </summary>
    /// <param name="viewModel">Upload form values.</param>
    /// <returns>The upload view or a redirect to details.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(OpenApiUploadViewModel viewModel)
    {
        if (!ModelState.IsValid) return View(viewModel);

        try
        {
            var file = viewModel.SpecFile!;

            if (file.Length == 0)
            {
                ModelState.AddModelError("SpecFile", "The selected file is empty");
                return View(viewModel);
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("SpecFile", "File size cannot exceed 10MB");
                return View(viewModel);
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("SpecFile", "Only JSON files are supported (YAML coming soon)");
                return View(viewModel);
            }

            var validation = await _openApiService.ValidateAsync(file);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors) ModelState.AddModelError(string.Empty, error);
                return View(viewModel);
            }

            foreach (var warning in validation.Warnings)
                TempData["Warning"] = warning;

            var metadata = new OpenApiSpec
            {
                Title = !string.IsNullOrWhiteSpace(viewModel.Title) ? viewModel.Title : (validation.Title ?? file.FileName),
                Description = viewModel.Description,
                Version = validation.Version ?? string.Empty,
                SpecFormat = validation.SpecFormat ?? string.Empty,
                FileName = file.FileName,
                FileSize = file.Length,
                DefaultBaseUrl = !string.IsNullOrWhiteSpace(viewModel.DefaultBaseUrl) ? viewModel.DefaultBaseUrl : validation.DefaultBaseUrl,
                AvailableServers = validation.Servers,
                EndpointCount = validation.EndpointCount,
                HttpMethods = validation.HttpMethods,
                ApiTags = validation.ApiTags,
                UserTags = viewModel.GetTags(),
                SecuritySchemes = validation.SecuritySchemes,
            };

            var saved = await _openApiService.UploadAsync(file, metadata);

            TempData["Success"] = $"Spec '{saved.Title}' uploaded — {saved.EndpointCount} endpoints discovered";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading OpenAPI spec");
            ModelState.AddModelError(string.Empty, $"Upload failed: {ex.Message}");
            return View(viewModel);
        }
    }

    // ── MVC: Details ──────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the details and parsed structure for a stored specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>The details view for the requested specification.</returns>
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        try
        {
            var spec = await _openApiService.GetByIdAsync(id);
            if (spec is null) return NotFound();

            var structure = await _openApiService.GetStructureAsync(id) ?? new OpenApiStructure();

            return View(new OpenApiDetailsViewModel { Spec = spec, Structure = structure });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OpenAPI details {Id}", id);
            TempData["Error"] = "Failed to load specification details";
            return RedirectToAction(nameof(Index));
        }
    }

    // ── MVC: Edit ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the edit form for a stored specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>The edit view for the requested specification.</returns>
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var spec = await _openApiService.GetByIdAsync(id);
        if (spec is null) return NotFound();

        return View(spec);
    }

    /// <summary>
    /// Updates metadata for a stored specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <param name="spec">Updated specification metadata.</param>
    /// <param name="userTagsString">Comma-separated user tags.</param>
    /// <param name="defaultBaseUrl">Optional default base URL override.</param>
    /// <returns>The edit view on validation failure or a redirect to the details view on success.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, OpenApiSpec spec, string? userTagsString, string? defaultBaseUrl)
    {
        if (id != spec.Id) return NotFound();

        if (!ModelState.IsValid) return View(spec);

        try
        {
            // Refresh tags from form string
            spec.UserTags = userTagsString?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList() ?? new();

            if (!string.IsNullOrWhiteSpace(defaultBaseUrl))
                spec.DefaultBaseUrl = defaultBaseUrl.Trim();

            await _openApiService.UpdateAsync(spec);
            TempData["Success"] = $"Specification '{spec.Title}' updated";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating OpenAPI spec {Id}", id);
            ModelState.AddModelError(string.Empty, "Failed to update specification");
            return View(spec);
        }
    }

    // ── MVC: Delete ───────────────────────────────────────────────────────────

    /// <summary>
    /// Displays the delete confirmation page for a stored specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>The delete confirmation view for the requested specification.</returns>
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var spec = await _openApiService.GetByIdAsync(id);
        if (spec is null) return NotFound();

        return View(spec);
    }

    /// <summary>
    /// Deletes a stored specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>A redirect to the index view.</returns>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        try
        {
            var spec = await _openApiService.GetByIdAsync(id);
            if (spec is null) return NotFound();

            await _openApiService.DeleteAsync(id);
            TempData["Success"] = $"Specification '{spec.Title}' deleted";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting OpenAPI spec {Id}", id);
            TempData["Error"] = "Failed to delete specification";
            return RedirectToAction(nameof(Index));
        }
    }

    // ── MVC: Download ─────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the raw specification file.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>The specification content as a file download.</returns>
    public async Task<IActionResult> Download(string id)
    {
        try
        {
            var spec = await _openApiService.GetByIdAsync(id);
            if (spec is null) return NotFound();

            var content = await _openApiService.GetContentAsync(id);
            if (content is null) return NotFound();

            return File(Encoding.UTF8.GetBytes(content), "application/json", spec.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading OpenAPI spec {Id}", id);
            TempData["Error"] = "Failed to download specification";
            return RedirectToAction(nameof(Index));
        }
    }

    // ── MVC: Test runner view ─────────────────────────────────────────────────

    /// <summary>
    /// Displays the interactive endpoint test runner for a specification.
    /// </summary>
    /// <param name="id">Specification identifier.</param>
    /// <returns>The test runner view for the requested specification.</returns>
    public async Task<IActionResult> Test(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        try
        {
            var spec = await _openApiService.GetByIdAsync(id);
            if (spec is null) return NotFound();

            var structure = await _openApiService.GetStructureAsync(id) ?? new OpenApiStructure();
            return View(new OpenApiDetailsViewModel { Spec = spec, Structure = structure });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading test runner for {Id}", id);
            TempData["Error"] = "Failed to load test runner";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // ── API: Get endpoints as JSON ────────────────────────────────────────────

    /// <summary>
    /// Returns the parsed endpoint list for a stored specification.
    /// </summary>
    /// <param name="specId">Specification identifier.</param>
    /// <returns>A JSON payload describing available servers, security schemes, and endpoints.</returns>
    [HttpGet("/api/openapi/{specId}/endpoints")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(OpenApiEndpointsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEndpoints(string specId)
    {
        try
        {
            var structure = await _openApiService.GetStructureAsync(specId);
            if (structure is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Specification Not Found",
                    Detail = $"OpenAPI specification with ID '{specId}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(new OpenApiEndpointsResponse
            {
                Servers = structure.Servers,
                SecuritySchemes = structure.SecuritySchemes,
                Endpoints = structure.AllEndpoints
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting endpoints for spec {SpecId}", specId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Endpoint Discovery Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    // ── API: Execute a single endpoint ────────────────────────────────────────

    /// <summary>
    /// Executes a single endpoint from a stored specification using caller-supplied inputs.
    /// </summary>
    /// <param name="specId">Specification identifier.</param>
    /// <param name="request">Endpoint execution request payload.</param>
    /// <returns>A JSON result containing request timing, response payload, and headers.</returns>
    [HttpPost("/api/openapi/{specId}/execute")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(OpenApiEndpointExecuteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteEndpoint(string specId, [FromBody] OpenApiEndpointExecuteRequest request)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Validate spec exists
            var spec = await _openApiService.GetByIdAsync(specId);
            if (spec is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Specification Not Found",
                    Detail = $"OpenAPI specification with ID '{specId}' was not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Build URL
            var baseUrl = request.BaseUrl.TrimEnd('/');
            var path = request.Path;

            // Substitute path parameters
            foreach (var (key, value) in request.PathParams)
                path = path.Replace($"{{{key}}}", Uri.EscapeDataString(value));

            // Build query string
            var queryParts = request.QueryParams
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")
                .ToList();

            var fullUrl = $"{baseUrl}{path}";
            if (queryParts.Count > 0) fullUrl += "?" + string.Join("&", queryParts);

            using var httpClient = _httpClientFactory.CreateClient("OpenApiTest");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            using var httpRequest = new HttpRequestMessage(
                new HttpMethod(request.Method.ToUpper()),
                fullUrl);

            // Auth
            switch (request.AuthType?.ToLower())
            {
                case "bearer":
                    if (!string.IsNullOrEmpty(request.BearerToken))
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.BearerToken);
                    break;
                case "apikey":
                    if (!string.IsNullOrEmpty(request.ApiKeyHeader) && !string.IsNullOrEmpty(request.ApiKeyValue))
                        httpRequest.Headers.TryAddWithoutValidation(request.ApiKeyHeader, request.ApiKeyValue);
                    break;
                case "basic":
                    if (!string.IsNullOrEmpty(request.BasicUsername))
                    {
                        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.BasicUsername}:{request.BasicPassword}"));
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
                    }
                    break;
            }

            // Custom headers
            foreach (var (key, value) in request.Headers)
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    httpRequest.Headers.TryAddWithoutValidation(key, value);

            // Body
            if (!string.IsNullOrEmpty(request.Body))
                httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");

            sw.Restart();
            var response = await httpClient.SendAsync(httpRequest);
            sw.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();

            // Collect response headers (flattened)
            var responseHeaders = new Dictionary<string, string>();
            foreach (var h in response.Headers)
                responseHeaders[h.Key] = string.Join(", ", h.Value);
            foreach (var h in response.Content.Headers)
                responseHeaders[h.Key] = string.Join(", ", h.Value);

            return Ok(new OpenApiEndpointExecuteResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? response.StatusCode.ToString(),
                ResponseBody = responseBody,
                ResponseHeaders = responseHeaders,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                RequestUrl = fullUrl
            });
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Ok(new OpenApiEndpointExecuteResult
            {
                Success = false,
                StatusCode = 408,
                StatusText = "Request Timeout",
                ErrorMessage = "The request timed out after 30 seconds",
                ResponseTimeMs = sw.ElapsedMilliseconds,
                RequestUrl = request.BaseUrl + request.Path
            });
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return Ok(new OpenApiEndpointExecuteResult
            {
                Success = false,
                StatusCode = 0,
                StatusText = "Connection Failed",
                ErrorMessage = ex.Message,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                RequestUrl = request.BaseUrl + request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing endpoint for spec {SpecId}", specId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Endpoint Execution Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}

