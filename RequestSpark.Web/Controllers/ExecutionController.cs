using Microsoft.AspNetCore.Mvc;
using RequestSpark.Web.Models;
using RequestSpark.Web.Services;

namespace RequestSpark.Web.Controllers;

/// <summary>
/// Controller for managing test executions (both API and MVC views)
/// </summary>
[Route("[controller]")]
public partial class ExecutionController : Controller
{
    private readonly IExecutionService _executionService;
    private readonly IConfigurationService _configurationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExecutionController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ExecutionController"/>.
    /// </summary>
    /// <param name="executionService">Service for managing test executions.</param>
    /// <param name="configurationService">Service for managing test configurations.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public ExecutionController(
        IExecutionService executionService,
        IConfigurationService configurationService,
        IHttpClientFactory httpClientFactory,
        ILogger<ExecutionController> logger)
    {
        _executionService = executionService;
        _configurationService = configurationService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ============ MVC VIEW ACTIONS ============

    /// <summary>
    /// Execution history list page
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 50, string? configurationId = null)
    {
        try
        {
            var history = await _executionService.GetExecutionHistoryAsync(pageSize, pageNumber, configurationId);
            var configurations = await _configurationService.GetAllAsync();

            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.ConfigurationId = configurationId;
            ViewBag.Configurations = configurations;

            return View(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading execution history");
            TempData["Error"] = "Failed to load execution history";
            return View(new List<ExecutionHistory>());
        }
    }

    /// <summary>
    /// Execution results detail page
    /// </summary>
    [HttpGet("Results/{id}")]
    public async Task<IActionResult> Results(string id)
    {
        try
        {
            var history = await _executionService.GetExecutionHistoryAsync(id);

            if (history == null)
            {
                TempData["Error"] = $"Execution {id} not found";
                return RedirectToAction(nameof(Index));
            }

            return View(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading execution results for {ExecutionId}", id);
            TempData["Error"] = "Failed to load execution results";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Delete execution history (MVC POST)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var deleted = await _executionService.DeleteExecutionHistoryAsync(id);

            if (deleted)
            {
                TempData["Success"] = "Execution history deleted successfully";
            }
            else
            {
                TempData["Error"] = "Execution not found";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting execution {ExecutionId}", id);
            TempData["Error"] = "Failed to delete execution history";
            return RedirectToAction(nameof(Index));
        }
    }

    // ============ API ENDPOINTS ============

    /// <summary>
    /// Start a new test execution
    /// </summary>
    /// <param name="request">Execution start request</param>
    /// <returns>Created test execution</returns>
    [HttpPost("/api/execution/start")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TestExecution), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TestExecution>> StartExecution([FromBody] StartExecutionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ConfigurationId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "ConfigurationId is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Verify configuration exists
            var config = await _configurationService.GetByIdAsync(request.ConfigurationId);
            if (config == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Configuration Not Found",
                    Detail = $"Configuration with ID '{request.ConfigurationId}' not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var execution = await _executionService.StartExecutionAsync(
                request.ConfigurationId,
                request.ExecutedBy ?? "API");

            _logger.LogInformation("Started execution {ExecutionId} via API", execution.Id);

            return CreatedAtAction(
                nameof(GetExecutionStatus),
                new { executionId = execution.Id },
                execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start execution for configuration {ConfigurationId}", request.ConfigurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Execution Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get execution status and progress
    /// </summary>
    /// <param name="executionId">Execution ID</param>
    /// <returns>Test execution details</returns>
    [HttpGet("/api/execution/{executionId}/status")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TestExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestExecution>> GetExecutionStatus(string executionId)
    {
        var execution = await _executionService.GetExecutionAsync(executionId);

        if (execution == null)
        {
            // Try history
            var history = await _executionService.GetExecutionHistoryAsync(executionId);
            if (history != null)
            {
                // Convert history to execution for consistent response
                return Ok(new TestExecution
                {
                    Id = history.Id,
                    ConfigurationId = history.ConfigurationId,
                    ConfigurationName = history.ConfigurationName,
                    SourceType = history.SourceType,
                    SourceId = history.SourceId,
                    SourceName = history.SourceName,
                    Status = history.Status,
                    StartTime = history.StartTime,
                    ExecutedBy = history.ExecutedBy,
                    ErrorMessage = history.ErrorMessage,
                    ProgressPercentage = 100,
                    CurrentPhase = history.Status.ToString(),
                    TotalRequests = history.Statistics?.TotalRequests ?? 0,
                    CompletedRequests = history.Statistics?.TotalRequests ?? 0,
                    SuccessfulRequests = history.Statistics?.SuccessfulRequests ?? 0,
                    FailedRequests = history.Statistics?.FailedRequests ?? 0,
                    CurrentAverageResponseTime = history.Statistics?.AverageResponseTime ?? 0
                });
            }

            return NotFound(new ProblemDetails
            {
                Title = "Execution Not Found",
                Detail = $"Execution with ID '{executionId}' not found",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(execution);
    }

    /// <summary>
    /// Cancel a running execution
    /// </summary>
    /// <param name="executionId">Execution ID</param>
    /// <param name="request">Cancel request</param>
    /// <returns>Cancellation result</returns>
    [HttpPost("/api/execution/{executionId}/cancel")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CancelExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelExecution(string executionId, [FromBody] CancelExecutionRequest request)
    {
        var cancelled = await _executionService.CancelExecutionAsync(
            executionId,
            request.CancelledBy ?? "API");

        if (!cancelled)
        {
            var execution = await _executionService.GetExecutionAsync(executionId);
            if (execution == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Execution Not Found",
                    Detail = $"Execution with ID '{executionId}' not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Cannot Cancel",
                Detail = "Execution cannot be cancelled in its current state",
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Cancelled execution {ExecutionId} via API", executionId);
        return Ok(new CancelExecutionResponse
        {
            Message = "Execution cancelled successfully",
            ExecutionId = executionId
        });
    }

    /// <summary>
    /// Get execution results
    /// </summary>
    /// <param name="executionId">Execution ID</param>
    /// <returns>Execution history with results</returns>
    [HttpGet("/api/execution/{executionId}/results")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ExecutionHistory), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionHistory>> GetExecutionResults(string executionId)
    {
        var history = await _executionService.GetExecutionHistoryAsync(executionId);

        if (history == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Results Not Found",
                Detail = $"Execution results for ID '{executionId}' not found",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(history);
    }

    /// <summary>
    /// Download execution results as CSV
    /// </summary>
    /// <param name="executionId">Execution ID</param>
    /// <returns>CSV file</returns>
    [HttpGet("/api/execution/{executionId}/download")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadResults(string executionId)
    {
        var history = await _executionService.GetExecutionHistoryAsync(executionId);

        if (history == null || string.IsNullOrEmpty(history.ResultsFilePath))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Results Not Found",
                Detail = $"Results file for execution '{executionId}' not found",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (!System.IO.File.Exists(history.ResultsFilePath))
        {
            return NotFound(new ProblemDetails
            {
                Title = "File Not Found",
                Detail = "Results file does not exist on disk",
                Status = StatusCodes.Status404NotFound
            });
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(history.ResultsFilePath);
        var fileName = $"execution_{executionId}_results.csv";

        return File(fileBytes, "text/csv", fileName);
    }

    /// <summary>
    /// Get all running executions
    /// </summary>
    /// <returns>List of running executions</returns>
    [HttpGet("/api/execution/running")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<TestExecution>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TestExecution>>> GetRunningExecutions()
    {
        var executions = await _executionService.GetRunningExecutionsAsync();
        return Ok(executions);
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    /// <param name="pageSize">Page size</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="configurationId">Filter by configuration ID (optional)</param>
    /// <returns>List of execution history records</returns>
    [HttpGet("/api/execution/history")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<ExecutionHistory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExecutionHistory>>> GetExecutionHistoryApi(
        [FromQuery] int pageSize = 50,
        [FromQuery] int pageNumber = 1,
        [FromQuery] string? configurationId = null)
    {
        var history = await _executionService.GetExecutionHistoryAsync(pageSize, pageNumber, configurationId);
        return Ok(history);
    }

    /// <summary>
    /// Get recent executions
    /// </summary>
    /// <param name="count">Number of recent executions to return</param>
    /// <returns>List of recent executions</returns>
    [HttpGet("/api/execution/recent")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<ExecutionHistory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExecutionHistory>>> GetRecentExecutions([FromQuery] int count = 10)
    {
        var executions = await _executionService.GetRecentExecutionsAsync(count);
        return Ok(executions);
    }

    /// <summary>
    /// Delete execution history (API)
    /// </summary>
    /// <param name="executionId">Execution ID</param>
    /// <returns>Deletion result</returns>
    [HttpDelete("/api/execution/{executionId}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExecutionHistoryApi(string executionId)
    {
        var deleted = await _executionService.DeleteExecutionHistoryAsync(executionId);

        if (!deleted)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Execution Not Found",
                Detail = $"Execution with ID '{executionId}' not found",
                Status = StatusCodes.Status404NotFound
            });
        }

        _logger.LogInformation("Deleted execution history {ExecutionId} via API", executionId);
        return NoContent();
    }

}

