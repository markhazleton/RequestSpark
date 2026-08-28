using Moq;

namespace RequestSpark.Web.Tests.Controllers;

/// <summary>
/// Tests for <see cref="RunnerController"/> dashboard actions.
/// </summary>
[TestClass]
public class RunnerControllerTests
{
        /// <summary>
    /// Verifies that the dashboard index action returns a populated dashboard view model.
    /// </summary>
    [TestMethod]
    public async Task Index_ReturnsDashboardViewModel()
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetAllAsync()).ReturnsAsync(
        [
            new TestConfiguration
            {
                Id = "cfg1",
                Name = "Demo Config",
                Runner = new CompareRunner
                {
                    Instances = [new CompareInstance { Name = "Local", BaseUrl = "https://example.com/" }],
                    Requests = [new CompareRequest { Path = "api/status", RequestMethod = HttpVerb.GET }]
                }
            }
        ]);

        var collectionService = new Mock<ICollectionService>();
        collectionService.Setup(service => service.GetAllAsync()).ReturnsAsync(
        [
            new CollectionMetadata { Id = "col1", Name = "Demo Collection", FileName = "collection.json", FilePath = "collection.json" }
        ]);

        var executionService = new Mock<IExecutionService>();
        executionService.Setup(service => service.GetRecentExecutionsAsync(5)).ReturnsAsync([]);
        executionService.Setup(service => service.GetRunningExecutionsAsync()).ReturnsAsync([]);

        var controller = new RunnerController(
            configurationService.Object,
            collectionService.Object,
            executionService.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<RunnerController>>());

        var result = await controller.Index() as ViewResult;

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<DashboardViewModel>(result.Model);
        var model = (DashboardViewModel)result.Model!;
        Assert.AreEqual(1, model.Configurations.Count);
        Assert.AreEqual(1, model.Collections.Count);
    }
}
