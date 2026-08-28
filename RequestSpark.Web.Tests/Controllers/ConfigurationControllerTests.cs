using Moq;

namespace RequestSpark.Web.Tests.Controllers;

/// <summary>
/// Tests for <see cref="ConfigurationController"/> MVC actions.
/// </summary>
[TestClass]
public class ConfigurationControllerTests
{
        /// <summary>
    /// Verifies that requesting configuration details without an ID returns not found.
    /// </summary>
    [TestMethod]
    public async Task Details_WithoutId_ReturnsNotFound()
    {
        var controller = new ConfigurationController(
            Mock.Of<IConfigurationService>(),
            Mock.Of<ICollectionService>(),
            Mock.Of<IOpenApiService>(),
            Mock.Of<IApiDefinitionMappingService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<ConfigurationController>>());

        var result = await controller.Details(string.Empty);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
