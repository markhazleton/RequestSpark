using Moq;

namespace RequestSpark.Web.Tests.Controllers;

/// <summary>
/// Tests for <see cref="ExecutionController"/> API actions.
/// </summary>
[TestClass]
public class ExecutionControllerTests
{
        /// <summary>
    /// Verifies that starting an execution without a configuration ID returns a bad request.
    /// </summary>
    [TestMethod]
    public async Task StartExecution_WithoutConfigurationId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.StartExecution(new StartExecutionRequest());

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
    }

        /// <summary>
    /// Verifies that starting an execution for an unknown configuration returns not found.
    /// </summary>
    [TestMethod]
    public async Task StartExecution_WithUnknownConfiguration_ReturnsNotFound()
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetByIdAsync("missing")).ReturnsAsync((TestConfiguration?)null);

        var controller = CreateController(configurationService: configurationService.Object);

        var result = await controller.StartExecution(new StartExecutionRequest { ConfigurationId = "missing" });

        Assert.IsInstanceOfType<NotFoundObjectResult>(result.Result);
    }

        /// <summary>
    /// Verifies that starting an execution with a valid configuration returns a created result.
    /// </summary>
    [TestMethod]
    public async Task StartExecution_WithValidRequest_ReturnsCreatedAtAction()
    {
        var configuration = new TestConfiguration
        {
            Name = "Config",
            Runner = new CompareRunner
            {
                Instances = [new CompareInstance { Name = "Local", BaseUrl = "https://example.com/" }],
                Requests = [new CompareRequest { Path = "api/status", RequestMethod = HttpVerb.GET }]
            }
        };

        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetByIdAsync("cfg1")).ReturnsAsync(configuration);

        var executionService = new Mock<IExecutionService>();
        executionService.Setup(service => service.StartExecutionAsync("cfg1", "API"))
            .ReturnsAsync(new TestExecution { Id = "exec1", ConfigurationId = "cfg1", ConfigurationName = "Config" });

        var controller = CreateController(executionService.Object, configurationService.Object);

        var result = await controller.StartExecution(new StartExecutionRequest { ConfigurationId = "cfg1" });

        var createdResult = result.Result as CreatedAtActionResult;
        Assert.IsNotNull(createdResult);
        Assert.AreEqual(nameof(ExecutionController.GetExecutionStatus), createdResult.ActionName);
    }

    private static ExecutionController CreateController(
        IExecutionService? executionService = null,
        IConfigurationService? configurationService = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        return new ExecutionController(
            executionService ?? Mock.Of<IExecutionService>(),
            configurationService ?? Mock.Of<IConfigurationService>(),
            httpClientFactory ?? Mock.Of<IHttpClientFactory>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ExecutionController>>());
    }
}
