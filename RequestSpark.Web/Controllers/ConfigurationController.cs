using Microsoft.AspNetCore.Mvc;
using RequestSpark.Domain.Models;
using RequestSpark.Web.Models;
using RequestSpark.Web.Models.ViewModels;
using RequestSpark.Web.Services;
using System.Text.Json;

namespace RequestSpark.Web.Controllers
{
    /// <summary>
    /// Controller for managing RequestSpark configurations
    /// </summary>
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationService _configurationService;
        private readonly ICollectionService _collectionService;
        private readonly IOpenApiService _openApiService;
        private readonly IApiDefinitionMappingService _apiDefinitionMappingService;
        private readonly ILogger<ConfigurationController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ConfigurationController"/>.
        /// </summary>
        /// <param name="configurationService">Service for configuration storage and retrieval.</param>
        /// <param name="collectionService">Service for collection storage and retrieval.</param>
        /// <param name="openApiService">Service for OpenAPI specification management.</param>
        /// <param name="apiDefinitionMappingService">Service for mapping API definitions into runner requests.</param>
        /// <param name="logger">Logger instance.</param>
        public ConfigurationController(
            IConfigurationService configurationService,
            ICollectionService collectionService,
            IOpenApiService openApiService,
            IApiDefinitionMappingService apiDefinitionMappingService,
            ILogger<ConfigurationController> logger)
        {
            _configurationService = configurationService;
            _collectionService = collectionService;
            _openApiService = openApiService;
            _apiDefinitionMappingService = apiDefinitionMappingService;
            _logger = logger;
        }

