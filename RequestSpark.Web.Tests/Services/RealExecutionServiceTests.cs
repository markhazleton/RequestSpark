using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RequestSpark.Web.Hubs;

namespace RequestSpark.Web.Tests.Services;

/// <summary>
/// Tests for <see cref="RealExecutionService"/> validation and delegated operations.
/// </summary>
[TestClass]
public class RealExecutionServiceTests
{
        /// <summary>
    /// Verifies that starting an execution fails when the configuration is missing.
    /// </summary>
    [TestMethod]
    public async Task StartExecutionAsync_WhenConfigurationIsMissing_ThrowsInvalidOperationException()
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.GetByIdAsync("missing")).ReturnsAsync((TestConfiguration?)null);

        var service = CreateService(configurationService: configurationService.Object);

        var exception = await AssertThrowsAsync<InvalidOperationException>(() => service.StartExecutionAsync("missing"));

        Assert.AreEqual("Configuration missing not found", exception.Message);
    }

        /// <summary>
    /// Verifies that starting an execution fails when the configuration is invalid.
    /// </summary>
    [TestMethod]
    public async Task StartExecutionAsync_WhenConfigurationIsInvalid_ThrowsInvalidOperationException()
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService
            .Setup(service => service.GetByIdAsync("invalid"))
            .ReturnsAsync(new TestConfiguration
            {
                Id = "invalid",
                Name = "Invalid Config",
                Runner = new CompareRunner()
            });

        var service = CreateService(configurationService: configurationService.Object);

        var exception = await AssertThrowsAsync<InvalidOperationException>(() => service.StartExecutionAsync("invalid"));

        Assert.AreEqual("Configuration is not valid", exception.Message);
    }

        /// <summary>
    /// Verifies that history lookup by ID delegates to the history store.
    /// </summary>
    [TestMethod]
    public async Task GetExecutionHistoryAsync_ById_DelegatesToHistoryStore()
    {
        var expected = new ExecutionHistory { Id = "exec-1", ConfigurationId = "config-1", ConfigurationName = "Config 1" };
        var historyStore = new Mock<IExecutionHistoryStore>();
        historyStore.Setup(store => store.GetAsync("exec-1", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var service = CreateService(historyStore: historyStore.Object);

        var actual = await service.GetExecutionHistoryAsync("exec-1");

        Assert.AreSame(expected, actual);
        historyStore.Verify(store => store.GetAsync("exec-1", It.IsAny<CancellationToken>()), Times.Once);
    }

        /// <summary>
    /// Verifies that deleting execution history delegates to the history store.
    /// </summary>
    [TestMethod]
    public async Task DeleteExecutionHistoryAsync_DelegatesToHistoryStore()
    {
        var historyStore = new Mock<IExecutionHistoryStore>();
        historyStore.Setup(store => store.DeleteAsync("exec-2", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = CreateService(historyStore: historyStore.Object);

        var deleted = await service.DeleteExecutionHistoryAsync("exec-2");

        Assert.IsTrue(deleted);
        historyStore.Verify(store => store.DeleteAsync("exec-2", It.IsAny<CancellationToken>()), Times.Once);
    }

        /// <summary>
    /// Verifies that aggregated statistics lookup delegates to the history store.
    /// </summary>
    [TestMethod]
    public async Task GetAggregatedStatisticsAsync_DelegatesToHistoryStore()
    {
        var expected = new ExecutionStatistics();
        var historyStore = new Mock<IExecutionHistoryStore>();
        historyStore
            .Setup(store => store.GetAggregatedStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), "config-3"))
            .ReturnsAsync(expected);

        var service = CreateService(historyStore: historyStore.Object);

        var actual = await service.GetAggregatedStatisticsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "config-3");

        Assert.AreSame(expected, actual);
    }

        /// <summary>
    /// Verifies that cancelling an unknown execution returns false.
    /// </summary>
    [TestMethod]
    public async Task CancelExecutionAsync_WhenExecutionDoesNotExist_ReturnsFalse()
    {
        var service = CreateService();

        var cancelled = await service.CancelExecutionAsync("missing");

        Assert.IsFalse(cancelled);
    }

    private static RealExecutionService CreateService(
        IConfigurationService? configurationService = null,
        IExecutionHistoryStore? historyStore = null)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();

        configurationService ??= Mock.Of<IConfigurationService>();
        historyStore ??= Mock.Of<IExecutionHistoryStore>();

        serviceProvider
            .Setup(provider => provider.GetService(typeof(IConfigurationService)))
            .Returns(configurationService);

        scope.SetupGet(value => value.ServiceProvider).Returns(serviceProvider.Object);
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);

        return new RealExecutionService(
            scopeFactory.Object,
            Mock.Of<IHttpClientFactory>(),
            historyStore,
            Mock.Of<IHubContext<ExecutionHub>>(),
            Mock.Of<ILogger<RealExecutionService>>());
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
        throw new InvalidOperationException("Unreachable");
    }
}
