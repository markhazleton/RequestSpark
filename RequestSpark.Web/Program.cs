using Microsoft.AspNetCore.Http.Features; // Add for FormOptions
using Microsoft.AspNetCore.Server.Kestrel.Core; // Add for KestrelServerOptions
using Microsoft.OpenApi; // Add for Swagger
using RequestSpark.Domain.Interfaces;
using RequestSpark.Domain.Models;
using RequestSpark.Web.SampleCRUD;
using RequestSpark.Web.Services; // Add for our new services
using WebSpark.Bootswatch; // Add for Bootswatch
using WebSpark.HttpClientUtility; // Add for HttpClientUtility
using WebSpark.HttpClientUtility.ClientService;
using WebSpark.HttpClientUtility.RequestResult; // Add for HttpRequestResultService
using WebSpark.HttpClientUtility.StringConverter;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RequestSpark API",
        Version = "v1",
        Description = "RequestSpark API for testing and benchmarking REST endpoints"
    });

    // Group endpoints by tag (controller-like grouping for minimal APIs)
    c.TagActionsBy(api =>
    {
        // Check if ActionDescriptor has tags
        if (api.GroupName != null)
            return new[] { api.GroupName };

        var path = api.RelativePath?.ToLower();
        if (path != null)
        {
            if (path.StartsWith("api/employee"))
                return new[] { "Employee" };
            if (path.StartsWith("api/department"))
                return new[] { "Department" };
            if (path.StartsWith("api/debug"))
                return new[] { "Debug" };
            if (path.StartsWith("api/initialization"))
                return new[] { "System" };
            if (path.StartsWith("api/status") || path.StartsWith("api/api-explorer"))
                return new[] { "System" };
        }
        return new[] { "Other" };
    });

    c.DocInclusionPredicate((name, api) => true);

    // Handle polymorphic types and circular references
    c.UseAllOfToExtendReferenceSchemas();

    // Custom schema IDs to avoid naming conflicts
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    // Add support for Newtonsoft.Json attributes in the SampleCRUD client
    c.SupportNonNullableReferenceTypes();
});

// Add Newtonsoft.Json support for Swagger (must be called after AddSwaggerGen)
builder.Services.AddSwaggerGenNewtonsoftSupport();

builder.Services.AddControllersWithViews();
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

// Enable Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RequestSpark API v1");
        c.RoutePrefix = "docs"; // Swagger UI at /docs
    });
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
.WithTags("Employee")
.WithName("GetAllEmployees")
.Produces<ICollection<EmployeeDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/employee/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.GetEmployeeById(id)))
.WithTags("Employee")
.WithName("GetEmployeeById")
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapPost("/api/employee", async (SampleCRUDService service, EmployeeDto employee) =>
{
    var validationErrors = EmployeeDtoValidator.Validate(employee);
    return validationErrors.Count > 0
        ? Results.ValidationProblem(validationErrors)
    : await Proxy(() => service.CreateEmployee(employee));
})
.WithTags("Employee")
.WithName("CreateEmployee")
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
.WithTags("Employee")
.WithName("UpdateEmployee")
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapDelete("/api/employee/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.DeleteEmployee(id)))
.WithTags("Employee")
.WithName("DeleteEmployee")
.Produces<EmployeeDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

// Department Endpoints
app.MapGet("/api/department", (SampleCRUDService service, bool? includeEmployees) =>
    Proxy(() => service.GetDepartments(includeEmployees)))
.WithTags("Department")
.WithName("GetDepartments")
.Produces<ICollection<DepartmentDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/department/{id}", (SampleCRUDService service, int id) =>
    Proxy(() => service.GetDepartmentById(id)))
.WithTags("Department")
.WithName("GetDepartmentById")
.Produces<ICollection<DepartmentDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/api-explorer", (SampleCRUDService service) =>
    Proxy(() => service.GetApiExplorer()))
.WithTags("System")
.WithName("GetApiExplorer")
.Produces<ICollection<ApiExplorerModel>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway)
.ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.MapGet("/api/status", (SampleCRUDService service) =>
    Proxy(() => service.GetStatus()))
.WithTags("System")
.WithName("GetStatus")
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

        return Results.Ok(new
        {
            timestamp = DateTime.UtcNow,
            initialized = true,
            sampleCollection = new
            {
                exists = sampleCollection != null,
                id = sampleCollection?.Id,
                name = sampleCollection?.Name,
                requestCount = sampleCollection?.RequestCount ?? 0
            },
            initialConfiguration = new
            {
                exists = initialConfig != null,
                id = initialConfig?.Id,
                name = initialConfig?.Name,
                totalTests = initialConfig?.GetTotalTestCount() ?? 0,
                iterations = initialConfig?.Iterations ?? 0,
                concurrency = initialConfig?.MaxConcurrency ?? 0
            },
            totals = new
            {
                configurations = configurations.Count,
                collections = collections.Count,
                activeConfigurations = configurations.Count(c => c.IsActive)
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
}).WithTags("System");


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

            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                totalConfigurations = configurations.Count,
                configurations = configurations.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    description = c.Description,
                    isActive = c.IsActive,
                    createdAt = c.CreatedAt,
                    iterations = c.Iterations,
                    maxConcurrency = c.MaxConcurrency,
                    instancesCount = c.Runner?.Instances?.Count ?? 0,
                    usersCount = c.Runner?.Users?.Count ?? 0,
                    requestsCount = c.Runner?.Requests?.Count ?? 0,
                    editUrl = $"/Configuration/Edit/{c.Id}"
                })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Debug configurations check failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }).WithTags("Debug");
}

// Map SignalR hub for real-time execution updates
app.MapHub<RequestSpark.Web.Hubs.ExecutionHub>("/hubs/execution");

app.Run();

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


