        /// <summary>
        /// Display list of configurations
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var configurations = await _configurationService.GetAllAsync();
                return View(configurations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configurations");
                TempData["Error"] = "Failed to load configurations";
                return View(new List<TestConfiguration>());
            }
        }

        /// <summary>
        /// Display configuration details
        /// </summary>
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                var configuration = await _configurationService.GetByIdAsync(id);
                if (configuration == null)
                    return NotFound();

                return View(configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration {Id}", id);
                TempData["Error"] = "Failed to load configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Show create configuration form
        /// </summary>
        public async Task<IActionResult> Create()
        {
            var viewModel = new ConfigurationViewModel();
            await PopulateViewModelCollections(viewModel);
            return View(viewModel);
        }

        /// <summary>
        /// Handle configuration creation
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConfigurationViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await PopulateViewModelCollections(viewModel);
                return View(viewModel);
            }

            try
            {
                var configuration = await MapViewModelToConfiguration(viewModel);
                var validation = await _configurationService.ValidateAsync(configuration);

                if (!validation.IsValid)
                {
                    foreach (var error in validation.Errors)
                        ModelState.AddModelError(string.Empty, error);

                    foreach (var warning in validation.Warnings)
                        TempData["Warning"] = warning;

                    await PopulateViewModelCollections(viewModel);
                    return View(viewModel);
                }

                await _configurationService.CreateAsync(configuration);
                TempData["Success"] = $"Configuration '{configuration.Name}' created successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating configuration");
                ModelState.AddModelError(string.Empty, "Failed to create configuration");
                await PopulateViewModelCollections(viewModel);
                return View(viewModel);
            }
        }

        /// <summary>
        /// Show edit configuration form
        /// </summary>
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                _logger.LogInformation("Edit requested for configuration ID: {Id}", id);

                var configuration = await _configurationService.GetByIdAsync(id);
                if (configuration == null)
                {
                    _logger.LogWarning("Configuration not found for ID: {Id}", id);
                    return NotFound();
                }

                _logger.LogInformation("Configuration loaded: {Name}, Instances: {InstancesCount}, Users: {UsersCount}, Requests: {RequestsCount}",
                    configuration.Name, configuration.Runner.Instances?.Count ?? 0, configuration.Runner.Users?.Count ?? 0, configuration.Runner.Requests?.Count ?? 0);

                var viewModel = await MapConfigurationToViewModel(configuration);

                _logger.LogInformation("ViewModel mapped: InstancesJson length: {InstancesLength}, UsersJson length: {UsersLength}, RequestsJson length: {RequestsLength}",
                    viewModel.InstancesJson?.Length ?? 0, viewModel.UsersJson?.Length ?? 0, viewModel.RequestsJson?.Length ?? 0);

                await PopulateViewModelCollections(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration for edit {Id}", id);
                TempData["Error"] = "Failed to load configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle configuration update
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ConfigurationViewModel viewModel)
        {
            if (id != viewModel.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await PopulateViewModelCollections(viewModel);
                return View(viewModel);
            }

            try
            {
                var configuration = await MapViewModelToConfiguration(viewModel);
                var validation = await _configurationService.ValidateAsync(configuration);

                if (!validation.IsValid)
                {
                    foreach (var error in validation.Errors)
                        ModelState.AddModelError(string.Empty, error);

                    foreach (var warning in validation.Warnings)
                        TempData["Warning"] = warning;

                    await PopulateViewModelCollections(viewModel);
                    return View(viewModel);
                }

                await _configurationService.UpdateAsync(configuration);
                TempData["Success"] = $"Configuration '{configuration.Name}' updated successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration {Id}", id);
                ModelState.AddModelError(string.Empty, "Failed to update configuration");
                await PopulateViewModelCollections(viewModel);
                return View(viewModel);
            }
        }

        /// <summary>
        /// Show delete confirmation
        /// </summary>
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                var configuration = await _configurationService.GetByIdAsync(id);
                if (configuration == null)
                    return NotFound();

                return View(configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration for delete {Id}", id);
                TempData["Error"] = "Failed to load configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Handle configuration deletion
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var configuration = await _configurationService.GetByIdAsync(id);
                if (configuration == null)
                    return NotFound();

                await _configurationService.DeleteAsync(id);
                TempData["Success"] = $"Configuration '{configuration.Name}' deleted successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting configuration {Id}", id);
                TempData["Error"] = "Failed to delete configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Export configuration as JSON
        /// </summary>
        public async Task<IActionResult> Export(string id)
        {
            try
            {
                var json = await _configurationService.ExportAsync(id);
                if (json == null)
                    return NotFound();

                var configuration = await _configurationService.GetByIdAsync(id);
                var fileName = $"{configuration?.Name ?? "configuration"}_{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting configuration {Id}", id);
                TempData["Error"] = "Failed to export configuration";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Debug action to check JSON mapping
        /// </summary>
        public async Task<IActionResult> DebugEdit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                var configuration = await _configurationService.GetByIdAsync(id);
                if (configuration == null)
                    return NotFound();

                var viewModel = await MapConfigurationToViewModel(configuration);

                return Json(new
                {
                    ConfigurationId = configuration.Id,
                    ConfigurationName = configuration.Name,
                    InstancesCount = configuration.Runner.Instances.Count,
                    UsersCount = configuration.Runner.Users.Count,
                    RequestsCount = configuration.Runner.Requests.Count,
                    ViewModelInstancesJson = viewModel.InstancesJson,
                    ViewModelUsersJson = viewModel.UsersJson,
                    ViewModelRequestsJson = viewModel.RequestsJson,
                    OriginalInstances = configuration.Runner.Instances,
                    OriginalUsers = configuration.Runner.Users,
                    OriginalRequests = configuration.Runner.Requests
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Helper action to redirect to the initial configuration for easy testing
        /// </summary>
        public async Task<IActionResult> EditInitial()
        {
            try
            {
                var configurations = await _configurationService.GetAllAsync();
                var initialConfig = configurations.FirstOrDefault(c => c.Name == "Initial RequestSpark Configuration");

                if (initialConfig != null)
                {
                    return RedirectToAction("Edit", new { id = initialConfig.Id });
                }
                else
                {
                    TempData["Error"] = "Initial configuration not found. Please check the initialization status.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding initial configuration");
                TempData["Error"] = "Failed to find initial configuration";
                return RedirectToAction("Index");
            }
        }

        #region Helper Methods

        private async Task PopulateViewModelCollections(ConfigurationViewModel viewModel)
        {
            try
            {
                var collections = await _collectionService.GetActiveAsync();
                var specs = await _openApiService.GetActiveAsync();

                viewModel.AvailableApiDefinitions = collections.Select(c => new ApiDefinitionOption
                {
                    SourceType = ApiDefinitionType.PostmanCollection,
                    Id = c.Id,
                    Name = c.Name,
                    SourceLabel = "Postman Collection",
                    EndpointCount = c.RequestCount
                }).Concat(specs.Select(s => new ApiDefinitionOption
                {
                    SourceType = ApiDefinitionType.OpenApiSpec,
                    Id = s.Id,
                    Name = s.Title,
                    SourceLabel = "OpenAPI Specification",
                    EndpointCount = s.EndpointCount
                })).OrderBy(s => s.SourceLabel).ThenBy(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load API definition options for dropdown");
                viewModel.AvailableApiDefinitions = new List<ApiDefinitionOption>();
            }
        }

        private async Task<TestConfiguration> MapViewModelToConfiguration(ConfigurationViewModel viewModel)
        {
            var configuration = new TestConfiguration
            {
                Id = string.IsNullOrEmpty(viewModel.Id) ? Guid.NewGuid().ToString() : viewModel.Id,
                Name = viewModel.Name,
                Description = viewModel.Description,
                Iterations = viewModel.Iterations,
                MaxConcurrency = viewModel.MaxConcurrency,
                SourceType = viewModel.SourceType,
                SourceId = viewModel.SourceType == ApiDefinitionType.None ? null : viewModel.SourceId,
                CollectionFileName = viewModel.SourceType == ApiDefinitionType.PostmanCollection ? viewModel.SourceId : null,
                IsActive = viewModel.IsActive,
                Tags = viewModel.GetTags()
            };

            // Parse JSON configurations
            try
            {
                if (!string.IsNullOrWhiteSpace(viewModel.InstancesJson))
                {
                    configuration.Runner.Instances = JsonSerializer.Deserialize<List<CompareInstance>>(viewModel.InstancesJson) ?? new List<CompareInstance>();
                }

                if (!string.IsNullOrWhiteSpace(viewModel.UsersJson))
                {
                    configuration.Runner.Users = JsonSerializer.Deserialize<List<CompareUser>>(viewModel.UsersJson) ?? new List<CompareUser>();
                }

                if (!string.IsNullOrWhiteSpace(viewModel.RequestsJson))
                {
                    configuration.Runner.Requests = JsonSerializer.Deserialize<List<CompareRequest>>(viewModel.RequestsJson) ?? new List<CompareRequest>();
                }

                if (viewModel.SourceType != ApiDefinitionType.None && !string.IsNullOrWhiteSpace(viewModel.SourceId))
                {
                    var mappingResult = await _apiDefinitionMappingService.MapRequestsAsync(viewModel.SourceType, viewModel.SourceId);
                    configuration.SourceName = mappingResult.SourceName;
                    configuration.Runner.Requests = mappingResult.Requests;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON configuration");
                throw new ArgumentException("Invalid JSON in configuration fields");
            }

            return configuration;
        }

        private async Task<ConfigurationViewModel> MapConfigurationToViewModel(TestConfiguration configuration)
        {
            _logger.LogInformation("Mapping configuration to view model: {ConfigId}, {ConfigName}, {ConfigDescription}, {ConfigIterations}, {ConfigConcurrency}",
                configuration.Id, configuration.Name, configuration.Description, configuration.Iterations, configuration.MaxConcurrency);

            var viewModel = new ConfigurationViewModel
            {
                Id = configuration.Id,
                Name = configuration.Name,
                Description = configuration.Description,
                Iterations = configuration.Iterations,
                MaxConcurrency = configuration.MaxConcurrency,
                SourceType = configuration.SourceType == ApiDefinitionType.None && !string.IsNullOrWhiteSpace(configuration.CollectionFileName)
                    ? ApiDefinitionType.PostmanCollection
                    : configuration.SourceType,
                SourceId = !string.IsNullOrWhiteSpace(configuration.SourceId)
                    ? configuration.SourceId
                    : configuration.CollectionFileName,
                IsActive = configuration.IsActive
            };

            _logger.LogInformation("ViewModel created with basic properties: Id={Id}, Name={Name}, Description={Description}, Iterations={Iterations}, MaxConcurrency={MaxConcurrency}",
                viewModel.Id, viewModel.Name, viewModel.Description, viewModel.Iterations, viewModel.MaxConcurrency);

            viewModel.SetTags(configuration.Tags);

            // Serialize to JSON for editing with proper handling of null/empty collections
            var options = new JsonSerializerOptions { WriteIndented = true };

            // Ensure we have actual data to serialize, provide defaults if empty
            var instances = configuration.Runner.Instances?.Any() == true
                ? configuration.Runner.Instances
                : new List<CompareInstance>
                {
                    new() { Name = "Local", BaseUrl = "https://localhost:44315/" },
                    new() { Name = "Demo", BaseUrl = "https://ui.makeboldspark.com/" }
                };

            var users = configuration.Runner.Users?.Any() == true
                ? configuration.Runner.Users
                : new List<CompareUser>
                {
                    new()
                    {
                        UserName = "testuser",
                        Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword,
                        Properties = new Dictionary<string, string>
                        {
                            { "email", "test@example.com" },
                            { "role", "tester" }
                        }
                    }
                };

            var requests = configuration.Runner.Requests?.Any() == true
                ? configuration.Runner.Requests
                : new List<CompareRequest>
                {
                    new() { Path = "api/status", RequestMethod = HttpVerb.GET, RequiresClientToken = false },
                    new() { Path = "api/employee", RequestMethod = HttpVerb.GET, RequiresClientToken = false },
                    new() { Path = "api/employee/1", RequestMethod = HttpVerb.GET, RequiresClientToken = false },
                    new() { Path = "api/department", RequestMethod = HttpVerb.GET, RequiresClientToken = false }
                };

            viewModel.InstancesJson = JsonSerializer.Serialize(instances, options);
            viewModel.UsersJson = JsonSerializer.Serialize(users, options);
            viewModel.RequestsJson = JsonSerializer.Serialize(requests, options);

            _logger.LogInformation("Final ViewModel: Name={Name}, InstancesJson={InstancesLength} chars, UsersJson={UsersLength} chars, RequestsJson={RequestsLength} chars",
                viewModel.Name, viewModel.InstancesJson?.Length ?? 0, viewModel.UsersJson?.Length ?? 0, viewModel.RequestsJson?.Length ?? 0);

            return viewModel;
        }

        #endregion
    }
}
