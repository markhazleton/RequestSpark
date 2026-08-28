using ApiTestSpark;
using Microsoft.AspNetCore.Http.Features; // Add for FormOptions
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Server.Kestrel.Core; // Add for KestrelServerOptions
using Microsoft.OpenApi;
using RequestSpark.Domain.Interfaces;
using RequestSpark.Domain.Models;
using RequestSpark.Web.Models;
using RequestSpark.Web.Models.ViewModels;
using RequestSpark.Web.SampleCRUD;
using RequestSpark.Web.Services; // Add for our new services
using System.Text.Json;
using System.Text.Json.Nodes;
using WebSpark.Bootswatch; // Add for Bootswatch
using WebSpark.HttpClientUtility; // Add for HttpClientUtility
using WebSpark.HttpClientUtility.ClientService;
using WebSpark.HttpClientUtility.RequestResult; // Add for HttpRequestResultService
using WebSpark.HttpClientUtility.StringConverter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "RequestSpark API";
        document.Info.Version = "v1";
        document.Info.Contact = new OpenApiContact
        {
            Name = "Make Bold Solutions",
            Url = new Uri("https://makeboldsolutions.com/")
        };
        document.Info.License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        };
        foreach (var path in document.Paths.Keys
            .Where(path => !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api-test-spark/", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            document.Paths.Remove(path);
        }

        ApplyApiTestSparkDocumentMetadata(document);
        document.Info.Description = BuildApiDescription(document);

        return Task.CompletedTask;
    });

    options.AddSchemaTransformer(ApplyApiTestSparkSchemaMetadata);
});

builder.Services.AddMvcCore().AddApiExplorer();
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new ApiRouteApiExplorerConvention());
});
builder.Services.AddHttpContextAccessor();

// Configure form options for file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Configure request size limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Configure Kestrel server options for file uploads
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Register HttpClientUtility services BEFORE Bootswatch (required dependency)
builder.Services.AddHttpClientUtility();

// Register Bootswatch theme switcher (depends on HttpClientUtility)
builder.Services.AddBootswatchThemeSwitcher();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IStringConverter, SystemJsonStringConverter>();
builder.Services.AddScoped<IHttpClientService, HttpClientService>();
builder.Services.AddScoped<HttpRequestResultService>();
builder.Services.AddScoped<IHttpRequestResultService>(provider =>
{
    IHttpRequestResultService service = provider.GetRequiredService<HttpRequestResultService>();
    // Add Telemetry decorator (outermost layer)
    service = new HttpRequestResultServiceTelemetry(
        provider.GetRequiredService<ILogger<HttpRequestResultServiceTelemetry>>(),
        service
    );
    return service;
});

// Add RequestSpark services
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IConfigurationService, FileConfigurationService>();
builder.Services.AddScoped<ICollectionService, FileCollectionService>();
builder.Services.AddScoped<IOpenApiService, FileOpenApiService>();
builder.Services.AddScoped<IApiDefinitionMappingService, ApiDefinitionMappingService>();
builder.Services.AddSingleton<IExecutionHistoryStore, ExecutionHistoryStore>();
builder.Services.AddSingleton<IExecutionService, RealExecutionService>();
builder.Services.AddScoped<SampleCRUDService>(); // <-- Add SampleCRUDService registration

// Add SignalR for real-time execution updates
builder.Services.AddSignalR(); // <-- Added SignalR service

// Register CompareRunner as a factory that creates a basic initialized instance
// For web usage, we'll create configurations dynamically rather than using static initialization
builder.Services.AddScoped<CompareRunner>(serviceProvider =>
{
    var runner = new CompareRunner();

    // Initialize with basic default configuration
    runner.Instances = new List<CompareInstance>
    {
        new() { Name = "Local", BaseUrl = "https://localhost:7001/" },
        new() { Name = "Demo", BaseUrl = "https://ui.makeboldspark.com/" }
    };

    runner.Requests = new List<CompareRequest>
    {
        new() { Path = "api/status", RequestMethod = HttpVerb.GET, RequiresClientToken = false }
    };

    runner.Users = new List<CompareUser>
    {
        new()
        {
            UserName = "default",
            Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword
        }
    };

    return runner;
});

var app = builder.Build();

// Initialize file storage and create sample data on startup
using (var scope = app.Services.CreateScope())
{
    var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    await fileStorage.InitializeAsync();

    // Initialize sample data
    await InitializeSampleDataAsync(scope.ServiceProvider);
}

app.UseStaticFiles();
app.UseRouting();
app.UseBootswatchAll();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Helper: proxy an upstream call and return a proper error response on failure
static async Task<IResult> Proxy<T>(Func<Task<T>> call)
{
    try
    {
        return Results.Ok(await call());
    }
    catch (TaskCanceledException)
    {
        return Results.Problem("The upstream API did not respond in time.", statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (ApiException ex) when (ex.StatusCode is >= 400 and <= 599)
    {
        return Results.Problem(
            string.IsNullOrWhiteSpace(ex.Response) ? ex.Message : ex.Response,
            statusCode: ex.StatusCode);
    }
    catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
    {
        return Results.Problem(
            $"Upstream request failed: {ex.Message}",
            statusCode: (int)ex.StatusCode.Value);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"Upstream request failed: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unexpected error: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
    }
}

// Employee Endpoints
app.MapGet("/api/employee", (SampleCRUDService service, int? pageNumber, int? pageSize) =>
    Proxy(() => service.GetAllEmployees(pageNumber, pageSize)))
.WithTags("Sample CRUD: Employees")
.WithName("GetAllEmployees")
.WithSummary("List employees")
.WithDescription("""
    Returns a paged list of employee records from the upstream sample CRUD API.

    Use `pageNumber=1` and `pageSize=15` for the default seeded data view.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "pageNumber", "One-based page number. Use `1` to start with the first page of seeded employees.", 1);
    SetParameterMetadata(operation, "pageSize", "Number of employee records to return. The sample UI starts with `15`.", 15);
    return Task.CompletedTask;
})
.Produces<ICollection<EmployeeDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/employee/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.GetEmployeeById(id)))
.WithTags("Sample CRUD: Employees")
.WithName("GetEmployeeById")
.WithSummary("Get an employee by ID")
.WithDescription("""
    Returns a single employee record by identifier.

    Try seeded ID `1` first. A missing upstream employee is returned as `404`.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "id", "Employee identifier. Try `1` for seeded sample data.", 1);
    return Task.CompletedTask;
})
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPost("/api/employee", async (SampleCRUDService service, EmployeeDto employee) =>
{
    var validationErrors = EmployeeDtoValidator.Validate(employee);
    return validationErrors.Count > 0
        ? Results.ValidationProblem(validationErrors)
    : await Proxy(() => service.CreateEmployee(employee));
})
.WithTags("Sample CRUD: Employees")
.WithName("CreateEmployee")
.WithSummary("Create an employee")
.WithDescription("""
    Creates an employee in the upstream sample CRUD API.

    **Workflow callout:** after creating an employee, copy the returned `id` and call `PUT /api/employee/{id}` to update the same record.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetJsonRequestExample(operation, """
        {
          "age": 34,
          "country": "USA",
          "department": 1,
          "departmentName": "Engineering",
          "gender": 1,
          "id": 0,
          "manager_id": 1,
          "name": "Ada Lovelace",
          "state": "TX",
          "profile_picture": "https://example.com/profiles/ada.png"
        }
        """);
    return Task.CompletedTask;
})
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPut("/api/employee/{id}", async (SampleCRUDService service, int id, EmployeeDto employee) =>
{
    var validationErrors = EmployeeDtoValidator.Validate(employee);
    if (id <= 0)
    {
        validationErrors["id"] = ["Id must be greater than zero."];
    }

    return validationErrors.Count > 0
        ? Results.ValidationProblem(validationErrors)
        : await Proxy(() => service.UpdateEmployee(id, employee));
})
.WithTags("Sample CRUD: Employees")
.WithName("UpdateEmployee")
.WithSummary("Update an employee")
.WithDescription("""
    Updates an existing employee in the upstream sample CRUD API.

    Use the same `id` in the route and request body. The local wrapper validates that the route `id` is greater than zero.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "id", "Employee identifier to update. Use an ID returned by `POST /api/employee` or a seeded ID such as `1`.", 1);
    SetJsonRequestExample(operation, """
        {
          "age": 35,
          "country": "USA",
          "department": 1,
          "departmentName": "Engineering",
          "gender": 1,
          "id": 1,
          "manager_id": 1,
          "name": "Ada Lovelace",
          "state": "TX",
          "profile_picture": "https://example.com/profiles/ada.png"
        }
        """);
    return Task.CompletedTask;
})
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapDelete("/api/employee/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.DeleteEmployee(id)))
.WithTags("Sample CRUD: Employees")
.WithName("DeleteEmployee")
.WithSummary("Delete an employee")
.WithDescription("""
    Deletes an employee from the upstream sample CRUD API.

    Use an ID returned by `POST /api/employee` when testing destructive behavior.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "id", "Employee identifier to delete.", 1);
    return Task.CompletedTask;
})
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

// Department Endpoints
app.MapGet("/api/department", (SampleCRUDService service, bool? includeEmployees) =>
    Proxy(() => service.GetDepartments(includeEmployees)))
.WithTags("Sample CRUD: Departments")
.WithName("GetDepartments")
.WithSummary("List departments")
.WithDescription("""
    Returns departments from the upstream sample CRUD API.

    Set `includeEmployees=true` to include nested employee collections in each department response.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "includeEmployees", "When `true`, include nested employee records for each department.", true);
    return Task.CompletedTask;
})
.Produces<ICollection<DepartmentDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/department/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.GetDepartmentById(id)))
.WithTags("Sample CRUD: Departments")
.WithName("GetDepartmentById")
.WithSummary("Get a department by ID")
.WithDescription("""
    Returns one department from the upstream sample CRUD API.

    Try seeded ID `1` first. Set `GET /api/department?includeEmployees=true` when you want the broader department list with nested employees.
    """)
.AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
{
    SetParameterMetadata(operation, "id", "Department identifier. Try `1` for seeded sample data.", 1);
    return Task.CompletedTask;
})
.Produces<ICollection<DepartmentDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/api-explorer", (SampleCRUDService service) =>
    Proxy(() => service.GetApiExplorer()))
.WithTags("RequestSpark: System")
.WithName("GetApiExplorer")
.WithSummary("List upstream API explorer groups")
.WithDescription("""
    Returns the upstream sample API explorer data grouped by resource.

    Use this endpoint when you want to compare RequestSpark's proxied routes with the source sample API surface.
    """)
.Produces<ICollection<ApiExplorerModel>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/status", (SampleCRUDService service) =>
    Proxy(() => service.GetStatus()))
.WithTags("RequestSpark: System")
.WithName("GetStatus")
.WithSummary("Get upstream service status")
.WithDescription("""
    Returns status, build, region, feature, and test metadata from the upstream sample CRUD API.

    Start here before running employee or department requests so you know the upstream sample service is reachable.
    """)
.Produces<ApplicationStatus>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

// Add initialization status endpoint
app.MapGet("/api/initialization-status", async (HttpContext context) =>
{
    var serviceProvider = context.RequestServices;
    try
    {
        var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
        var collectionService = serviceProvider.GetRequiredService<ICollectionService>();

        var configurations = await configurationService.GetAllAsync();
        var collections = await collectionService.GetAllAsync();

        var initialConfig = configurations.FirstOrDefault(c => c.Name == "Initial RequestSpark Configuration");
        var sampleCollection = collections.FirstOrDefault(c => c.Name == "Sample RequestSpark Test Collection");

        return Results.Ok(new InitializationStatusResponse
        {
            Timestamp = DateTime.UtcNow,
            Initialized = true,
            SampleCollection = new SampleCollectionInitializationStatus
            {
                Exists = sampleCollection != null,
                Id = sampleCollection?.Id,
                Name = sampleCollection?.Name,
                RequestCount = sampleCollection?.RequestCount ?? 0
            },
            InitialConfiguration = new InitialConfigurationStatus
            {
                Exists = initialConfig != null,
                Id = initialConfig?.Id,
                Name = initialConfig?.Name,
                TotalTests = initialConfig?.GetTotalTestCount() ?? 0,
                Iterations = initialConfig?.Iterations ?? 0,
                Concurrency = initialConfig?.MaxConcurrency ?? 0
            },
            Totals = new InitializationTotals
            {
                Configurations = configurations.Count,
                Collections = collections.Count,
                ActiveConfigurations = configurations.Count(c => c.IsActive)
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Initialization status check failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithTags("RequestSpark: System")
.WithName("GetInitializationStatus")
.WithSummary("Get initialization status")
.WithDescription("""
    Shows whether the default RequestSpark configuration and sample collection exist in local storage.

    This is useful after startup or deployment to confirm the demo data needed by the UI was created.
    """)
.Produces<InitializationStatusResponse>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status500InternalServerError);


// Add debug endpoint to list all configurations (development only)
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug-configurations", async (HttpContext context) =>
    {
        var serviceProvider = context.RequestServices;
        try
        {
            var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
            var configurations = await configurationService.GetAllAsync();

            return Results.Ok(new DebugConfigurationsResponse
            {
                Timestamp = DateTime.UtcNow,
                TotalConfigurations = configurations.Count,
                Configurations = configurations.Select(c => new DebugConfigurationSummary
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    Iterations = c.Iterations,
                    MaxConcurrency = c.MaxConcurrency,
                    InstancesCount = c.Runner?.Instances?.Count ?? 0,
                    UsersCount = c.Runner?.Users?.Count ?? 0,
                    RequestsCount = c.Runner?.Requests?.Count ?? 0,
                    EditUrl = $"/Configuration/Edit/{c.Id}"
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Debug configurations check failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithTags("Diagnostics: Debug")
    .WithName("GetDebugConfigurations")
    .WithSummary("List debug configuration summaries")
    .WithDescription("""
        Development-only diagnostics endpoint that lists stored RequestSpark configurations and their request/user/instance counts.

        Use this to confirm generated sample data and inspect edit links during local development.
        """)
    .Produces<DebugConfigurationsResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status500InternalServerError);
}

// Map SignalR hub for real-time execution updates
app.MapHub<RequestSpark.Web.Hubs.ExecutionHub>("/hubs/execution");

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
}

app.MapApiTestSpark(options =>
{
    options.OpenApiUrl = "/openapi/v1.json";
    options.Environments = ["Development", "Staging"];
    options.EnableDemoIntegrations = false;
    options.DefaultHeaders["X-RequestSpark-RequestId"] = "{request-guid}";
    options.DefaultHeaders["X-RequestSpark-SessionId"] = "{session-guid}";
});

app.Run();

static void ApplyApiTestSparkDocumentMetadata(OpenApiDocument document)
{
    if (document.Paths is null)
    {
        return;
    }

    EnsureTag(document, "Sample CRUD: Employees", "Employee sample CRUD proxy endpoints.");
    EnsureTag(document, "Sample CRUD: Departments", "Department sample CRUD proxy endpoints.");
    EnsureTag(document, "RequestSpark: System", "Local health, startup, and upstream discovery endpoints.");
    EnsureTag(document, "RequestSpark: Executions", "Start, monitor, cancel, inspect, export, and delete RequestSpark executions.");
    EnsureTag(document, "RequestSpark: OpenAPI Specs", "Interact with uploaded OpenAPI specifications.");
    EnsureTag(document, "Diagnostics: Debug", "Development-only diagnostics endpoints.");

    foreach (var (path, pathItem) in document.Paths)
    {
        foreach (var (httpMethod, operation) in pathItem.Operations ?? [])
        {
            var method = httpMethod.Method.ToUpperInvariant();
            var normalizedPath = path.ToLowerInvariant();

            switch (method, normalizedPath)
            {
                case ("POST", "/api/execution/start"):
                    SetOperationMetadata(document, operation, "StartExecution", "RequestSpark: Executions", "Start a test execution", """
                        Starts a saved RequestSpark configuration and returns the created execution.

                        **Workflow callout:** copy the returned `id`, then call `GET /api/execution/{executionId}/status` to watch progress.
                        """);
                    SetJsonRequestExample(operation, """
                        {
                          "configurationId": "sample-configuration-id",
                          "executedBy": "API"
                        }
                        """);
                    break;

                case ("GET", "/api/execution/{executionid}/status"):
                    SetOperationMetadata(document, operation, "GetExecutionStatus", "RequestSpark: Executions", "Get execution status", """
                        Returns current execution progress. If the run already completed, the endpoint falls back to persisted execution history.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier returned from `POST /api/execution/start`.", "execution-id");
                    break;

                case ("POST", "/api/execution/{executionid}/cancel"):
                    SetOperationMetadata(document, operation, "CancelExecution", "RequestSpark: Executions", "Cancel an execution", """
                        Attempts to cancel a running execution.

                        The endpoint returns `400` when the execution exists but is already in a state that cannot be cancelled.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier returned from `POST /api/execution/start`.", "execution-id");
                    SetJsonRequestExample(operation, """
                        {
                          "cancelledBy": "API"
                        }
                        """);
                    break;

                case ("GET", "/api/execution/{executionid}/results"):
                    SetOperationMetadata(document, operation, "GetExecutionResults", "RequestSpark: Executions", "Get execution results", """
                        Returns persisted execution history, summary statistics, and result-file metadata for a completed run.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier to inspect.", "execution-id");
                    break;

                case ("GET", "/api/execution/{executionid}/download"):
                    SetOperationMetadata(document, operation, "DownloadExecutionResults", "RequestSpark: Executions", "Download execution CSV", """
                        Downloads the persisted CSV results file for a completed execution.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier whose CSV results should be downloaded.", "execution-id");
                    break;

                case ("GET", "/api/execution/running"):
                    SetOperationMetadata(document, operation, "GetRunningExecutions", "RequestSpark: Executions", "List running executions", """
                        Returns currently running RequestSpark executions.
                        """);
                    break;

                case ("GET", "/api/execution/history"):
                    SetOperationMetadata(document, operation, "GetExecutionHistory", "RequestSpark: Executions", "List execution history", """
                        Returns paged execution history, optionally filtered to a single configuration.
                        """);
                    SetParameterMetadata(operation, "pageSize", "Number of history records to return.", 50);
                    SetParameterMetadata(operation, "pageNumber", "One-based history page number.", 1);
                    SetParameterMetadata(operation, "configurationId", "Optional configuration identifier filter.", "sample-configuration-id");
                    break;

                case ("GET", "/api/execution/recent"):
                    SetOperationMetadata(document, operation, "GetRecentExecutions", "RequestSpark: Executions", "List recent executions", """
                        Returns the most recent persisted executions for dashboards and quick status checks.
                        """);
                    SetParameterMetadata(operation, "count", "Number of recent executions to return.", 10);
                    break;

                case ("DELETE", "/api/execution/{executionid}"):
                    SetOperationMetadata(document, operation, "DeleteExecutionHistory", "RequestSpark: Executions", "Delete execution history", """
                        Deletes persisted history and associated result files for one execution.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier to delete.", "execution-id");
                    break;

                case ("GET", "/api/execution/{executionid}/results/rows"):
                    SetOperationMetadata(document, operation, "GetExecutionResultRows", "RequestSpark: Executions", "List execution result rows", """
                        Parses the persisted CSV results file and returns rows sorted by duration.

                        Use the optional filters to focus on a status code, HTTP verb, target instance, or path substring.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier whose CSV rows should be parsed.", "execution-id");
                    SetParameterMetadata(operation, "statusCode", "Optional HTTP status code filter, such as `200` or `500`.", "200");
                    SetParameterMetadata(operation, "verb", "Optional HTTP verb filter.", "GET");
                    SetParameterMetadata(operation, "instance", "Optional target instance name filter.", "Local");
                    SetParameterMetadata(operation, "path", "Optional request path substring filter.", "/api/status");
                    break;

                case ("POST", "/api/execution/{executionid}/export"):
                    SetOperationMetadata(document, operation, "ExportExecutionResults", "RequestSpark: Executions", "Export execution results", """
                        Exports persisted execution results in the requested format and returns generated file metadata.
                        """);
                    SetParameterMetadata(operation, "executionId", "Execution identifier whose results should be exported.", "execution-id");
                    SetParameterMetadata(operation, "format", "Export format. Use `csv` for spreadsheet-friendly output.", "csv");
                    break;

                case ("POST", "/api/execution/test-request"):
                    SetOperationMetadata(document, operation, "TestSingleRequest", "RequestSpark: Executions", "Test a single request", """
                        Executes one ad hoc HTTP request and returns timing, status, response headers, and response body.

                        Start with the sample status endpoint, then change `method`, `path`, headers, and body for your own target API.
                        """);
                    SetJsonRequestExample(operation, """
                        {
                          "baseUrl": "https://ui.makeboldspark.com",
                          "path": "/api/status",
                          "method": "GET",
                          "body": null,
                          "bearerToken": null,
                          "headers": {
                            "Accept": "application/json"
                          }
                        }
                        """);
                    break;

                case ("GET", "/api/openapi/{specid}/endpoints"):
                    SetOperationMetadata(document, operation, "GetStoredOpenApiEndpoints", "RequestSpark: OpenAPI Specs", "List stored spec endpoints", """
                        Returns parsed endpoint, parameter, request body, response, server, and security metadata for an uploaded OpenAPI specification.
                        """);
                    SetParameterMetadata(operation, "specId", "Stored OpenAPI specification identifier.", "spec-id");
                    break;

                case ("POST", "/api/openapi/{specid}/execute"):
                    SetOperationMetadata(document, operation, "ExecuteStoredOpenApiEndpoint", "RequestSpark: OpenAPI Specs", "Execute a stored spec endpoint", """
                        Executes one endpoint from an uploaded OpenAPI specification using supplied path, query, header, body, and auth values.
                        """);
                    SetParameterMetadata(operation, "specId", "Stored OpenAPI specification identifier.", "spec-id");
                    SetJsonRequestExample(operation, """
                        {
                          "baseUrl": "https://ui.makeboldspark.com",
                          "path": "/api/status",
                          "method": "GET",
                          "pathParams": {},
                          "queryParams": {},
                          "headers": {
                            "Accept": "application/json"
                          },
                          "body": null,
                          "authType": "none",
                          "bearerToken": null,
                          "apiKeyHeader": "X-API-Key",
                          "apiKeyValue": null,
                          "basicUsername": null,
                          "basicPassword": null
                        }
                        """);
                    break;
            }
        }
    }
}

static string BuildApiDescription(OpenApiDocument document)
{
    var groupCounts = (document.Paths ?? [])
        .SelectMany(path => path.Value.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
        .SelectMany(operation => operation.Tags?.Select(tag => tag.Name ?? "Other: Misc") ?? ["Other: Misc"])
        .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => $"| {group.Key} | {group.Count()} |");

    return $"""
        RequestSpark exposes APIs for proxying the sample CRUD service, inspecting local runtime health, running configured request tests, and executing uploaded OpenAPI definitions.

        | Group | Endpoint count |
        | --- | ---: |
        {string.Join(Environment.NewLine, groupCounts)}

        Suggested workflow:

        1. Call `GET /api/status` to confirm the upstream sample API is reachable.
        2. Call `GET /api/employee?pageNumber=1&pageSize=15` to inspect seeded employees.
        3. Call `GET /api/employee/1` to fetch one known employee.
        4. Try `POST /api/employee` with the generated example body, then use the returned `id` with the matching employee update endpoint.
        5. Start a configured RequestSpark run with `POST /api/execution/start`, then inspect status and results with the returned execution ID.

        Seeded sample data is provided by `https://ui.makeboldspark.com/` using API version `1`.
        """;
}

static void EnsureTag(OpenApiDocument document, string name, string description)
{
    document.Tags ??= new HashSet<OpenApiTag>();

    if (document.Tags.Any(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase)))
    {
        return;
    }

    document.Tags.Add(new OpenApiTag
    {
        Name = name,
        Description = description
    });
}

static void SetOperationMetadata(
    OpenApiDocument document,
    OpenApiOperation operation,
    string operationId,
    string tag,
    string summary,
    string description)
{
    operation.OperationId = operationId;
    operation.Summary = summary;
    operation.Description = description;
    operation.Tags = new HashSet<OpenApiTagReference>
    {
        new(tag, document, null)
    };
}

static Task ApplyApiTestSparkSchemaMetadata(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
{
    var type = context.JsonTypeInfo.Type;

    if (type == typeof(EmployeeDto))
    {
        schema.Description = "Employee record used by the upstream sample CRUD API.";
        SetSchemaProperty(schema, "id", "Employee identifier assigned by the sample CRUD API. Use `0` when creating a new employee.", defaultValue: 0, example: 1, minimum: "0");
        SetSchemaProperty(schema, "name", "Employee display name.", defaultValue: "Ada Lovelace", example: "Ada Lovelace", minLength: 1, maxLength: 120);
        SetSchemaProperty(schema, "age", "Employee age in years.", defaultValue: 34, example: 34, minimum: "16", maximum: "100");
        SetSchemaProperty(schema, "gender", "Numeric gender enum value used by the upstream sample API.", defaultValue: 1, example: 1, minimum: "0", maximum: "2");
        SetSchemaProperty(schema, "department", "Numeric department enum value used by the upstream sample API.", defaultValue: 1, example: 1, minimum: "0", maximum: "6");
        SetSchemaProperty(schema, "departmentName", "Human-readable department name.", defaultValue: "Engineering", example: "Engineering", minLength: 1, maxLength: 100);
        SetSchemaProperty(schema, "manager_id", "Optional manager employee identifier.", defaultValue: 1, example: 1, minimum: "1");
        SetSchemaProperty(schema, "country", "Employee country code or country name.", defaultValue: "USA", example: "USA", minLength: 2, maxLength: 80);
        SetSchemaProperty(schema, "state", "Employee state or region.", defaultValue: "TX", example: "TX", minLength: 2, maxLength: 80);
        SetSchemaProperty(schema, "profile_picture", "URL for the employee profile image.", defaultValue: "https://example.com/profiles/ada.png", example: "https://example.com/profiles/ada.png", minLength: 1, maxLength: 512);
        SetSchemaProperty(schema, "profileImage", "Optional binary profile image content returned by some upstream API responses.");
    }
    else if (type == typeof(DepartmentDto))
    {
        schema.Description = "Department record returned by the upstream sample CRUD API.";
        SetSchemaProperty(schema, "id", "Department identifier assigned by the upstream sample API.", defaultValue: 1, example: 1, minimum: "1");
        SetSchemaProperty(schema, "name", "Department display name.", defaultValue: "Engineering", example: "Engineering", minLength: 1, maxLength: 100);
        SetSchemaProperty(schema, "description", "Department description.", defaultValue: "Builds and maintains product systems.", example: "Builds and maintains product systems.", maxLength: 500);
        SetSchemaProperty(schema, "employees", "Employees assigned to this department when `includeEmployees=true`.");
    }
    else if (type == typeof(ApplicationStatus))
    {
        schema.Description = "Runtime status payload returned by the upstream sample CRUD API.";
        SetSchemaProperty(schema, "buildDate", "UTC build timestamp reported by the upstream service.");
        SetSchemaProperty(schema, "buildVersion", "Version components reported by the upstream service.");
        SetSchemaProperty(schema, "features", "Feature flags or capabilities reported by the upstream service.");
        SetSchemaProperty(schema, "messages", "Status messages returned by the upstream service.");
        SetSchemaProperty(schema, "region", "Hosting region reported by the upstream service.", defaultValue: "Central US", example: "Central US");
        SetSchemaProperty(schema, "status", "Current service status enum value.");
        SetSchemaProperty(schema, "tests", "Health-test details reported by the upstream service.");
    }
    else if (type == typeof(BuildVersion))
    {
        schema.Description = "Build version components reported by the upstream service.";
        SetSchemaProperty(schema, "majorVersion", "Major version component.", example: 1, minimum: "0");
        SetSchemaProperty(schema, "minorVersion", "Minor version component.", example: 0, minimum: "0");
        SetSchemaProperty(schema, "build", "Build number.", example: 0, minimum: "0");
        SetSchemaProperty(schema, "revision", "Revision number.", example: 0, minimum: "0");
    }
    else if (type == typeof(ApiExplorerModel))
    {
        schema.Description = "Upstream API explorer group with related endpoint descriptions.";
        SetSchemaProperty(schema, "groupName", "Display name for the upstream endpoint group.", example: "Employee");
        SetSchemaProperty(schema, "groupItems", "Endpoint descriptions in this upstream group.");
    }
    else if (type == typeof(ApiDescriptionModel))
    {
        schema.Description = "Upstream API endpoint description.";
        SetSchemaProperty(schema, "relativePath", "Relative endpoint path from the upstream sample API.", example: "api/employee");
    }
    else if (type == typeof(InitializationStatusResponse))
    {
        schema.Description = "Current startup data status for the RequestSpark host application.";
    }
    else if (type == typeof(SampleCollectionInitializationStatus))
    {
        schema.Description = "Status of the seeded sample Postman collection.";
    }
    else if (type == typeof(InitialConfigurationStatus))
    {
        schema.Description = "Status of the seeded default RequestSpark test configuration.";
    }
    else if (type == typeof(InitializationTotals))
    {
        schema.Description = "Aggregate counts for initialized RequestSpark data.";
    }
    else if (type == typeof(DebugConfigurationsResponse))
    {
        schema.Description = "Development diagnostics for stored RequestSpark configurations.";
    }
    else if (type == typeof(DebugConfigurationSummary))
    {
        schema.Description = "Development summary for one stored RequestSpark configuration.";
    }
    else if (type == typeof(StartExecutionRequest))
    {
        schema.Description = "Request payload for starting a saved RequestSpark configuration.";
    }
    else if (type == typeof(CancelExecutionRequest))
    {
        schema.Description = "Request payload for cancelling a running execution.";
    }
    else if (type == typeof(CancelExecutionResponse))
    {
        schema.Description = "Response returned after an execution cancellation request is accepted.";
    }
    else if (type == typeof(ExportResult))
    {
        schema.Description = "File metadata returned after exporting execution results.";
    }
    else if (type == typeof(SingleRequestTestRequest))
    {
        schema.Description = "Request payload for executing one ad hoc HTTP request.";
    }
    else if (type == typeof(SingleRequestResult))
    {
        schema.Description = "Result details for one ad hoc HTTP request.";
    }
    else if (type == typeof(ExecutionResultRow))
    {
        schema.Description = "Parsed CSV result row from an execution result file.";
    }
    else if (type == typeof(OpenApiEndpointExecuteRequest))
    {
        schema.Description = "Request payload for executing one endpoint from a stored OpenAPI specification.";
    }
    else if (type == typeof(OpenApiEndpointExecuteResult))
    {
        schema.Description = "Result details from executing one endpoint from a stored OpenAPI specification.";
    }
    else if (type == typeof(OpenApiEndpointsResponse))
    {
        schema.Description = "Parsed endpoint details for a stored OpenAPI specification.";
    }
    else if (type == typeof(OpenApiEndpointInfo))
    {
        schema.Description = "Parsed operation metadata for one endpoint in a stored OpenAPI specification.";
        SetSchemaProperty(schema, "path", "OpenAPI path template.", example: "/api/status");
        SetSchemaProperty(schema, "method", "HTTP method for the endpoint.", example: "GET");
        SetSchemaProperty(schema, "operationId", "OpenAPI operation identifier when present.", example: "GetStatus");
        SetSchemaProperty(schema, "summary", "Short OpenAPI summary when present.", example: "Get service status");
        SetSchemaProperty(schema, "description", "Detailed OpenAPI description when present.");
        SetSchemaProperty(schema, "tags", "OpenAPI tags associated with this endpoint.");
        SetSchemaProperty(schema, "parameters", "Parsed OpenAPI parameters.");
        SetSchemaProperty(schema, "requestBody", "Parsed OpenAPI request body metadata.");
        SetSchemaProperty(schema, "responses", "Parsed response code descriptions.");
        SetSchemaProperty(schema, "isDeprecated", "Whether the OpenAPI operation is marked deprecated.", defaultValue: false, example: false);
        SetSchemaProperty(schema, "securityRequirements", "Security requirements declared for this endpoint.");
    }
    else if (type == typeof(OpenApiParameterInfo))
    {
        schema.Description = "Parsed parameter metadata from a stored OpenAPI specification.";
        SetSchemaProperty(schema, "name", "Parameter name.", example: "id");
        SetSchemaProperty(schema, "in", "Parameter location: path, query, header, or cookie.", example: "path");
        SetSchemaProperty(schema, "description", "Parameter description from the OpenAPI specification.");
        SetSchemaProperty(schema, "required", "Whether the parameter is required.", defaultValue: false, example: true);
        SetSchemaProperty(schema, "type", "JSON schema type.", defaultValue: "string", example: "string");
        SetSchemaProperty(schema, "format", "Optional JSON schema format.", example: "uuid");
        SetSchemaProperty(schema, "defaultValue", "Default parameter value when supplied by the OpenAPI specification.");
        SetSchemaProperty(schema, "example", "Example parameter value when supplied by the OpenAPI specification.");
        SetSchemaProperty(schema, "enumValues", "Allowed enum values when declared by the OpenAPI specification.");
    }
    else if (type == typeof(OpenApiRequestBodyInfo))
    {
        schema.Description = "Parsed request body metadata from a stored OpenAPI specification.";
        SetSchemaProperty(schema, "description", "Request body description from the OpenAPI specification.");
        SetSchemaProperty(schema, "required", "Whether the request body is required.", defaultValue: false, example: true);
        SetSchemaProperty(schema, "contentType", "Request content type.", defaultValue: "application/json", example: "application/json");
        SetSchemaProperty(schema, "schemaJson", "Raw JSON schema for the request body.");
        SetSchemaProperty(schema, "exampleJson", "Example JSON body from the OpenAPI specification.");
    }

    return Task.CompletedTask;
}

static void SetParameterMetadata(OpenApiOperation operation, string name, string description, object? example)
{
    var parameter = operation.Parameters?.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));
    if (parameter is null)
    {
        return;
    }

    if (parameter is OpenApiParameter openApiParameter)
    {
        openApiParameter.Description = description;
        openApiParameter.Example = ToJsonNode(example);
    }

    if (parameter.Schema is OpenApiSchema parameterSchema)
    {
        parameterSchema.Description = description;
        parameterSchema.Example = ToJsonNode(example);
        parameterSchema.Default ??= ToJsonNode(example);
    }
}

static void SetJsonRequestExample(OpenApiOperation operation, string json)
{
    if (operation.RequestBody?.Content?.TryGetValue("application/json", out var mediaType) == true)
    {
        mediaType.Example = JsonNode.Parse(json);
    }
}

static void SetSchemaProperty(
    OpenApiSchema schema,
    string propertyName,
    string description,
    object? defaultValue = null,
    object? example = null,
    string? minimum = null,
    string? maximum = null,
    int? minLength = null,
    int? maxLength = null)
{
    if (schema.Properties is null || !schema.Properties.TryGetValue(propertyName, out var property) || property is not OpenApiSchema openApiProperty)
    {
        return;
    }

    openApiProperty.Description = description;
    openApiProperty.Minimum = minimum;
    openApiProperty.Maximum = maximum;
    openApiProperty.MinLength = minLength;
    openApiProperty.MaxLength = maxLength;

    if (defaultValue is not null)
    {
        openApiProperty.Default = ToJsonNode(defaultValue);
    }

    if (example is not null)
    {
        openApiProperty.Example = ToJsonNode(example);
    }
}

static JsonNode? ToJsonNode(object? value) => value switch
{
    null => null,
    JsonNode node => node.DeepClone(),
    string stringValue => JsonValue.Create(stringValue),
    int intValue => JsonValue.Create(intValue),
    long longValue => JsonValue.Create(longValue),
    bool boolValue => JsonValue.Create(boolValue),
    double doubleValue => JsonValue.Create(doubleValue),
    decimal decimalValue => JsonValue.Create(decimalValue),
    _ => JsonSerializer.SerializeToNode(value)
};

/// <summary>
/// Initialize sample data including default configuration and collection
/// </summary>
/// <param name="serviceProvider">Service provider for dependency injection</param>
static async Task InitializeSampleDataAsync(IServiceProvider serviceProvider)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();

        // Check if initial configuration exists
        var existingConfigurations = await configurationService.GetAllAsync();
        var initialConfig = existingConfigurations.FirstOrDefault(c => c.Name == "Initial RequestSpark Configuration");

        if (initialConfig == null)
        {
            logger.LogInformation("Creating initial configuration based on console RequestSpark defaults...");

            // Create initial configuration based on console RequestSpark defaults
            var configuration = new RequestSpark.Web.Models.TestConfiguration
            {
                Name = "Initial RequestSpark Configuration",
                Description = "Default configuration created automatically using console RequestSpark values (100 iterations, 10 concurrency)",
                Iterations = 100, // Default from console app
                MaxConcurrency = 10, // Default from console app
                Tags = new List<string> { "initial", "demo", "auto-generated", "console-defaults" },
                CreatedBy = "System",
                IsActive = true,
                Runner = new CompareRunner
                {
                    // Set up instances (matching console app defaults)
                    Instances = new List<CompareInstance>
                    {
                        new() { Name = "Local", BaseUrl = "https://localhost:44315/" },
                        new() { Name = "Demo", BaseUrl = "https://ui.makeboldspark.com/" }
                    },

                    // Set up users (matching console app pattern)
                    Users = new List<CompareUser>
                    {
                        new()
                        {
                            UserName = "testuser",
                            Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword,
                            Properties = new Dictionary<string, string>
                            {
                                { "email", "test@example.com" },
                                { "role", "tester" },
                                { "department", "QA" }
                            }
                        },
                        new()
                        {
                            UserName = "admin",
                            Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword,
                            Properties = new Dictionary<string, string>
                            {
                                { "email", "admin@example.com" },
                                { "role", "administrator" },
                                { "department", "IT" }
                            }
                        }
                    },

                    // Set up requests based on available API endpoints
                    Requests = new List<CompareRequest>
                    {
                        new()
                        {
                            Path = "api/status",
                            RequestMethod = HttpVerb.GET,
                            RequiresClientToken = false
                        },
                        new()
                        {
                            Path = "api/employee",
                            RequestMethod = HttpVerb.GET,
                            RequiresClientToken = false
                        },
                        new()
                        {
                            Path = "api/employee/1",
                            RequestMethod = HttpVerb.GET,
                            RequiresClientToken = false
                        },
                        new()
                        {
                            Path = "api/department",
                            RequestMethod = HttpVerb.GET,
                            RequiresClientToken = false
                        }
                    }
                }
            };

            await configurationService.CreateAsync(configuration);
            logger.LogInformation("Initial configuration created with ID: {ConfigurationId}, Total Tests: {TotalTests}",
                configuration.Id, configuration.GetTotalTestCount());
        }
        else
        {
            logger.LogInformation("Initial configuration already exists with ID: {ConfigurationId}", initialConfig.Id);
        }

        logger.LogInformation("Sample data initialization completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize sample data");
    }
}

sealed class ApiRouteApiExplorerConvention : Microsoft.AspNetCore.Mvc.ApplicationModels.IActionModelConvention
{
    public void Apply(Microsoft.AspNetCore.Mvc.ApplicationModels.ActionModel action)
    {
        if (action.Selectors.Any(selector =>
                selector.AttributeRouteModel?.Template?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true))
        {
            action.ApiExplorer.IsVisible = true;
        }
    }
}


